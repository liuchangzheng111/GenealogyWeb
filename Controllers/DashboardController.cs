using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GenealogyApp.Data;

namespace GenealogyApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public DashboardController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            var total = await _db.Persons.CountAsync();
            var male = await _db.Persons.CountAsync(p => p.Gender == "男" || p.Gender == "M");
            var female = await _db.Persons.CountAsync(p => p.Gender == "女" || p.Gender == "F");

            var maleRatio = total == 0 ? 0 : Math.Round(male * 100.0 / total, 1);
            var femaleRatio = total == 0 ? 0 : Math.Round(female * 100.0 / total, 1);

            return Ok(new
            {
                genealogyCount = await _db.Genealogies.CountAsync(),
                totalMembers = total,
                maleCount = male,
                femaleCount = female,
                maleRatio,
                femaleRatio
            });
        }
    }
}
