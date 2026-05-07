# GenealogyApp

一个用于族谱管理的 ASP.NET Core Blazor Server 原型项目，支持用户注册/登录、登录后查看当前用户信息、族谱列表展示，以及后续扩展的成员管理、树状预览和亲缘关系查询。

## 已实现功能

- 用户注册与登录
- Cookie 认证
- 登录后显示当前用户信息
- 首页 Dashboard
- 族谱列表基础 API
- SQLite 本地数据库自动建表
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
cd GenealogyApp
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
- `POST /api/auth/logout`
- `GET /api/auth/me`

### 族谱接口

- `GET /api/genealogies`
- `GET /api/genealogies/{id}`
- `POST /api/genealogies`

### 人员接口

- `GET /api/persons/byGenealogy/{gid}?q=`
- `GET /api/persons/{id}`
- `POST /api/persons`

## 数据库说明

项目使用 **MySQL**，连接字符串在 `appsettings.json` 的 `ConnectionStrings:DefaultConnection`（默认库名 `genealogy`）。

首次启动时使用 `EnsureCreated()` 自动建表；正式开发也可改用 `dotnet ef migrations` 管理架构变更。

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
- Dashboard 统计完善
- 更完整的权限控制与邀请协作流程

