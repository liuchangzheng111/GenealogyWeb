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

        /// <summary>新建成员；服务端覆盖 <see cref="Person.Id"/> 与 <see cref="Person.CreatedAt"/>。</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Person model, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanEditGenealogyContentAsync(userId.Value, model.GenealogyId, cancellationToken))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(model.GivenName))
            {
                return BadRequest("姓名不能为空。");
            }

            model.Id = Guid.NewGuid();
            model.CreatedAt = DateTime.UtcNow;
            _db.Persons.Add(model);
            await _db.SaveChangesAsync(cancellationToken);
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
    }

    /// <summary>更新成员 API 请求体。</summary>
    public record UpdatePersonDto(string GivenName, string? Gender, int? BirthYear, int? DeathYear, string? Bio);
}
