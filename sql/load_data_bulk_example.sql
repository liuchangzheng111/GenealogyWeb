-- =============================================================================
-- MySQL 8：使用 LOAD DATA LOCAL INFILE 批量导入 tools/datagen/out/*.csv
-- 前置：mysql 客户端加 --local-infile=1；服务端 local_infile=ON；CSV 路径换成本机绝对路径。
-- 顺序：Genealogies -> GenealogyUsers -> Persons -> ParentChildren -> Marriages（满足外键）
-- 若主键/唯一键冲突：先删除对应族谱数据或在空库执行；需「整库只保留本轮 CSV」时先执行 sql/clear_genealogy_domain.sql 再执行本脚本。
-- Windows 可用 mysql --local-infile=1 -u... -p 执行本脚本。
-- =============================================================================

USE genealogy;

-- 1) 族谱（CompiledAt 空 -> NULL）
LOAD DATA LOCAL INFILE 'D:/GenealogyWeb/tools/datagen/out/genealogies.csv'
INTO TABLE Genealogies
CHARACTER SET utf8mb4
FIELDS TERMINATED BY ',' OPTIONALLY ENCLOSED BY '"'
LINES TERMINATED BY '\n'
IGNORE 1 LINES
(@Id, @Title, @Surname, @CompiledAt, @CreatedByUserId, @CreatedAt)
SET Id = @Id,
    Title = @Title,
    Surname = @Surname,
    CompiledAt = NULLIF(@CompiledAt, ''),
    CreatedByUserId = @CreatedByUserId,
    CreatedAt = @CreatedAt;

-- 2) 协作（Owner），无 InvitedByUserId
LOAD DATA LOCAL INFILE 'D:/GenealogyWeb/tools/datagen/out/genealogy_users.csv'
INTO TABLE GenealogyUsers
CHARACTER SET utf8mb4
FIELDS TERMINATED BY ',' OPTIONALLY ENCLOSED BY '"'
LINES TERMINATED BY '\n'
IGNORE 1 LINES
(@GenealogyId, @UserId, @Role, @InvitedByUserId, @InvitedAt)
SET GenealogyId = @GenealogyId,
    UserId = @UserId,
    Role = @Role,
    InvitedByUserId = NULLIF(@InvitedByUserId, ''),
    InvitedAt = @InvitedAt;

-- 3) 成员
LOAD DATA LOCAL INFILE 'D:/GenealogyWeb/tools/datagen/out/persons.csv'
INTO TABLE Persons
CHARACTER SET utf8mb4
FIELDS TERMINATED BY ',' OPTIONALLY ENCLOSED BY '"'
LINES TERMINATED BY '\n'
IGNORE 1 LINES
(@Id, @GenealogyId, @GivenName, @Gender, @BirthYear, @DeathYear, @Bio, @CreatedAt)
SET Id = @Id,
    GenealogyId = @GenealogyId,
    GivenName = @GivenName,
    Gender = NULLIF(@Gender, ''),
    BirthYear = NULLIF(@BirthYear, ''),
    DeathYear = NULLIF(@DeathYear, ''),
    Bio = NULLIF(@Bio, ''),
    CreatedAt = @CreatedAt;

-- 4) 亲子边（Id 自增，不导入）
LOAD DATA LOCAL INFILE 'D:/GenealogyWeb/tools/datagen/out/parent_children.csv'
INTO TABLE ParentChildren
CHARACTER SET utf8mb4
FIELDS TERMINATED BY ',' OPTIONALLY ENCLOSED BY '"'
LINES TERMINATED BY '\n'
IGNORE 1 LINES
(@GenealogyId, @ParentId, @ChildId, @RelationshipType)
SET GenealogyId = @GenealogyId,
    ParentId = @ParentId,
    ChildId = @ChildId,
    RelationshipType = NULLIF(@RelationshipType, '');

-- 5) 婚姻事实（Id 自增，不导入）
LOAD DATA LOCAL INFILE 'D:/GenealogyWeb/tools/datagen/out/marriages.csv'
INTO TABLE Marriages
CHARACTER SET utf8mb4
FIELDS TERMINATED BY ',' OPTIONALLY ENCLOSED BY '"'
LINES TERMINATED BY '\n'
IGNORE 1 LINES
(@GenealogyId, @SpouseAId, @SpouseBId, @MarriedAtYear, @DivorcedAtYear, @Note)
SET GenealogyId = @GenealogyId,
    SpouseAId = @SpouseAId,
    SpouseBId = @SpouseBId,
    MarriedAtYear = NULLIF(@MarriedAtYear, ''),
    DivorcedAtYear = NULLIF(@DivorcedAtYear, ''),
    Note = NULLIF(@Note, '');

-- 导入后建议：ANALYZE TABLE Persons, ParentChildren, Marriages, Genealogies, GenealogyUsers;
