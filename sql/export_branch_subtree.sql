-- =============================================================================
-- 导出「某成员在指定族谱内的全部后代」为扁平结果集，便于 mysqldump 或客户端另存 CSV
-- 将 @genealogy_id、@root_person_id 替换为 manifest 或界面中的 Guid。
-- 若需服务器端文件：在 secure_file_priv 允许目录下取消注释 INTO OUTFILE。
-- =============================================================================

SET @genealogy_id = '00000000-0000-0000-0000-000000000000' COLLATE utf8mb4_bin;
SET @root_person_id = '00000000-0000-0000-0000-000000000000' COLLATE utf8mb4_bin;

WITH RECURSIVE sub AS (
    SELECT p.Id, p.GivenName, p.BirthYear, p.Gender, 0 AS depth
    FROM Persons p
    WHERE p.GenealogyId = @genealogy_id AND p.Id = @root_person_id
    UNION ALL
    SELECT c.Id, c.GivenName, c.BirthYear, c.Gender, sub.depth + 1
    FROM sub
    JOIN ParentChildren pc
      ON pc.GenealogyId = @genealogy_id AND pc.ParentId = sub.Id
    JOIN Persons c ON c.Id = pc.ChildId AND c.GenealogyId = @genealogy_id
)
SELECT Id, GivenName, BirthYear, Gender, depth
FROM sub
ORDER BY depth, GivenName;

-- 示例：导出到服务器目录（路径需符合 secure_file_priv）
-- SELECT Id, GivenName, BirthYear, Gender, depth FROM (...) AS t
-- INTO OUTFILE 'C:/ProgramData/MySQL/MySQL Server 8.0/Uploads/branch_export.csv'
-- FIELDS TERMINATED BY ',' ENCLOSED BY '"'
-- LINES TERMINATED BY '\n';
