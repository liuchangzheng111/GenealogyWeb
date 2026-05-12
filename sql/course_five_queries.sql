-- =============================================================================
-- 课程「SQL 核心功能」参考：每条需求一条 SQL（请替换 @member_id、@genealogy_id 等变量后执行）
-- 表名与列名与 GenealogyWeb EF 迁移一致：Persons, ParentChildren, Marriages, Genealogies
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1) 基本查询：给定成员 Id，查询其配偶及所有子女（单条 SQL，用 UNION ALL 合并两类结果）
-- -----------------------------------------------------------------------------
SET @member_id   = '00000000-0000-0000-0000-000000000001' COLLATE utf8mb4_bin;
SET @genealogy_id = '00000000-0000-0000-0000-000000000002' COLLATE utf8mb4_bin;

(
  SELECT 'spouse' AS relation_kind, p.Id AS person_id, p.GivenName, p.Gender, p.BirthYear
  FROM Marriages m
  JOIN Persons p ON p.GenealogyId = @genealogy_id
    AND p.Id <> @member_id
    AND m.GenealogyId = @genealogy_id
    AND ((m.SpouseAId = @member_id AND m.SpouseBId = p.Id) OR (m.SpouseBId = @member_id AND m.SpouseAId = p.Id))
)
UNION ALL
(
  SELECT 'child', c.Id, c.GivenName, c.Gender, c.BirthYear
  FROM ParentChildren pc
  JOIN Persons c ON c.Id = pc.ChildId AND c.GenealogyId = @genealogy_id
  WHERE pc.GenealogyId = @genealogy_id AND pc.ParentId = @member_id
);


-- -----------------------------------------------------------------------------
-- 2) 递归查询：输入成员 A 的 Id，输出其向上追溯的所有历代祖先（Recursive CTE）
-- -----------------------------------------------------------------------------
SET @person_a = '00000000-0000-0000-0000-000000000001' COLLATE utf8mb4_bin;
SET @genealogy_id = '00000000-0000-0000-0000-000000000002' COLLATE utf8mb4_bin;

WITH RECURSIVE ancestors AS (
    SELECT pc.ParentId AS ancestor_id, pc.ChildId AS via_child, 1 AS generation
    FROM ParentChildren pc
    WHERE pc.GenealogyId = @genealogy_id AND pc.ChildId = @person_a
    UNION ALL
    SELECT pc.ParentId, pc.ChildId, a.generation + 1
    FROM ParentChildren pc
    INNER JOIN ancestors a ON pc.ChildId = a.ancestor_id
    WHERE pc.GenealogyId = @genealogy_id
)
SELECT DISTINCT an.ancestor_id, p.GivenName, p.BirthYear, an.generation
FROM ancestors an
JOIN Persons p ON p.Id = an.ancestor_id AND p.GenealogyId = @genealogy_id
ORDER BY an.generation, p.GivenName;


-- -----------------------------------------------------------------------------
-- 3) 统计分析：某家族中「平均寿命」最长的一代人（辈分）
--     说明：辈分用「自根最短代数」近似——从「不作为子女出现的成员」出发沿子边 BFS 得到 depth，再算寿命。
-- -----------------------------------------------------------------------------
SET @genealogy_id = '00000000-0000-0000-0000-000000000002' COLLATE utf8mb4_bin;

WITH RECURSIVE roots AS (
    SELECT p.Id
    FROM Persons p
    WHERE p.GenealogyId = @genealogy_id
      AND NOT EXISTS (
          SELECT 1 FROM ParentChildren pc
          WHERE pc.GenealogyId = @genealogy_id AND pc.ChildId = p.Id
      )
),
walk AS (
    SELECT r.Id, 0 AS depth FROM roots r
    UNION ALL
    SELECT c.Id, w.depth + 1
    FROM walk w
    JOIN ParentChildren pc ON pc.ParentId = w.Id AND pc.GenealogyId = @genealogy_id
    JOIN Persons c ON c.Id = pc.ChildId AND c.GenealogyId = @genealogy_id
),
person_depth AS (
    SELECT Id, MIN(depth) AS depth
    FROM walk
    GROUP BY Id
),
lifespan AS (
    SELECT d.depth AS generation,
           AVG(p.DeathYear - p.BirthYear) AS avg_lifespan_years
    FROM person_depth d
    JOIN Persons p ON p.Id = d.Id AND p.GenealogyId = @genealogy_id
    WHERE p.BirthYear IS NOT NULL AND p.DeathYear IS NOT NULL AND p.DeathYear >= p.BirthYear
    GROUP BY d.depth
)
SELECT generation, avg_lifespan_years
FROM lifespan
ORDER BY avg_lifespan_years DESC
LIMIT 1;


-- -----------------------------------------------------------------------------
-- 4) 年龄超过 50 岁（按当前日期与出生年 1 月 1 日估算）且无配偶的男性成员
-- -----------------------------------------------------------------------------
SET @genealogy_id = '00000000-0000-0000-0000-000000000002' COLLATE utf8mb4_bin;

SELECT p.Id, p.GivenName, p.BirthYear
FROM Persons p
WHERE p.GenealogyId = @genealogy_id
  AND (p.Gender IN ('男', 'M', 'male', 'Male') OR p.Gender LIKE '男%')
  AND p.BirthYear IS NOT NULL
  AND TIMESTAMPDIFF(YEAR, STR_TO_DATE(CONCAT(p.BirthYear, '-01-01'), '%Y-%m-%d'), CURDATE()) > 50
  AND NOT EXISTS (
      SELECT 1 FROM Marriages m
      WHERE m.GenealogyId = @genealogy_id
        AND (m.SpouseAId = p.Id OR m.SpouseBId = p.Id)
  );


-- -----------------------------------------------------------------------------
-- 5) 出生年份早于「该辈分（代）平均出生年份」的所有成员（辈分定义同查询 3 的 depth）
-- -----------------------------------------------------------------------------
SET @genealogy_id = '00000000-0000-0000-0000-000000000002' COLLATE utf8mb4_bin;

WITH RECURSIVE roots AS (
    SELECT p.Id FROM Persons p
    WHERE p.GenealogyId = @genealogy_id
      AND NOT EXISTS (SELECT 1 FROM ParentChildren pc WHERE pc.GenealogyId = @genealogy_id AND pc.ChildId = p.Id)
),
walk AS (
    SELECT r.Id, 0 AS depth FROM roots r
    UNION ALL
    SELECT c.Id, w.depth + 1
    FROM walk w
    JOIN ParentChildren pc ON pc.ParentId = w.Id AND pc.GenealogyId = @genealogy_id
    JOIN Persons c ON c.Id = pc.ChildId AND c.GenealogyId = @genealogy_id
),
person_depth AS (
    SELECT Id, MIN(depth) AS depth FROM walk GROUP BY Id
),
gen_avg AS (
    SELECT d.depth, AVG(p.BirthYear) AS avg_birth_year
    FROM person_depth d
    JOIN Persons p ON p.Id = d.Id AND p.GenealogyId = @genealogy_id
    WHERE p.BirthYear IS NOT NULL
    GROUP BY d.depth
)
SELECT p.Id, p.GivenName, p.BirthYear, pd.depth AS generation, ga.avg_birth_year
FROM Persons p
JOIN person_depth pd ON pd.Id = p.Id
JOIN gen_avg ga ON ga.depth = pd.depth
WHERE p.GenealogyId = @genealogy_id
  AND p.BirthYear IS NOT NULL
  AND p.BirthYear < ga.avg_birth_year;
