# 批量模拟数据（课程：数据工程 / 导入导出）

## 目标量纲

- **10 本族谱**（CSV：`genealogies.csv`），第二本标题为「张氏·再婚同名…」：**约 18 名手工场景人物**（`realistic_scenario.py`：离异/再婚、两名「王伟」、半同胞、随母再婚旁支、出生年缺失、性别空等）+ **剩余人数为链式/叶填充**（自「张建民」向下挂接；每名子女有父+母边及初婚/低概率离异，见 `demography.py`）；**`marriages.csv` 含全部 10 本谱**（场景谱外为批量初婚备注）。
- **其余 8 本支谱**：`name_pools.py` 按出生年分名池 + 序号；主干为父链（全男）+ 每位配偶（母系姓用 `demography.MAIDEN_SURNAMES` 轮替）；叶节点与 hub 配偶双亲齐全。
- **大谱**：李氏，**≥50,000** 名核心成员 + **30 名主干配偶**（与 31 代父链一一对应；侧枝与对应子代**共母**），侧枝仍随机挂到父链节点；`marriages.csv` 含主干初婚及少量离异年。
- **全库 `Persons` 行数**：核心成员 **100,000** + 各谱补全母亲/配偶约 **+670**（以生成脚本控制台与 `manifest.txt` 的 `persons_total` 为准）。
- 每条 `ParentChildren` 满足应用迁移中的 **CHECK + 触发器**（父母出生年早于子女、同族谱）；`SpouseAId`/`SpouseBId` 在批量数据中约定为 **男在前、女在后**。

## 依赖

仅 **Python 3.10+** 标准库（`csv`、`uuid`、`argparse`），无需 `pip install`。

## 步骤

1. 在 MySQL 中确认已有 **`Users`** 记录（例如种子账号 `demo@genealogy.local`），并查询其 `Id`：

   ```sql
   SELECT Id, Email FROM Users WHERE Email = 'demo@genealogy.local';
   ```

2. 在项目根目录执行（将 `OWNER_GUID` 换成上一步的 `Id`；Windows 若无 `python` 命令可用 `py -3`）：

   ```bash
   py -3 tools/datagen/generate_bulk_data.py --owner-id OWNER_GUID --output-dir tools/datagen/out
   ```

3. **若需库内仅有本轮 CSV（无历史叠加）**：先执行 `sql/clear_genealogy_domain.sql`（清空 Marriages / ParentChildren / Persons / GenealogyUsers / Genealogies，**不删 Users**），再执行下一步。
4. 按 `sql/load_data_bulk_example.sql` 中的顺序执行 `LOAD DATA`（含 **`marriages.csv`**，须在 `ParentChildren` 之后导入）。

5. **分支导出**：按需修改 `sql/export_branch_subtree.sql` 中的 `@genealogy_id`、`@root_person_id` 后执行；若 `INTO OUTFILE` 被 `secure_file_priv` 限制，可改为只 `SELECT` 结果后在客户端导出。

## 备份（课程提交）

```bash
mysqldump -u root -p --databases genealogy --result-file=genealogy_backup.sql
```

## 与 Blazor 应用的关系

- 导入数据后重启 `GenealogyWeb`，Dashboard / 族谱列表将包含新谱（`CreatedByUserId` 为传入的 owner）。若先执行了 `clear_genealogy_domain.sql` 且**尚未**导入 CSV 就启动应用，空库会触发种子里的演示「张氏族谱」；请**先导入再启动**，或导入后再删该谱。
- 若与本地已有「张氏族谱」等冲突，可先备份库再在空库上导入，或使用新库名连接字符串。
