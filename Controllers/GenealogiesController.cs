using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GenealogyWeb.Data;
using GenealogyWeb.Models;
using GenealogyWeb.Services;
using System.Data;
using System.Data.Common;

namespace GenealogyWeb.Controllers
{
    /// <summary>
    /// 族谱 CRUD、协作邀请、<strong>后代树 / 祖先树 / 亲缘路径</strong> 等 JSON API（供 Blazor 与实验报告引用）。
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class GenealogiesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IGenealogyAccessService _access;
        private readonly IGenerationMaintenanceService _generations;

        public GenealogiesController(
            ApplicationDbContext db,
            IGenealogyAccessService access,
            IGenerationMaintenanceService generations)
        {
            _db = db;
            _access = access;
            _generations = generations;
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

        /// <summary>修改协作者角色（仅 Editor / Viewer）；不能修改谱主。</summary>
        [HttpPatch("{id}/collaborators/{userId}")]
        public async Task<IActionResult> PatchCollaboratorRole(
            Guid id,
            Guid userId,
            [FromBody] UpdateCollaboratorRoleDto dto,
            CancellationToken cancellationToken)
        {
            var currentUserId = User.GetUserIdOrNull();
            if (currentUserId is null) return Unauthorized();

            if (!await _access.CanManageGenealogyAsync(currentUserId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            var genealogy = await _db.Genealogies.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
            if (genealogy == null) return NotFound();

            if (userId == genealogy.CreatedByUserId)
            {
                return BadRequest("不能修改谱主在本族谱中的角色。");
            }

            var newRole = NormalizeInviteRole(dto.Role);
            if (newRole == null)
            {
                return BadRequest("角色只能是 Editor 或 Viewer。");
            }

            var row = await _db.GenealogyUsers.FirstOrDefaultAsync(
                gu => gu.GenealogyId == id && gu.UserId == userId,
                cancellationToken);
            if (row == null) return NotFound();

            if (string.Equals(row.Role, "Owner", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("不能通过此接口修改 Owner。");
            }

            row.Role = newRole;
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "已更新角色", userId, role = newRole });
        }

        /// <summary>移除协作者（不能移除谱主）。</summary>
        [HttpDelete("{id}/collaborators/{userId}")]
        public async Task<IActionResult> RemoveCollaborator(Guid id, Guid userId, CancellationToken cancellationToken)
        {
            var currentUserId = User.GetUserIdOrNull();
            if (currentUserId is null) return Unauthorized();

            if (!await _access.CanManageGenealogyAsync(currentUserId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            var genealogy = await _db.Genealogies.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
            if (genealogy == null) return NotFound();

            if (userId == genealogy.CreatedByUserId)
            {
                return BadRequest("不能移除谱主。");
            }

            var row = await _db.GenealogyUsers.FirstOrDefaultAsync(
                gu => gu.GenealogyId == id && gu.UserId == userId,
                cancellationToken);
            if (row == null) return NotFound();

            if (string.Equals(row.Role, "Owner", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("不能移除 Owner 记录。");
            }

            _db.GenealogyUsers.Remove(row);
            await _db.SaveChangesAsync(cancellationToken);
            return NoContent();
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
                .AsNoTracking()
                .Where(p => p.GenealogyId == id)
                .ToListAsync(cancellationToken);

            if (!persons.Any())
            {
                return Ok(Array.Empty<TreeNodeDto>());
            }

            var relations = await _db.ParentChildren
                .AsNoTracking()
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

        /// <summary>
        /// 自某人向上展开祖先树：返回单根 <see cref="TreeNodeDto"/> 数组，其中每个节点的 <c>Children</c> 表示其<strong>父母</strong>（便于复用 <c>TreeNodeView</c> 缩进展示）。
        /// </summary>
        [HttpGet("{id}/ancestors")]
        public async Task<IActionResult> GetAncestors(Guid id, [FromQuery] Guid personId, CancellationToken cancellationToken = default)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanAccessGenealogyAsync(userId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            var persons = await _db.Persons
                .AsNoTracking()
                .Where(p => p.GenealogyId == id)
                .ToListAsync(cancellationToken);

            if (!persons.Any())
            {
                return Ok(Array.Empty<TreeNodeDto>());
            }

            var personLookup = persons.ToDictionary(p => p.Id);
            if (!personLookup.ContainsKey(personId))
            {
                return NotFound("指定成员不属于该族谱或不存在。");
            }

            var relations = await _db.ParentChildren
                .AsNoTracking()
                .Where(r => r.GenealogyId == id)
                .ToListAsync(cancellationToken);

            var parentsByChild = relations
                .GroupBy(r => r.ChildId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ParentId).Distinct().ToList());

            var root = BuildAncestorTree(personId, personLookup, parentsByChild, new HashSet<Guid>());
            return Ok(new[] { root });
        }

        /// <summary>
        /// 两人之间是否存在亲缘通路（无向图：父母子女边 + 配偶边），若有则返回经过节点序列及相邻关系说明。
        /// </summary>
        [HttpGet("{id}/kinship")]
        public async Task<IActionResult> GetKinship(
            Guid id,
            [FromQuery] Guid fromPersonId,
            [FromQuery] Guid toPersonId,
            CancellationToken cancellationToken = default)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanAccessGenealogyAsync(userId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            if (fromPersonId == toPersonId)
            {
                var one = await _db.Persons.AsNoTracking().FirstOrDefaultAsync(p => p.Id == fromPersonId && p.GenealogyId == id, cancellationToken);
                if (one == null) return NotFound();
                return Ok(new KinshipPathResponse(true, new List<KinshipStepDto>
                {
                    new(fromPersonId, one.GivenName, "起点")
                }));
            }

            var persons = await _db.Persons
                .AsNoTracking()
                .Where(p => p.GenealogyId == id)
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            if (!persons.ContainsKey(fromPersonId) || !persons.ContainsKey(toPersonId))
            {
                return NotFound("成员 Id 无效或不属于该族谱。");
            }

            var adj = await BuildKinshipAdjacency(id, cancellationToken);
            var parentRelations = await _db.ParentChildren
                .AsNoTracking()
                .Where(r => r.GenealogyId == id)
                .ToListAsync(cancellationToken);
            var parentSet = new HashSet<(Guid Parent, Guid Child)>(parentRelations.Select(r => (r.ParentId, r.ChildId)));

            var path = BreadthFirstKinshipPath(fromPersonId, toPersonId, adj, persons, parentSet);
            if (path == null)
            {
                return Ok(new KinshipPathResponse(false, null));
            }

            // parentSet 已在上方构建，可直接使用以判断方向（父/子）
            var steps = new List<KinshipStepDto>(path.Count);
            for (var i = 0; i < path.Count; i++)
            {
                var pid = path[i];
                var name = persons[pid].GivenName;
                string label;
                if (i == 0)
                {
                    label = "起点";
                }
                else
                {
                    var prev = path[i - 1];
                    // 查找边的类型
                    var kind = "";
                    if (adj.TryGetValue(prev, out var neighbors))
                    {
                        foreach (var (n, k) in neighbors)
                        {
                            if (n == pid)
                            {
                                kind = k;
                                break;
                            }
                        }
                    }

                    if (kind == "父母—子女")
                    {
                        // 如果 prev 是 pid 的父，则 prev -> pid 应显示为父亲/母亲（根据 prev 性别）
                        if (parentSet.Contains((prev, pid)))
                        {
                            label = string.Equals(persons[prev].Gender, "男", StringComparison.OrdinalIgnoreCase) ? "父亲" : "母亲";
                        }
                        // 如果 prev 是 pid 的子，则 prev -> pid 显示为儿子/女儿（根据 prev 性别）
                        else if (parentSet.Contains((pid, prev)))
                        {
                            label = string.Equals(persons[prev].Gender, "男", StringComparison.OrdinalIgnoreCase) ? "儿子" : "女儿";
                        }
                        else
                        {
                            label = "父母—子女";
                        }
                    }
                    else if (kind == "配偶")
                    {
                        label = "配偶";
                    }
                    else
                    {
                        label = string.IsNullOrEmpty(kind) ? "—" : kind;
                    }
                }

                steps.Add(new KinshipStepDto(pid, name, label));
            }

            return Ok(new KinshipPathResponse(true, steps));
        }

        /// <summary>课程接口：给定成员 Id，返回其配偶与所有子女。</summary>
        [HttpGet("{id}/course/person-relations")]
        public async Task<IActionResult> GetPersonRelations(Guid id, Guid personId, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanAccessGenealogyAsync(userId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            var person = await _db.Persons.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personId && p.GenealogyId == id, cancellationToken);
            if (person == null)
            {
                return NotFound();
            }

            var spouseIds = await _db.Marriages.AsNoTracking()
                .Where(m => m.GenealogyId == id && (m.SpouseAId == personId || m.SpouseBId == personId))
                .Select(m => m.SpouseAId == personId ? m.SpouseBId : m.SpouseAId)
                .ToListAsync(cancellationToken);

            var spouseRelations = await _db.Persons.AsNoTracking()
                .Where(p => p.GenealogyId == id && spouseIds.Contains(p.Id))
                .Select(p => new PersonRelationDto(p.Id, p.GivenName, p.Gender, p.BirthYear, "spouse"))
                .ToListAsync(cancellationToken);

            var childrenRelations = await (
                from pc in _db.ParentChildren.AsNoTracking()
                join c in _db.Persons.AsNoTracking() on pc.ChildId equals c.Id
                where pc.GenealogyId == id && pc.ParentId == personId && c.GenealogyId == id
                select new PersonRelationDto(c.Id, c.GivenName, c.Gender, c.BirthYear, "child")
            ).ToListAsync(cancellationToken);

            var relations = spouseRelations.Concat(childrenRelations)
                .OrderBy(r => r.RelationKind)
                .ThenBy(r => r.GivenName)
                .ToList();

            return Ok(relations);
        }

        /// <summary>按亲子边重算全谱 <see cref="Person.Generation"/>（Owner/Editor）。导入或修复历史数据后调用。</summary>
        [HttpPost("{id}/recalculate-generations")]
        public async Task<IActionResult> RecalculateGenerations(Guid id, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanEditGenealogyContentAsync(userId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            await _generations.RecalculateGenealogyAsync(id, cancellationToken);
            var maxGen = await _db.Persons.AsNoTracking()
                .Where(p => p.GenealogyId == id)
                .MaxAsync(p => (int?)p.Generation, cancellationToken) ?? 0;
            var count = await _db.Persons.AsNoTracking().CountAsync(p => p.GenealogyId == id, cancellationToken);
            return Ok(new { message = "辈分已重算", memberCount = count, maxGeneration = maxGen });
        }

        /// <summary>课程接口：统计各代平均寿命，并返回所有代别结果。</summary>
        [HttpGet("{id}/course/generation-lifespan")]
        public async Task<IActionResult> GetGenerationLifespan(Guid id, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanAccessGenealogyAsync(userId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            var rows = await QueryGenerationLifespanAsync(id, cancellationToken);
            return Ok(rows);
        }

        /// <summary>课程接口：查询当前族谱中年龄超过 50 岁且无配偶的男性成员。</summary>
        [HttpGet("{id}/course/unmarried-male-over50")]
        public async Task<IActionResult> GetUnmarriedMaleOver50(Guid id, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanAccessGenealogyAsync(userId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            var currentYear = DateTime.UtcNow.Year;
            var cutoffYear = currentYear - 51;
            var marriedIds = await _db.Marriages.AsNoTracking()
                .Where(m => m.GenealogyId == id)
                .SelectMany(m => new[] { m.SpouseAId, m.SpouseBId })
                .Distinct()
                .ToListAsync(cancellationToken);

            var result = await _db.Persons.AsNoTracking()
                .Where(p => p.GenealogyId == id && p.BirthYear.HasValue && p.BirthYear <= cutoffYear)
                .Where(p => IsMale(p.Gender))
                .Where(p => !marriedIds.Contains(p.Id))
                .OrderBy(p => p.GivenName)
                .ToListAsync(cancellationToken);

            return Ok(result);
        }

        /// <summary>课程接口：查询出生年份早于本代平均值的成员。</summary>
        [HttpGet("{id}/course/early-births")]
        public async Task<IActionResult> GetEarlyBirths(Guid id, CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            if (!await _access.CanAccessGenealogyAsync(userId.Value, id, cancellationToken))
            {
                return Forbid();
            }

            var result = await QueryEarlyBirthsAsync(id, cancellationToken);
            return Ok(result);
        }

        private async Task<List<GenerationLifespanDto>> QueryGenerationLifespanAsync(Guid genealogyId, CancellationToken cancellationToken)
        {
            const string sql = @"
SELECT p.Generation,
       AVG(p.DeathYear - p.BirthYear) AS AvgLifespanYears,
       COUNT(*) AS MemberCount
FROM Persons p
WHERE p.GenealogyId = @gid
  AND p.BirthYear IS NOT NULL AND p.DeathYear IS NOT NULL AND p.DeathYear >= p.BirthYear
GROUP BY p.Generation
ORDER BY p.Generation;";

            return await ExecuteSqlQueryAsync(sql, reader => new GenerationLifespanDto(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? 0.0 : reader.GetDouble(1),
                reader.GetInt32(2)
            ), genealogyId, cancellationToken);
        }

        private async Task<List<EarlyBirthPersonDto>> QueryEarlyBirthsAsync(Guid genealogyId, CancellationToken cancellationToken)
        {
            const string sql = @"
WITH gen_avg AS (
    SELECT p.Generation AS depth, AVG(p.BirthYear) AS avg_birth_year
    FROM Persons p
    WHERE p.GenealogyId = @gid AND p.BirthYear IS NOT NULL
    GROUP BY p.Generation
)
SELECT p.Id, p.GivenName, p.Gender, p.BirthYear, p.Generation, ga.avg_birth_year
FROM Persons p
JOIN gen_avg ga ON ga.depth = p.Generation
WHERE p.GenealogyId = @gid
  AND p.BirthYear IS NOT NULL AND p.BirthYear < ga.avg_birth_year
ORDER BY p.Generation, p.GivenName;";

            return await ExecuteSqlQueryAsync(sql, reader => new EarlyBirthPersonDto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.GetInt32(4),
                reader.IsDBNull(5) ? 0.0 : reader.GetDouble(5)
            ), genealogyId, cancellationToken);
        }

        private async Task<List<T>> ExecuteSqlQueryAsync<T>(string sql, Func<DbDataReader, T> map, Guid genealogyId, CancellationToken cancellationToken)
        {
            var results = new List<T>();
            var connection = _db.Database.GetDbConnection();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                var param = command.CreateParameter();
                param.ParameterName = "@gid";
                param.Value = genealogyId;
                param.DbType = DbType.Guid;
                command.Parameters.Add(param);

                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(map(reader));
                }
            }

            return results;
        }

        private static Dictionary<Guid, int> BuildPersonDepths(List<Person> persons, List<ParentChild> relations)
        {
            var depths = new Dictionary<Guid, int>();
            var childrenByParent = relations
                .GroupBy(r => r.ParentId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.ChildId).ToList());

            var rootIds = persons.Select(p => p.Id).Except(relations.Select(r => r.ChildId)).ToList();
            var queue = new Queue<Guid>();
            foreach (var rootId in rootIds)
            {
                depths[rootId] = 0;
                queue.Enqueue(rootId);
            }

            while (queue.Count > 0)
            {
                var parentId = queue.Dequeue();
                if (!childrenByParent.TryGetValue(parentId, out var childIds))
                {
                    continue;
                }

                var parentDepth = depths[parentId];
                foreach (var childId in childIds)
                {
                    var nextDepth = parentDepth + 1;
                    if (!depths.TryGetValue(childId, out var existingDepth) || nextDepth < existingDepth)
                    {
                        depths[childId] = nextDepth;
                        queue.Enqueue(childId);
                    }
                }
            }

            return depths;
        }

        private static bool IsMale(string? gender)
            => !string.IsNullOrWhiteSpace(gender) && (
                string.Equals(gender, "男", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(gender, "m", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(gender, "male", StringComparison.OrdinalIgnoreCase) ||
                gender.StartsWith("男", StringComparison.OrdinalIgnoreCase));

        private async Task<Dictionary<Guid, List<(Guid Neighbor, string Kind)>>> BuildKinshipAdjacency(Guid genealogyId, CancellationToken cancellationToken)
        {
            var adj = new Dictionary<Guid, List<(Guid, string)>>();

            void AddEdge(Guid a, Guid b, string kind)
            {
                if (!adj.TryGetValue(a, out var la))
                {
                    la = new List<(Guid, string)>();
                    adj[a] = la;
                }

                la.Add((b, kind));

                if (!adj.TryGetValue(b, out var lb))
                {
                    lb = new List<(Guid, string)>();
                    adj[b] = lb;
                }

                lb.Add((a, kind));
            }

            var pc = await _db.ParentChildren.AsNoTracking()
                .Where(r => r.GenealogyId == genealogyId)
                .ToListAsync(cancellationToken);
            foreach (var r in pc)
            {
                AddEdge(r.ParentId, r.ChildId, "父母—子女");
            }

            var marriages = await _db.Marriages.AsNoTracking()
                .Where(m => m.GenealogyId == genealogyId)
                .ToListAsync(cancellationToken);
            foreach (var m in marriages)
            {
                AddEdge(m.SpouseAId, m.SpouseBId, "配偶");
            }

            return adj;
        }

        private static List<Guid>? BreadthFirstKinshipPath(
            Guid from,
            Guid to,
            IReadOnlyDictionary<Guid, List<(Guid Neighbor, string Kind)>> adj,
            IReadOnlyDictionary<Guid, Person> persons,
            HashSet<(Guid Parent, Guid Child)> parentSet)
        {
            if (!adj.ContainsKey(from) || !adj.ContainsKey(to))
            {
                // 孤立节点仍可能与另一孤立节点不相连；BFS 需能启动
                if (from == to)
                {
                    return new List<Guid> { from };
                }

                return null;
            }

            var queue = new Queue<Guid>();
            var prev = new Dictionary<Guid, Guid>();
            var visited = new HashSet<Guid> { from };
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                var u = queue.Dequeue();
                if (u == to)
                {
                    var path = new List<Guid>();
                    var cur = to;
                    while (true)
                    {
                        path.Add(cur);
                        if (cur == from) break;
                        cur = prev[cur];
                    }

                    path.Reverse();
                    return path;
                }

                if (!adj.TryGetValue(u, out var neighbors))
                {
                    continue;
                }

                // 为了在多条等长路径中优先选择“父亲”，对邻居按优先级排序：
                // 1. 父亲（邻居是 u 的父且性别为男）
                // 2. 母亲
                // 3. 配偶
                // 4. 子（男）
                // 5. 子（女）
                var ordered = neighbors
                    .Select(nk => new { Id = nk.Neighbor, Kind = nk.Kind })
                    .DistinctBy(x => x.Id)
                    .OrderBy(x =>
                    {
                        if (x.Kind == "父母—子女")
                        {
                            if (parentSet.Contains((x.Id, u)))
                            {
                                // x.Id 是 u 的父母之一
                                var g = persons.TryGetValue(x.Id, out var p) ? p.Gender : null;
                                if (string.Equals(g, "男", StringComparison.OrdinalIgnoreCase)) return 0;
                                return 1;
                            }
                            if (parentSet.Contains((u, x.Id)))
                            {
                                var g = persons.TryGetValue(x.Id, out var p) ? p.Gender : null;
                                if (string.Equals(g, "男", StringComparison.OrdinalIgnoreCase)) return 3;
                                return 4;
                            }
                            return 5;
                        }

                        if (x.Kind == "配偶") return 2;
                        return 6;
                    })
                    .Select(x => x.Id)
                    .ToList();

                foreach (var v in ordered)
                {
                    if (visited.Add(v))
                    {
                        prev[v] = u;
                        queue.Enqueue(v);
                    }
                }
            }

            return null;
        }

        private static string DescribeEdge(
            Guid from,
            Guid to,
            IReadOnlyDictionary<Guid, List<(Guid Neighbor, string Kind)>> adj)
        {
            if (adj.TryGetValue(from, out var list))
            {
                foreach (var (n, kind) in list)
                {
                    if (n == to)
                    {
                        return kind;
                    }
                }
            }

            return "—";
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

        /// <summary>自某人向上：每个节点的子节点列表存放其父母（递归）。</summary>
        private static TreeNodeDto BuildAncestorTree(
            Guid personId,
            IReadOnlyDictionary<Guid, Person> personLookup,
            IReadOnlyDictionary<Guid, List<Guid>> parentsByChild,
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

            var parents = new List<TreeNodeDto>();
            if (parentsByChild.TryGetValue(personId, out var parentIds))
            {
                foreach (var pid in parentIds)
                {
                    if (personLookup.ContainsKey(pid))
                    {
                        parents.Add(BuildAncestorTree(pid, personLookup, parentsByChild, visiting));
                    }
                }
            }

            visiting.Remove(personId);
            return new TreeNodeDto(person.Id, person.GivenName, person.Gender, person.BirthYear, person.Bio, parents);
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

    /// <summary>修改协作者角色请求体。</summary>
    public record UpdateCollaboratorRoleDto(string Role);

    /// <summary>树节点 DTO，与 Blazor 组件 <c>TreeNodeView</c> 绑定；子节点递归同结构。</summary>
    public record TreeNodeDto(Guid Id, string Name, string? Gender, int? BirthYear, string? Bio, List<TreeNodeDto> Children);

    /// <summary>亲缘路径查询结果。</summary>
    public record KinshipPathResponse(bool Connected, IReadOnlyList<KinshipStepDto>? Path);

    /// <summary>路径上一步：成员与相对上一点的边类型说明。</summary>
    public record KinshipStepDto(Guid PersonId, string Name, string RelationFromPrevious);

    /// <summary>成员配偶与子女查询结果。</summary>
    public record PersonRelationDto(Guid PersonId, string GivenName, string? Gender, int? BirthYear, string RelationKind);

    /// <summary>每代平均寿命统计。</summary>
    public record GenerationLifespanDto(int Generation, double AvgLifespanYears, int MemberCount);

    /// <summary>出生年份早于本代平均值的成员。</summary>
    public record EarlyBirthPersonDto(Guid PersonId, string GivenName, string? Gender, int? BirthYear, int Generation, double AvgBirthYear);
}
