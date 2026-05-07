using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GenealogyApp.Data;
using GenealogyApp.Models;
using GenealogyApp.Services;

namespace GenealogyApp.Controllers
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

            if (!await _access.CanAccessGenealogyAsync(userId.Value, model.GenealogyId, cancellationToken))
            {
                return Forbid();
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
    }
}
