using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GenealogyApp.Data;
using GenealogyApp.Models;

namespace GenealogyApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GenealogiesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public GenealogiesController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var list = await _db.Genealogies.ToListAsync();
            return Ok(list);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var item = await _db.Genealogies.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Genealogy model)
        {
            model.Id = Guid.NewGuid();
            model.CreatedAt = DateTime.UtcNow;
            _db.Genealogies.Add(model);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = model.Id }, model);
        }

        [HttpGet("{id}/tree")]
        public async Task<IActionResult> GetTree(Guid id, Guid? rootId = null)
        {
            var persons = await _db.Persons
                .Where(p => p.GenealogyId == id)
                .ToListAsync();

            if (!persons.Any())
            {
                return Ok(Array.Empty<TreeNodeDto>());
            }

            var relations = await _db.ParentChildren
                .Where(r => r.GenealogyId == id)
                .ToListAsync();

            var childrenLookup = relations
                .GroupBy(r => r.ParentId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ChildId).Distinct().ToList());

            var personLookup = persons.ToDictionary(p => p.Id);
            var rootPerson = SelectRootPerson(persons, relations, rootId);
            if (rootPerson == null)
            {
                return Ok(Array.Empty<TreeNodeDto>());
            }

            var tree = BuildTree(rootPerson.Id, personLookup, childrenLookup, new HashSet<Guid>());
            return Ok(new[] { tree });
        }

        private static Person? SelectRootPerson(List<Person> persons, List<ParentChild> relations, Guid? rootId)
        {
            if (rootId.HasValue)
            {
                return persons.FirstOrDefault(p => p.Id == rootId.Value);
            }

            var childIds = relations.Select(r => r.ChildId).ToHashSet();
            var roots = persons.Where(p => !childIds.Contains(p.Id)).ToList();

            return roots
                .OrderByDescending(p => string.Equals(p.Gender, "男", StringComparison.OrdinalIgnoreCase))
                .ThenBy(p => p.BirthYear ?? int.MaxValue)
                .FirstOrDefault();
        }

        private static TreeNodeDto BuildTree(
            Guid personId,
            IReadOnlyDictionary<Guid, Person> personLookup,
            IReadOnlyDictionary<Guid, List<Guid>> childrenLookup,
            HashSet<Guid> visiting)
        {
            if (!personLookup.TryGetValue(personId, out var person))
            {
                throw new InvalidOperationException($"Person {personId} not found.");
            }

            if (!visiting.Add(personId))
            {
                return new TreeNodeDto(person.Id, person.GivenName, person.Gender, person.BirthYear, person.Bio, new List<TreeNodeDto>());
            }

            var children = new List<TreeNodeDto>();
            if (childrenLookup.TryGetValue(personId, out var childIds))
            {
                foreach (var childId in childIds)
                {
                    if (personLookup.ContainsKey(childId))
                    {
                        children.Add(BuildTree(childId, personLookup, childrenLookup, visiting));
                    }
                }
            }

            visiting.Remove(personId);

            return new TreeNodeDto(person.Id, person.GivenName, person.Gender, person.BirthYear, person.Bio, children);
        }
    }

    public record TreeNodeDto(Guid Id, string Name, string? Gender, int? BirthYear, string? Bio, List<TreeNodeDto> Children);
}
