-- =============================================================================
-- 课程「物理优化」：四代查询 —— 给定一名祖先，列出其「曾孙」一代（距祖先 4 条子边）
-- 配合 EXPLAIN 对比索引：建议存在 IX_ParentChildren_GenealogyId_ParentId（见迁移 AddParentChildGenealogyParentIndex）
-- =============================================================================

SET @genealogy_id = '00000000-0000-0000-0000-000000000000' COLLATE utf8mb4_bin;
SET @ancestor_id = '00000000-0000-0000-0000-000000000000' COLLATE utf8mb4_bin;

WITH RECURSIVE down AS (
    SELECT pc.ChildId AS id, 1 AS gen
    FROM ParentChildren pc
    WHERE pc.GenealogyId = @genealogy_id AND pc.ParentId = @ancestor_id
    UNION ALL
    SELECT pc.ChildId, d.gen + 1
    FROM down d
    JOIN ParentChildren pc
      ON pc.GenealogyId = @genealogy_id AND pc.ParentId = d.id
    WHERE d.gen < 4
)
SELECT p.Id, p.GivenName, p.BirthYear
FROM down d
JOIN Persons p ON p.Id = d.id AND p.GenealogyId = @genealogy_id
WHERE d.gen = 4;

-- 执行计划（导入大数据后对比）：先 DROP INDEX IX_ParentChildren_GenealogyId_ParentId 再 EXPLAIN 一次（仅实验环境）
-- EXPLAIN FORMAT=TREE
-- WITH RECURSIVE down AS ( ... 同上 CTE ... )
-- SELECT p.Id, p.GivenName, p.BirthYear FROM down d JOIN Persons p ... ;
