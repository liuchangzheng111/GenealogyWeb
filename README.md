# GenealogyWeb

一个用于族谱管理的 ASP.NET Core Blazor Server 原型项目，支持用户注册/登录、登录后查看当前用户信息、族谱列表展示，以及后续扩展的成员管理、树状预览和亲缘关系查询。

**团队协作**：目录约定、认证与数据隔离说明见 [docs/开发协作说明.md](docs/开发协作说明.md)；业务源码含中文注释与关键 XML 文档（`Migrations/` 为工具生成，勿手改）。

## 已实现功能

- 用户注册与登录
- Cookie 认证
- 登录后显示当前用户信息
- 首页 Dashboard：族谱树预览支持**可选树根**；快捷入口含「关系查询」
- **祖先树** API + 页面：自指定成员向上展示父母链（复用 `TreeNodeView`）
- 成员编辑时可**同步更新父母与配偶**（可选勾选）；族谱设置中 Owner 可**调整协作者角色**或**移除**受邀用户
- 课程参考 SQL：`sql/mysql8_recursive_ancestors_example.sql`（递归 CTE 列祖先）、`sql/course_five_queries.sql`（五条课程查询模板）
- **外键 + CHECK + 触发器**（迁移 `FkCheckAndTriggers`）：`Persons` 生卒年；`ParentChildren` 非自环；`Marriages` 配偶不同、离婚年；亲子边触发器校验**同族谱**与**父母出生年早于子女**（若均已填）
- **批量模拟数据**：`tools/datagen/generate_bulk_data.py` + `tools/datagen/README.md`（10 谱、核心 10 万成员 + 双亲/配偶补全；一谱 ≥5 万且含 30 代父链；**第二本为再婚/同名等场景谱**；**各谱均有** `marriages.csv` 行）；`sql/clear_genealogy_domain.sql`（清空族谱域以便**仅保留一轮**导入）；`sql/load_data_bulk_example.sql`（`LOAD DATA LOCAL INFILE`，含婚姻表）；`sql/export_branch_subtree.sql`（分支导出查询）
- **索引 / EXPLAIN**：迁移 `AddParentChildGenealogyParentIndex`（`ParentChildren(GenealogyId, ParentId)`）；`sql/explain_four_generation_greatgreatgrandchildren.sql`（四代曾孙查询模板）
- 族谱列表 API（仅本人创建或受邀可见）
- 族谱与成员的 **CRUD**（含删除族谱级联清理）
- **按邮箱邀请**已注册用户（`Editor` / `Viewer`，仅 Owner）
- Dashboard 汇总（仅统计有权访问的族谱内成员）
- MySQL 8 数据库（EF Core **Migrations** + 启动时 `Migrate()`）
- Blazor 页面：`/genealogies`、族谱设置、成员管理、**`/genealogies/{id}/relations` 关系查询**（需登录）
- 登录后 **returnUrl** 回跳（仅允许站内以 `/` 开头的路径，防开放重定向）
- 操作成功 **Toast** 轻提示（右下角自动消失）

## 技术栈

- 后端：ASP.NET Core 8
- 前端：Blazor Server
- 数据访问：Entity Framework Core
- 数据库：MySQL 8（通过 Pomelo.EntityFrameworkCore.MySql）

## 本地运行

