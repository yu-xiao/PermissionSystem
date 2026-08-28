# PermissionSystem 项目文档

## 项目概述

PermissionSystem 是一个基于 .NET 10 和 Vue 3 的企业级权限管理系统，采用前后端分离架构。

## 技术栈

### 后端技术栈

**框架与运行时**
- .NET 10.0 (C#)
- ASP.NET Core Web API
- Entity Framework Core 10.0.7

**架构模式**
- Clean Architecture / DDD (领域驱动设计)
- 项目分层：
  - `PermissionSystem.Api` - Web API 层
  - `PermissionSystem.Application` - 应用服务层
  - `PermissionSystem.Domain` - 领域模型层
  - `PermissionSystem.Infrastructure` - 基础设施层
  - `PermissionSystem.Shared` - 共享层
  - `PermissionSystem.Worker` - 后台任务服务

**身份认证与授权**
- OpenIddict 7.6.0 (OAuth 2.0 / OpenID Connect)
- JWT (System.IdentityModel.Tokens.Jwt 7.7.1)
- Microsoft.Extensions.Identity.Core 10.0.7
- SSO (单点登录) 支持

**数据存储**
- SQL Server (Microsoft.EntityFrameworkCore.SqlServer 10.0.7)
- Redis (StackExchangeRedis) - 缓存
- MinIO 7.0.0 - 对象存储

**后台任务**
- Hangfire 1.8.23 (作业调度)
- RabbitMQ 7.2.1 (消息队列)

**日志与监控**
- Serilog 4.x (结构化日志)
  - Serilog.AspNetCore 10.0.0
  - Serilog.Sinks.Console 6.1.1
  - Serilog.Sinks.File 7.0.0
- OpenTelemetry 1.15.x (分布式追踪)
  - 集成 AspNetCore, EntityFrameworkCore, HTTP, Redis

**API 文档**
- Swashbuckle.AspNetCore 10.2.3 (Swagger)
- Microsoft.OpenApi 2.4.1

**测试框架**
- `PermissionSystem.Tests` - 集成测试
- `PermissionSystem.UnitTests` - 单元测试
- `PermissionSystem.IntegrationTests` - 集成测试

### 前端技术栈

**位置**: `frontend/permission-admin/`

**核心框架**
- Vue 3.5.34 (Composition API)
- Vue Router 5.0.6 (路由管理)
- Pinia 3.0.4 (状态管理)

**UI 组件库**
- Element Plus 2.14.0

**网络请求**
- Axios 1.16.0
- NProgress 0.2.0 (进度条)

**构建工具**
- Vite 8.0.12 (构建工具)
- TypeScript 6.0.2
- Sass 1.99.0 (CSS 预处理器)

**代码质量**
- ESLint 9.39.1 (代码检查)
- Prettier 3.7.4 (代码格式化)
- Vue TSC 3.2.8 (类型检查)

**测试**
- Vitest 4.0.15 (单元测试)
- Playwright 1.57.0 (E2E 测试)
- Vue Test Utils 2.4.6
- JSDOM 26.1.0

## 项目结构

```
PermissionSystem/
├── backend/                           # 后端项目
│   ├── PermissionSystem.sln          # 解决方案文件
│   ├── PermissionSystem.Api/         # Web API 入口
│   ├── PermissionSystem.Application/ # 应用服务层
│   ├── PermissionSystem.Domain/      # 领域模型层
│   ├── PermissionSystem.Infrastructure/ # 基础设施层
│   ├── PermissionSystem.Shared/      # 共享代码
│   ├── PermissionSystem.Worker/      # 后台服务
│   ├── PermissionSystem.Tests/       # 测试项目
│   ├── PermissionSystem.UnitTests/   # 单元测试
│   └── PermissionSystem.IntegrationTests/ # 集成测试
└── frontend/                          # 前端项目
    └── permission-admin/              # Vue 3 管理后台
        ├── src/                       # 源代码
        ├── e2e/                       # E2E 测试
        ├── package.json               # 依赖配置
        ├── vite.config.ts            # Vite 配置
        ├── vitest.config.ts          # 单元测试配置
        ├── playwright.config.ts       # E2E 测试配置
        └── tsconfig.*.json           # TypeScript 配置
```

## 关键特性

### 后端特性
1. **SSO 单点登录** - 支持 OIDC 协议的 SSO 集成
2. **分布式追踪** - OpenTelemetry 全链路追踪
3. **后台任务** - Hangfire 调度 + RabbitMQ 消息队列
4. **缓存策略** - Redis 分布式缓存
5. **对象存储** - MinIO 文件存储
6. **身份认证** - OpenIddict OAuth 2.0 服务器

### 前端特性
1. **打包优化** - Vite 代码分割策略
   - vendor-vue: Vue 核心生态
   - vendor-element-plus: UI 组件库
   - vendor-http: 网络请求库
2. **路由权限** - 基于路由的权限控制
3. **状态管理** - Pinia 模块化状态
4. **类型安全** - 完整的 TypeScript 支持

## 开发指南

### 后端开发

**运行要求**
- .NET 10 SDK
- SQL Server
- Redis
- RabbitMQ (可选)
- MinIO (可选)

**构建命令**
```bash
cd backend
dotnet restore
dotnet build
dotnet run --project PermissionSystem.Api
```

**测试命令**
```bash
dotnet test PermissionSystem.UnitTests
dotnet test PermissionSystem.IntegrationTests
```

### 前端开发

**运行要求**
- Node.js (推荐 LTS 版本)
- npm 或 pnpm

**开发命令**
```bash
cd frontend/permission-admin
npm install
npm run dev          # 启动开发服务器
npm run build        # 生产构建
npm run type-check   # 类型检查
npm run lint         # 代码检查
npm run test:unit    # 单元测试
npm run test:e2e     # E2E 测试
npm run preview      # 预览构建结果
```

## API 文档

后端 API 文档通过 Swagger 提供，启动后端服务后访问：
- Swagger UI: `http://localhost:<port>/swagger`

## 数据库

- **主数据库**: SQL Server (Entity Framework Core)
- **迁移**: 使用 EF Core Migrations 管理数据库架构
- **种子数据**: 通过 Infrastructure 层初始化

## 配置说明

### 后端配置
- `appsettings.json` - 基础配置
- `appsettings.Development.json` - 开发环境配置
- `appsettings.Production.json` - 生产环境配置

主要配置项：
- 数据库连接字符串
- Redis 连接
- OpenIddict 配置
- SSO 配置
- RabbitMQ 连接
- MinIO 配置
- Serilog 日志配置
- OpenTelemetry 导出器配置

### 前端配置
- `.env` - 环境变量
- `vite.config.ts` - Vite 构建配置

## 部署

### 后端部署
1. 发布 .NET 应用: `dotnet publish -c Release`
2. 配置生产环境数据库和依赖服务
3. 运行迁移: `dotnet ef database update`
4. 启动应用

### 前端部署
1. 构建生产版本: `npm run build`
2. 部署 `dist/` 目录到 Web 服务器 (Nginx/IIS/CDN)
3. 配置 SPA 路由重写规则

## 监控与日志

**日志**
- 结构化日志通过 Serilog 输出
- 支持控制台和文件输出
- 日志级别可通过配置调整

**监控**
- OpenTelemetry 追踪数据
- 支持 OTLP 协议导出
- 可集成 Jaeger/Zipkin/Prometheus

**性能**
- EF Core 查询追踪
- HTTP 请求追踪
- Redis 操作追踪

## 代码约定

### 后端约定
- 使用 C# 10+ 特性
- 启用 Nullable 引用类型
- 隐式 using
- Clean Architecture 分层原则
- SOLID 设计原则

### 前端约定
- Vue 3 Composition API
- TypeScript 严格模式
- ESLint + Prettier 代码规范
- 组件化开发
- 响应式设计

## 测试策略

### 后端测试
- **单元测试**: 测试业务逻辑和领域模型
- **集成测试**: 测试 API 端点和数据库交互
- 使用 xUnit 或 NUnit 框架

### 前端测试
- **单元测试**: Vitest 测试组件和工具函数
- **E2E 测试**: Playwright 测试用户流程
- 测试覆盖率要求

## 安全性

- OAuth 2.0 / OpenID Connect 认证
- JWT Token 授权
- HTTPS 通信
- CORS 配置
- SQL 注入防护 (参数化查询)
- XSS 防护
- CSRF 防护

## 性能优化

**后端**
- Redis 缓存
- 异步编程 (async/await)
- 数据库索引优化
- 响应压缩

**前端**
- 代码分割 (Code Splitting)
- 懒加载路由
- 图片优化
- Tree Shaking
- 生产构建优化

## 故障排查

### 常见问题

**后端**
- 检查数据库连接字符串
- 验证 Redis 可用性
- 查看 Serilog 日志输出
- 检查 EF Core 迁移状态

**前端**
- 清除 node_modules 重新安装
- 检查 API 端点配置
- 查看浏览器控制台错误
- 验证路由权限配置

## 贡献指南

1. 创建功能分支
2. 遵循代码规范
3. 编写测试用例
4. 提交前运行所有测试
5. 提供清晰的 commit message

## 许可证

(根据项目实际情况填写)
