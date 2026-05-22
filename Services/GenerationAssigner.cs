using GenealogyWeb.Data;
using GenealogyWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace GenealogyWeb.Services;

/// <summary>
/// 辈分（代）：无父母者为 0 代；父母为 x 代则子女为 x+1。同子女的双亲对齐为同一辈（不考虑跨辈婚姻）。
/// </summary>
public static class GenerationAssigner
{
    public const int MaxPropagationIterations = 256;

    /// <summary>根据父母辈分计算子女辈分；无父母则 0。</summary>
    public static int DeriveChildGeneration(int? fatherGeneration, int? motherGeneration)
    {
        if (!fatherGeneration.HasValue && !motherGeneration.HasValue)
        {
            return 0;
        }

        var maxParent = Math.Max(fatherGeneration ?? -1, motherGeneration ?? -1);
        return maxParent + 1;
    }

    /// <summary>写入新成员辈分，并将双亲调整为同一辈（取较高辈，避免母亲被误留在 0 代）。</summary>
    public static void ApplyChildGeneration(Person child, Person? father, Person? mother)
    {
        if (father != null && mother != null)
        {
            var align = Math.Max(father.Generation, mother.Generation);
            father.Generation = align;
            mother.Generation = align;
        }

        child.Generation = DeriveChildGeneration(father?.Generation, mother?.Generation);
    }

    /// <summary>重算整本族谱的 <see cref="Person.Generation"/>（修复历史数据或导入后调用）。</summary>
    public static async Task RecalculateGenealogyAsync(
        ApplicationDbContext db,
        Guid genealogyId,
        CancellationToken cancellationToken = default)
    {
        var persons = await db.Persons.Where(p => p.GenealogyId == genealogyId).ToListAsync(cancellationToken);
        if (persons.Count == 0)
        {
            return;
        }

        var edges = await db.ParentChildren
            .Where(pc => pc.GenealogyId == genealogyId)
            .ToListAsync(cancellationToken);

        var byId = persons.ToDictionary(p => p.Id);
        var childIds = edges.Select(e => e.ChildId).ToHashSet();

        foreach (var p in persons)
        {
            p.Generation = childIds.Contains(p.Id) ? -1 : 0;
        }

        for (var iter = 0; iter < MaxPropagationIterations; iter++)
        {
            var changed = false;
            foreach (var e in edges)
            {
                if (!byId.TryGetValue(e.ParentId, out var parent) || parent.Generation < 0)
                {
                    continue;
                }

                if (!byId.TryGetValue(e.ChildId, out var child))
                {
                    continue;
                }

                var next = parent.Generation + 1;
                if (child.Generation < next)
                {
                    child.Generation = next;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        foreach (var p in persons.Where(p => p.Generation < 0))
        {
            p.Generation = 0;
        }

        AlignParentsOfSameChild(edges, byId);

        for (var iter = 0; iter < MaxPropagationIterations; iter++)
        {
            var changed = false;
            foreach (var e in edges)
            {
                if (!byId.TryGetValue(e.ParentId, out var parent))
                {
                    continue;
                }

                if (!byId.TryGetValue(e.ChildId, out var child))
                {
                    continue;
                }

                var next = parent.Generation + 1;
                if (child.Generation < next)
                {
                    child.Generation = next;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void AlignParentsOfSameChild(
        List<ParentChild> edges,
        Dictionary<Guid, Person> byId)
    {
        foreach (var group in edges.GroupBy(e => e.ChildId))
        {
            var parentIds = group.Select(e => e.ParentId).Distinct().ToList();
            if (parentIds.Count <= 1)
            {
                continue;
            }

            var maxGen = parentIds.Max(pid => byId.TryGetValue(pid, out var p) ? p.Generation : 0);
            foreach (var pid in parentIds)
            {
                if (byId.TryGetValue(pid, out var parent))
                {
                    parent.Generation = maxGen;
                }
            }
        }
    }
}