1. 安装并启动 **MySQL 8**，创建空库（例如 `CREATE DATABASE genealogy CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;`）。
2. 在 `appsettings.json` 中把 `ConnectionStrings:DefaultConnection` 里的 `User`、`Password` 改成你的账号（或使用 [用户机密](https://learn.microsoft.com/aspnet/core/security/app-secrets) 覆盖连接字符串，避免把密码提交到仓库）。
3. 在项目根目录执行：

```bash
dotnet tool restore
dotnet restore
dotnet run
```

启动后访问：

```text
https://localhost:63973/
```

## 页面说明

- `/`：Dashboard；未登录可浏览提示，树预览需登录；支持选择族谱与**树根（可选）**
- `/genealogies/{id}/relations`：祖先树与两人亲缘路径查询
- `/genealogies`：族谱列表与新建
- `/genealogies/{id}`：族谱设置（编辑、邀请、删除）
- `/genealogies/{id}/members`：成员增删改
- `/register`：注册页面
- `/login`：登录页面

## 接口说明

### 认证接口

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/logout`（服务端 HttpClient 调用无法清除浏览器 Cookie，**网页退出请用** `GET /logout`）
- `GET /logout`：浏览器访问，清除 Cookie 后重定向到首页
- `GET /api/auth/me`

### 族谱接口（需登录 Cookie）

- `GET /api/genealogies`：仅返回当前用户创建或受邀的族谱
- `GET /api/genealogies/{id}`
- `GET /api/genealogies/{id}/me`：当前用户在本族谱角色（`Owner` / `Editor` / `Viewer`）
- `GET /api/genealogies/{id}/tree?rootId=`：后代树；`rootId` 可选
- `GET /api/genealogies/{id}/ancestors?personId=`：祖先树（JSON 中子列表表示父母）
- `GET /api/genealogies/{id}/kinship?fromPersonId=&toPersonId=`：亲缘通路（`connected` + `path` 数组）
- `GET /api/genealogies/{id}/collaborators`：协作成员列表
- `POST /api/genealogies`：请求体 JSON `{ "title", "surname", "compiledAt?" }`，创建者由服务端从登录态写入
- `PUT /api/genealogies/{id}`：更新元数据（Owner / Editor）
- `DELETE /api/genealogies/{id}`：删除整本族谱及关联数据（仅 Owner）
- `POST /api/genealogies/{id}/invite`：请求体 `{ "email", "role?" }`，`role` 为 `Editor` 或 `Viewer`（仅 Owner）
- `PATCH /api/genealogies/{id}/collaborators/{userId}`：请求体 `{ "role": "Editor"|"Viewer" }`（仅 Owner，不可改谱主）
- `DELETE /api/genealogies/{id}/collaborators/{userId}`：移除协作者（不可移除谱主）

### 人员接口（需登录 Cookie）

- `GET /api/persons/byGenealogy/{gid}?q=`
- `GET /api/persons/{id}`
- `POST /api/persons`（Owner / Editor）
- `GET /api/persons/{id}/family`：父母与配偶 Id（编辑回填）
- `PUT /api/persons/{id}`：请求体含 `syncRelationships`；为 `true` 时按 `fatherId`/`motherId`/`spouseId` 重写关系（可全 null 清除）
- `DELETE /api/persons/{id}`（Owner / Editor）

## 数据库说明

项目使用 **MySQL**，连接字符串在 `appsettings.json` 的 `ConnectionStrings:DefaultConnection`（默认库名 `genealogy`）。

首次启动或更新代码后，应用会对数据库执行 `Migrate()` 应用迁移。若你曾用旧版 `EnsureCreated()` 建过库且与迁移冲突，请**删除该库后重建**再启动。

**迁移 `AddParentChildGenealogyParentIndex`**：将 `ParentChildren` 上单列 `IX_GenealogyId` 调整为复合索引 **`IX_ParentChildren_GenealogyId_ParentId`**，利于按族谱 + 父节点多代向下扫描（课程物理设计对比请在导入大数据后执行 `EXPLAIN`）。

**迁移 `FkCheckAndTriggers`（约 2026-05-12）**：为 `Genealogies`、`GenealogyUsers`、`Persons`、`ParentChildren`、`Marriages` 添加外键（`ON DELETE RESTRICT`，协作者 `InvitedByUserId` 为 `SET NULL`）；上述 CHECK；`ParentChildren` 上 `BEFORE INSERT/UPDATE` 触发器（跨表出生年、族谱一致性）。拉取代码后请执行一次 `dotnet run` 或 `dotnet dotnet-ef database update` 以应用迁移。

**本地工具**：仓库根目录已配置 `dotnet-tools.json`，执行 `dotnet tool restore` 后可使用：

```bash
dotnet dotnet-ef migrations add <名称>
dotnet dotnet-ef database update
```

**演示账号（种子数据）**：`demo@genealogy.local` / `Demo123!`（首次空库时自动创建，并绑定「张氏族谱」示例）。

## 当前数据库模型

- `Users`
- `Genealogies`
- `GenealogyUsers`
- `Persons`
- `ParentChildren`
- `Marriages`

## 后续计划

- 树状谱系展示增强（多根、导出图）
- Dashboard 统计与筛选细化
- 在真实大数据集上完成「四代查询」**有/无索引**耗时记录与 `EXPLAIN ANALYZE` 截图归档

