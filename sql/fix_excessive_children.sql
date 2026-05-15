-- =============================================================================
-- 修复子代过多问题：除李家外，其他家族父母最多保留10个孩子
-- 使用窗口函数按ChildId排序保留前10个
-- =============================================================================

USE genealogy;

-- 删除不需要的记录：对于有超过10个孩子的父母，保留前10个（按ChildId排序）
DELETE pc FROM ParentChildren pc
JOIN Persons p ON pc.ParentId = p.Id
JOIN Genealogies g ON p.GenealogyId = g.Id
WHERE g.Surname != '李'
AND pc.Id NOT IN (
    SELECT Id FROM (
        SELECT pc2.Id, ROW_NUMBER() OVER (PARTITION BY pc2.ParentId ORDER BY pc2.ChildId) as rn
        FROM ParentChildren pc2
        JOIN Persons p2 ON pc2.ParentId = p2.Id
        JOIN Genealogies g2 ON p2.GenealogyId = g2.Id
        WHERE g2.Surname != '李'
    ) ranked
    WHERE rn <= 10
);