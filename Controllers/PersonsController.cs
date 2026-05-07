using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GenealogyApp.Data;
using GenealogyApp.Models;

namespace GenealogyApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PersonsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public PersonsController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet("byGenealogy/{gid}")]
        public async Task<IActionResult> GetByGenealogy(Guid gid, string? q)
        {
            var query = _db.Persons.Where(p => p.GenealogyId == gid);
            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(p => EF.Functions.Like(p.GivenName, $"%{q}%"));
            }
            var list = await query.ToListAsync();
            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Person model)
        {
            model.Id = Guid.NewGuid();
            model.CreatedAt = DateTime.UtcNow;
            _db.Persons.Add(model);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = model.Id }, model);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var p = await _db.Persons.FindAsync(id);
            if (p == null) return NotFound();
            return Ok(p);
        }
    }
}
