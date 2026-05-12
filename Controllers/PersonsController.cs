using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GenealogyWeb.Data;
using GenealogyWeb.Models;
using GenealogyWeb.Services;

namespace GenealogyWeb.Controllers
{
    /// <summary>
    /// 成员（Person）查询与创建：所有操作前校验对目标 <see cref="Person.GenealogyId"/> 的访问权。
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class PersonsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IGenealogyAccessService _access;

        public PersonsController(ApplicationDbContext db, IGenealogyAccessService access)
        {
            _db = db;
            _access = access;
        }

        /// <summary>按族谱列出成员；<paramref name="q"/> 非空时对 <see cref="Person.GivenName"/> 做 SQL Like 模糊匹配。</summary>
        [HttpGet("byGenealogy/{gid}")]
        public async Task<IActionResult> GetByGenealogy(Guid gid, string? q, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanAccessGenealogyAsync(userId.Value, gid, cancellationToken))
            {
                return Forbid();
            }

            var query = _db.Persons.Where(p => p.GenealogyId == gid);
            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(p => EF.Functions.Like(p.GivenName, $"%{q}%"));
            }

            var list = await query.ToListAsync(cancellationToken);
            return Ok(list);
        }

        /// <summary>新建成员；服务端覆盖 Id / CreatedAt，并可同步写入父母关系。</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePersonDto dto, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanEditGenealogyContentAsync(userId.Value, dto.GenealogyId, cancellationToken))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(dto.GivenName))
            {
                return BadRequest("姓名不能为空。");
            }

            if (dto.FatherId.HasValue && dto.MotherId.HasValue && dto.FatherId.Value == dto.MotherId.Value)
            {
                return BadRequest("父亲和母亲不能是同一人。");
            }

            if (dto.SpouseId.HasValue && dto.SpouseId.Value == dto.FatherId)
            {
                return BadRequest("配偶不能与父亲是同一人。");
            }

            if (dto.SpouseId.HasValue && dto.SpouseId.Value == dto.MotherId)
            {
                return BadRequest("配偶不能与母亲是同一人。");
            }

            var father = dto.FatherId.HasValue
                ? await _db.Persons.FirstOrDefaultAsync(p => p.Id == dto.FatherId.Value && p.GenealogyId == dto.GenealogyId, cancellationToken)
                : null;
            var mother = dto.MotherId.HasValue
                ? await _db.Persons.FirstOrDefaultAsync(p => p.Id == dto.MotherId.Value && p.GenealogyId == dto.GenealogyId, cancellationToken)
                : null;
            var spouse = dto.SpouseId.HasValue
                ? await _db.Persons.FirstOrDefaultAsync(p => p.Id == dto.SpouseId.Value && p.GenealogyId == dto.GenealogyId, cancellationToken)
                : null;

            if (dto.FatherId.HasValue && father == null)
            {
                return BadRequest("所选父亲必须属于当前族谱。");
            }

            if (dto.MotherId.HasValue && mother == null)
            {
                return BadRequest("所选母亲必须属于当前族谱。");
            }

            if (dto.SpouseId.HasValue && spouse == null)
            {
                return BadRequest("所选配偶必须属于当前族谱。");
            }

            if (father != null && !IsMale(father.Gender))
            {
                return BadRequest("父亲只能选择男性成员。");
            }

            if (mother != null && !IsFemale(mother.Gender))
            {
                return BadRequest("母亲只能选择女性成员。");
            }

            var model = new Person
            {
                Id = Guid.NewGuid(),
                GenealogyId = dto.GenealogyId,
                GivenName = dto.GivenName.Trim(),
                Gender = string.IsNullOrWhiteSpace(dto.Gender) ? null : dto.Gender.Trim(),
                BirthYear = dto.BirthYear,
                DeathYear = dto.DeathYear,
                Bio = string.IsNullOrWhiteSpace(dto.Bio) ? null : dto.Bio.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            if (spouse != null)
            {
                var existingMarriage = await _db.Marriages.AnyAsync(m =>
                    m.GenealogyId == dto.GenealogyId &&
                    ((m.SpouseAId == spouse.Id && m.SpouseBId == model.Id) ||
                     (m.SpouseAId == model.Id && m.SpouseBId == spouse.Id)), cancellationToken);

                if (existingMarriage)
                {
                    return Conflict("该配偶关系已存在。");
                }
            }

            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            _db.Persons.Add(model);

            if (dto.FatherId.HasValue)
            {
                _db.ParentChildren.Add(new ParentChild
                {
                    GenealogyId = dto.GenealogyId,
                    ParentId = dto.FatherId.Value,
                    ChildId = model.Id,
                    RelationshipType = "father"
                });
            }

            if (dto.MotherId.HasValue)
            {
                _db.ParentChildren.Add(new ParentChild
                {
                    GenealogyId = dto.GenealogyId,
                    ParentId = dto.MotherId.Value,
                    ChildId = model.Id,
                    RelationshipType = "mother"
                });
            }

            if (dto.SpouseId.HasValue)
            {
                _db.Marriages.Add(new Marriage
                {
                    GenealogyId = dto.GenealogyId,
                    SpouseAId = model.Id,
                    SpouseBId = dto.SpouseId.Value
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = model.Id }, model);
        }

        /// <summary>按 Id 取成员；无访问权或跨族谱访问返回 Forbid/NotFound。</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            var p = await _db.Persons.FindAsync(new object[] { id }, cancellationToken);
            if (p == null) return NotFound();

            if (!await _access.CanAccessGenealogyAsync(userId.Value, p.GenealogyId, cancellationToken))
            {
                return Forbid();
            }

            return Ok(p);
        }

        /// <summary>更新成员信息；不可改族谱 Id。</summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePersonDto dto, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.GivenName))
            {
                return BadRequest("姓名不能为空。");
            }

            var entity = await _db.Persons.FindAsync(new object[] { id }, cancellationToken);
            if (entity == null) return NotFound();

            if (!await _access.CanEditGenealogyContentAsync(userId.Value, entity.GenealogyId, cancellationToken))
            {
                return Forbid();
            }

            entity.GivenName = dto.GivenName.Trim();
            entity.Gender = string.IsNullOrWhiteSpace(dto.Gender) ? null : dto.Gender.Trim();
            entity.BirthYear = dto.BirthYear;
            entity.DeathYear = dto.DeathYear;
            entity.Bio = string.IsNullOrWhiteSpace(dto.Bio) ? null : dto.Bio.Trim();
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(entity);
        }

        /// <summary>删除成员及其在本族谱内的父母子女边与婚姻记录。</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            var entity = await _db.Persons.FindAsync(new object[] { id }, cancellationToken);
            if (entity == null) return NotFound();

            if (!await _access.CanEditGenealogyContentAsync(userId.Value, entity.GenealogyId, cancellationToken))
            {
                return Forbid();
            }

            var gid = entity.GenealogyId;
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            await _db.ParentChildren
                .Where(pc => pc.GenealogyId == gid && (pc.ParentId == id || pc.ChildId == id))
                .ExecuteDeleteAsync(cancellationToken);
            await _db.Marriages
                .Where(m => m.GenealogyId == gid && (m.SpouseAId == id || m.SpouseBId == id))
                .ExecuteDeleteAsync(cancellationToken);
            await _db.Persons.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return NoContent();
        }

        private static bool IsMale(string? gender)
            => !string.IsNullOrWhiteSpace(gender) && (
                string.Equals(gender, "男", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(gender, "m", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(gender, "male", StringComparison.OrdinalIgnoreCase));

        private static bool IsFemale(string? gender)
            => !string.IsNullOrWhiteSpace(gender) && (
                string.Equals(gender, "女", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(gender, "f", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(gender, "female", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>更新成员 API 请求体。</summary>
    public record UpdatePersonDto(string GivenName, string? Gender, int? BirthYear, int? DeathYear, string? Bio);

    /// <summary>新建成员 API 请求体，可附带父母关系。</summary>
    public record CreatePersonDto(
        Guid GenealogyId,
        string GivenName,
        string? Gender,
        int? BirthYear,
        int? DeathYear,
        string? Bio,
        Guid? SpouseId,
        Guid? FatherId,
        Guid? MotherId);
}
