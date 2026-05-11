using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GenealogyWeb.Data;
using GenealogyWeb.Models;
using GenealogyWeb.Services;

namespace GenealogyWeb.Controllers
{
    /// <summary>
    /// 族谱 CRUD（当前含列表/详情/创建）与<strong>后代树</strong> JSON（供 Blazor 递归渲染）。
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class GenealogiesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IGenealogyAccessService _access;

        public GenealogiesController(ApplicationDbContext db, IGenealogyAccessService access)
        {
            _db = db;
            _access = access;
        }

        /// <summary>当前用户可访问的族谱列表（创建或受邀）。</summary>
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            var allowed = await _access.GetAccessibleGenealogyIdsAsync(userId.Value, cancellationToken);
            if (allowed.Count == 0)
            {
                return Ok(Array.Empty<Genealogy>());
            }

            var list = await _db.Genealogies
                .Where(g => allowed.Contains(g.Id))
                .ToListAsync(cancellationToken);
            return Ok(list);
        }

        /// <summary>族谱详情；无权限返回 403。</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanAccessGenealogyAsync(userId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            var item = await _db.Genealogies.FindAsync(new object[] { id }, cancellationToken);
            if (item == null) return NotFound();
            return Ok(item);
        }

        /// <summary>当前用户在该族谱中的角色（用于前端控制按钮显隐）。</summary>
        [HttpGet("{id}/me")]
        public async Task<IActionResult> GetMyMembership(Guid id, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanAccessGenealogyAsync(userId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            var role = await _access.GetMembershipRoleAsync(userId.Value, id, cancellationToken);
            return Ok(new { role });
        }

        /// <summary>
        /// 创建族谱：<see cref="Genealogy.CreatedByUserId"/> 取自登录用户，并写入 Owner 的 <see cref="GenealogyUser"/>。
        /// 请勿信任客户端传入的创建者 Id。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateGenealogyDto dto, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Surname))
            {
                return BadRequest("谱名与姓氏不能为空。");
            }

            var model = new Genealogy
            {
                Id = Guid.NewGuid(),
                Title = dto.Title.Trim(),
                Surname = dto.Surname.Trim(),
                CompiledAt = dto.CompiledAt,
                CreatedByUserId = userId.Value,
                CreatedAt = DateTime.UtcNow
            };

            _db.Genealogies.Add(model);
            _db.GenealogyUsers.Add(new GenealogyUser
            {
                GenealogyId = model.Id,
                UserId = userId.Value,
                Role = "Owner",
                InvitedByUserId = null,
                InvitedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = model.Id }, model);
        }

        /// <summary>更新族谱元数据；需 Owner 或 Editor。</summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGenealogyDto dto, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanEditGenealogyContentAsync(userId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Surname))
            {
                return BadRequest("谱名与姓氏不能为空。");
            }

            var entity = await _db.Genealogies.FindAsync(new object[] { id }, cancellationToken);
            if (entity == null) return NotFound();

            entity.Title = dto.Title.Trim();
            entity.Surname = dto.Surname.Trim();
            entity.CompiledAt = dto.CompiledAt;
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(entity);
        }

        /// <summary>删除族谱及其成员、血缘、婚姻、协作行；仅 Owner（含创建者）。</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanManageGenealogyAsync(userId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            var exists = await _db.Genealogies.AnyAsync(g => g.Id == id, cancellationToken);
            if (!exists) return NotFound();

            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            await _db.ParentChildren.Where(pc => pc.GenealogyId == id).ExecuteDeleteAsync(cancellationToken);
            await _db.Marriages.Where(m => m.GenealogyId == id).ExecuteDeleteAsync(cancellationToken);
            await _db.Persons.Where(p => p.GenealogyId == id).ExecuteDeleteAsync(cancellationToken);
            await _db.GenealogyUsers.Where(gu => gu.GenealogyId == id).ExecuteDeleteAsync(cancellationToken);
            await _db.Genealogies.Where(g => g.Id == id).ExecuteDeleteAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return NoContent();
        }

        /// <summary>按邮箱邀请已注册用户；仅 Owner。角色默认为 Editor，可选 Viewer。</summary>
        [HttpPost("{id}/invite")]
        public async Task<IActionResult> Invite(Guid id, [FromBody] InviteGenealogyDto dto, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanManageGenealogyAsync(userId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                return BadRequest("邮箱不能为空。");
            }

            var email = dto.Email.Trim();
            var target = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
            if (target == null)
            {
                return NotFound("该邮箱尚未注册。");
            }

            if (target.Id == userId.Value)
            {
                return BadRequest("不能邀请自己。");
            }

            var role = NormalizeInviteRole(dto.Role);
            if (role == null)
            {
                return BadRequest("角色只能是 Editor 或 Viewer。");
            }

            var already = await _db.GenealogyUsers.AnyAsync(
                gu => gu.GenealogyId == id && gu.UserId == target.Id,
                cancellationToken);
            if (already)
            {
                return Conflict("该用户已是本族谱成员。");
            }

            _db.GenealogyUsers.Add(new GenealogyUser
            {
                GenealogyId = id,
                UserId = target.Id,
                Role = role,
                InvitedByUserId = userId.Value,
                InvitedAt = DateTime.UtcNow
            });

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return Conflict("该用户已是本族谱成员。");
            }

            return Ok(new { message = "邀请成功", userId = target.Id, email = target.Email, role });
        }

        /// <summary>列出族谱协作成员（含邮箱与角色）。</summary>
        [HttpGet("{id}/collaborators")]
        public async Task<IActionResult> GetCollaborators(Guid id, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanAccessGenealogyAsync(userId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            var rows = await (
                from gu in _db.GenealogyUsers.AsNoTracking()
                join u in _db.Users.AsNoTracking() on gu.UserId equals u.Id
                where gu.GenealogyId == id
                orderby gu.InvitedAt
                select new GenealogyCollaboratorDto(u.Id, u.Email, u.UserName, gu.Role, gu.InvitedAt)
            ).ToListAsync(cancellationToken);

            return Ok(rows);
        }

        private static string? NormalizeInviteRole(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return "Editor";
            }

            var r = role.Trim();
            if (string.Equals(r, "Editor", StringComparison.OrdinalIgnoreCase))
            {
                return "Editor";
            }

            if (string.Equals(r, "Viewer", StringComparison.OrdinalIgnoreCase))
            {
                return "Viewer";
            }

            return null;
        }

        /// <summary>
        /// 自根向下构建后代树。未指定 <paramref name="rootId"/> 时，在「无父边」的节点中选根（启发式：偏男性、出生年早）。
        /// </summary>
        /// <returns>长度为 1 的数组，元素为根节点 <see cref="TreeNodeDto"/>；无数据时为空数组。</returns>
        [HttpGet("{id}/tree")]
        public async Task<IActionResult> GetTree(Guid id, Guid? rootId = null, CancellationToken cancellationToken = default)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanAccessGenealogyAsync(userId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            var persons = await _db.Persons
                .Where(p => p.GenealogyId == id)
                .ToListAsync(cancellationToken);

            if (!persons.Any())
            {
                return Ok(Array.Empty<TreeNodeDto>());
            }

            var relations = await _db.ParentChildren
                .Where(r => r.GenealogyId == id)
                .ToListAsync(cancellationToken);

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

        /// <summary>确定树根：显式 rootId，或「不作为任何边的子」的节点集合中的启发式选择。</summary>
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

        /// <summary>深度优先构建树；<paramref name="visiting"/> 用于检测环，避免无限递归。</summary>
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
                // 数据存在环时：不再向下展开，返回空子节点列表。
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

    /// <summary>创建族谱 API 的请求体（勿包含创建者 Id）。</summary>
    public record CreateGenealogyDto(string Title, string Surname, DateTime? CompiledAt);

    /// <summary>更新族谱元数据。</summary>
    public record UpdateGenealogyDto(string Title, string Surname, DateTime? CompiledAt);

    /// <summary>邀请协作者：邮箱须为已注册用户；角色默认 Editor。</summary>
    public record InviteGenealogyDto(string Email, string? Role);

    /// <summary>协作成员列表项。</summary>
    public record GenealogyCollaboratorDto(Guid UserId, string Email, string UserName, string Role, DateTime InvitedAt);

    /// <summary>树节点 DTO，与 Blazor 组件 <c>TreeNodeView</c> 绑定；子节点递归同结构。</summary>
    public record TreeNodeDto(Guid Id, string Name, string? Gender, int? BirthYear, string? Bio, List<TreeNodeDto> Children);
}
