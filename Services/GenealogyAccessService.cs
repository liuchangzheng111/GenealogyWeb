using Microsoft.EntityFrameworkCore;
using GenealogyApp.Data;

namespace GenealogyApp.Services;

/// <inheritdoc cref="IGenealogyAccessService"/>
public sealed class GenealogyAccessService : IGenealogyAccessService
{
    private readonly ApplicationDbContext _db;

    public GenealogyAccessService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetAccessibleGenealogyIdsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // 创建者：即使尚未写入 GenealogyUsers（历史数据），仍应能访问；正常路径下创建接口会同时写两行。
        var owned = await _db.Genealogies.AsNoTracking()
            .Where(g => g.CreatedByUserId == userId)
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);

        var shared = await _db.GenealogyUsers.AsNoTracking()
            .Where(gu => gu.UserId == userId)
            .Select(gu => gu.GenealogyId)
            .ToListAsync(cancellationToken);

        return owned.Concat(shared).Distinct().ToList();
    }

    /// <inheritdoc />
    public async Task<bool> CanAccessGenealogyAsync(Guid userId, Guid genealogyId, CancellationToken cancellationToken = default)
    {
        if (await _db.Genealogies.AsNoTracking().AnyAsync(
                g => g.Id == genealogyId && g.CreatedByUserId == userId,
                cancellationToken))
        {
            return true;
        }

        return await _db.GenealogyUsers.AsNoTracking().AnyAsync(
            gu => gu.GenealogyId == genealogyId && gu.UserId == userId,
            cancellationToken);
    }
}
