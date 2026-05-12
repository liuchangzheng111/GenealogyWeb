-- MySQL 8+：递归 CTE 从某成员向上列出所有祖先（ParentChildren：ParentId -> ChildId）
-- 课程报告可替换 @person_id、@genealogy_id 后整段执行。
-- 若 SET 变量与库中排序规则不一致，可将 COLLATE 改为与表一致（常见 utf8mb4_unicode_ci）。
-- 说明：每一行表示「ancestor_id 是某一代祖先」，generation 为距起点的代数（1=父母，2=祖父母…）。

SET @person_id    = '00000000-0000-0000-0000-000000000001' COLLATE utf8mb4_bin;
SET @genealogy_id = '00000000-0000-0000-0000-000000000002' COLLATE utf8mb4_bin;

WITH RECURSIVE ancestors AS (
    SELECT
        pc.ParentId AS ancestor_id,
        pc.ChildId  AS child_id,
        1 AS generation
    FROM ParentChildren pc
    WHERE pc.GenealogyId = @genealogy_id
      AND pc.ChildId = @person_id

    UNION ALL

    SELECT
        pc.ParentId,
        pc.ChildId,
        a.generation + 1
    FROM ParentChildren pc
    INNER JOIN ancestors a ON pc.ChildId = a.ancestor_id
    WHERE pc.GenealogyId = @genealogy_id
)
SELECT DISTINCT ancestor_id, MIN(generation) AS min_generation
FROM ancestors
GROUP BY ancestor_id
ORDER BY min_generation, ancestor_id;
