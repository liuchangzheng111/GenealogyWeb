-- =============================================================================
-- 清空族谱业务域（保留 Users 等账号表），便于重新 LOAD DATA 后库内「仅有一轮」CSV 数据。
-- 删除顺序满足 Restrict 外键：Marriages -> ParentChildren -> Persons -> GenealogyUsers -> Genealogies
-- 建议：先执行本脚本，再执行 load_data_bulk_example.sql，最后再启动 Web（避免 EnsureSeeded 在空库时插入演示「张氏族谱」）。
-- =============================================================================

USE genealogy;

DELETE FROM Marriages;
DELETE FROM ParentChildren;
DELETE FROM Persons;
DELETE FROM GenealogyUsers;
DELETE FROM Genealogies;
