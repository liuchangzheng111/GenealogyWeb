using GenealogyWeb.Data;

namespace GenealogyWeb.Services;

public interface IGenerationMaintenanceService
{
    Task RecalculateGenealogyAsync(Guid genealogyId, CancellationToken cancellationToken = default);
}

public sealed class GenerationMaintenanceService : IGenerationMaintenanceService
{
    private readonly ApplicationDbContext _db;

    public GenerationMaintenanceService(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task RecalculateGenealogyAsync(Guid genealogyId, CancellationToken cancellationToken = default)
        => GenerationAssigner.RecalculateGenealogyAsync(_db, genealogyId, cancellationToken);
}
