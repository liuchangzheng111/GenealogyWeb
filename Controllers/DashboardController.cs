using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GenealogyWeb.Data;
using GenealogyWeb.Services;

namespace GenealogyWeb.Controllers
{
    /// <summary>
    /// 首页 Dashboard 用聚合统计：仅统计当前用户<strong>有权访问的族谱</strong>内的成员。
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IGenealogyAccessService _access;

        public DashboardController(ApplicationDbContext db, IGenealogyAccessService access)
        {
            _db = db;
            _access = access;
        }

        /// <summary>
        /// 返回族谱数量、成员总数、男女数量及占比（男/女含简写 M/F 与中文「男」「女」）。
        /// </summary>
        /// <remarks><c>genealogyCount</c> 为可访问族谱 Id 去重个数，与列表接口范围一致。</remarks>
        [HttpGet("summary")]
        public async Task<IActionResult> Summary(CancellationToken cancellationToken)
        {
            var userId = User.GetUserIdOrNull();
            if (userId is null) return Unauthorized();

            var allowed = await _access.GetAccessibleGenealogyIdsAsync(userId.Value, cancellationToken);
            if (allowed.Count == 0)
            {
                return Ok(new
                {
                    genealogyCount = 0,
                    totalMembers = 0,
                    maleCount = 0,
                    femaleCount = 0,
                    maleRatio = 0.0,
                    femaleRatio = 0.0
                });
            }

            var personsQuery = _db.Persons.Where(p => allowed.Contains(p.GenealogyId));
            var total = await personsQuery.CountAsync(cancellationToken);
            var male = await personsQuery.CountAsync(
                p => p.Gender == "男" || p.Gender == "M",
                cancellationToken);
            var female = await personsQuery.CountAsync(
                p => p.Gender == "女" || p.Gender == "F",
                cancellationToken);

            var maleRatio = total == 0 ? 0 : Math.Round(male * 100.0 / total, 1);
            var femaleRatio = total == 0 ? 0 : Math.Round(female * 100.0 / total, 1);

            return Ok(new
            {
                genealogyCount = allowed.Count,
                totalMembers = total,
                maleCount = male,
                femaleCount = female,
                maleRatio,
                femaleRatio
            });
        }
    }
}
