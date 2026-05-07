namespace GenealogyApp.Services;

/// <summary>
/// 族谱级访问控制：判断某用户是否可读写某族谱数据。
/// 规则：族谱创建者（CreatedByUserId）为该用户，或 GenealogyUsers 表中存在该用户。
/// 新增「仅受邀可见」场景时，在此集中调整查询，避免各控制器复制条件。
/// </summary>
public interface IGenealogyAccessService
{
    /// <summary>返回当前用户可访问的族谱 Id 列表（已去重）。</summary>
    Task<IReadOnlyList<Guid>> GetAccessibleGenealogyIdsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>是否可访问指定族谱。</summary>
    Task<bool> CanAccessGenealogyAsync(Guid userId, Guid genealogyId, CancellationToken cancellationToken = default);
}
