# GenealogyApp

一个用于族谱管理的 ASP.NET Core Blazor Server 原型项目，支持用户注册/登录、登录后查看当前用户信息、族谱列表展示，以及后续扩展的成员管理、树状预览和亲缘关系查询。

**团队协作**：目录约定、认证与数据隔离说明见 [docs/开发协作说明.md](docs/开发协作说明.md)；业务源码含中文注释与关键 XML 文档（`Migrations/` 为工具生成，勿手改）。

## 已实现功能

- 用户注册与登录
- Cookie 认证
- 登录后显示当前用户信息
- 首页 Dashboard
- 族谱列表 API（仅本人创建或受邀可见）
- Dashboard 汇总（仅统计有权访问的族谱内成员）
- MySQL 8 数据库（EF Core **Migrations** + 启动时 `Migrate()`）
- 简单图形化界面

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
http://localhost:5000
```

## 页面说明

- `/`：Dashboard，显示当前登录用户和族谱列表入口
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
- `GET /api/genealogies/{id}/tree`
- `POST /api/genealogies`：请求体 JSON `{ "title", "surname", "compiledAt?" }`，创建者由服务端从登录态写入

### 人员接口（需登录 Cookie）

- `GET /api/persons/byGenealogy/{gid}?q=`
- `GET /api/persons/{id}`
- `POST /api/persons`

## 数据库说明

项目使用 **MySQL**，连接字符串在 `appsettings.json` 的 `ConnectionStrings:DefaultConnection`（默认库名 `genealogy`）。

首次启动或更新代码后，应用会对数据库执行 `Migrate()` 应用迁移。若你曾用旧版 `EnsureCreated()` 建过库且与迁移冲突，请**删除该库后重建**再启动。

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

- 族谱与成员的完整 CRUD
- 树状谱系展示
- 祖先查询
- 两人亲缘关系路径查询
- Dashboard 统计与筛选细化
- 族谱邀请协作（写入 `GenealogyUsers` 的完整流程）

