# 企业级架构问题逐项修复方案

## 1. 文档信息

- 项目：PermissionSystem
- 编制日期：2026-07-18
- 适用范围：后端、前端、数据库、消息、文件、认证授权、部署运维与测试体系
- 依据：当前仓库静态架构审计、现有测试结果、前端生产构建结果
- 文档目标：将已识别问题拆分为可独立实施、独立验证、独立回滚的修复项，并明确推荐执行顺序

本文档只描述修复方案，不代表相关代码已经完成。实际实施时必须遵循 `Architect → DBA → 用户确认 → Developer → Reviewer` 流程。

## 2. 逐项修复规则

为避免认证、租户、数据和基础设施改动互相干扰，后续实施必须遵守以下规则：

1. 一次原则上只处理一个修复编号，不把多个高风险问题合并成一次大改。
2. 当前修复项未通过 Reviewer 验收前，不进入下一项。
3. 每项开始前重新检查工作区状态，保留用户已有改动。
4. 每项实施前由 Architect 明确范围、预计文件、兼容策略和不做事项。
5. 涉及实体、索引、约束或历史数据时，必须先完成 DBA 评审并由用户确认迁移方案。
6. 涉及认证协议、租户隔离、权限模型、公开 API 契约时，必须先确认兼容和灰度策略。
7. 每项只做最小必要变更，不借机大规模重构。
8. 每项必须包含自动化测试或明确说明无法自动化验证的原因。
9. 每项完成后最终说明必须包含：改了什么、验证了什么、还有什么风险。
10. 如果实施过程中发现当前方案需要扩大范围，应停止当前项，回到 Architect/DBA 重新确认。

## 3. 状态定义

- `[ ]`：未开始
- `[A]`：Architect 方案编制中
- `[D]`：DBA 评审中
- `[C]`：等待用户确认
- `[I]`：Developer 实施中
- `[R]`：Reviewer 复核中
- `[x]`：已完成并验收
- `[B]`：阻塞，等待业务信息或外部条件

## 4. 总体执行顺序

### 阶段一：生产安全与租户边界

- [ ] EA-001 OpenIddict 生产签名与加密密钥治理
- [x] EA-002 反向代理、HTTPS、真实 IP 与安全响应头
- [ ] EA-003 新增 SPA Public Client + Authorization Code + PKCE
- [ ] EA-004 前端认证会话与 Token 存储迁移
- [ ] EA-005 下线浏览器 Password Flow 与 Client Secret
- [x] EA-006 服务端租户写入一致性校验
- [x] EA-007 租户过滤改为 fail-closed 与显式系统作用域
- [ ] EA-008 租户初始化、停用与生命周期闭环
- [ ] EA-009 登录、刷新 Token 与租户状态重新校验
- [ ] EA-010 用户、角色和权限变更即时失效

### 阶段二：核心业务可靠性

- [ ] EA-011 RabbitMQ 关闭时的通知降级闭环
- [ ] EA-012 敏感操作二次验证重构
- [ ] EA-013 MFA 与密码过期策略真实落地
- [ ] EA-014 统一异常与 HTTP 状态码映射
- [x] EA-015 审计日志独立事务与审计操作人自动填充
- [ ] EA-016 SQL 报表执行安全隔离
- [ ] EA-017 工作流与状态机并发控制
- [ ] EA-018 文件持久化与 MinIO 能力治理
- [ ] EA-019 文件安全、业务 ACL 与存储补偿

### 阶段三：权限、数据与消息治理

- [ ] EA-020 数据权限统一强制机制
- [ ] EA-021 异步查询与高频查询性能治理
- [ ] EA-022 软删除唯一约束与通用并发模型
- [ ] EA-023 真正的事务型 Outbox
- [ ] EA-024 RabbitMQ 连接、DLQ、重试与消费治理
- [ ] EA-025 幂等请求指纹与分布式限流
- [ ] EA-026 SSO/Webhook 外联安全与 SSRF 防护

### 阶段四：生产运维与长期演进

- [ ] EA-027 健康检查、指标、日志归档与告警
- [ ] EA-028 Docker/生产部署安全加固
- [ ] EA-029 未闭环能力的产品状态治理
- [ ] EA-030 API 版本治理与模块化单体边界
- [ ] EA-031 CI/CD、自动化测试与前端工程化

## 5. 详细修复项

## EA-001 OpenIddict 生产签名与加密密钥治理

### 问题

当前 OpenIddict 无条件使用开发签名证书和开发加密证书，生产环境无法稳定控制 Token 签名材料，也不具备密钥轮换和灾备恢复能力。

### 目标

- Development 可以继续使用开发证书。
- Docker/Production 必须显式配置生产证书或受管密钥。
- 缺少生产密钥时启动失败，不允许静默降级。
- 支持新旧证书并存的轮换窗口。

### 实施要点

1. 新增 OpenIddict 证书配置模型，至少包含证书来源、路径/存储位置、密码引用和当前密钥标识。
2. Development 分支保留 `AddDevelopmentSigningCertificate` 和 `AddDevelopmentEncryptionCertificate`。
3. 非 Development 环境加载受管证书，禁止自动生成临时证书。
4. 密钥密码只允许从环境变量或 Secret Manager 注入。
5. 补充证书到期监控、轮换步骤和故障回滚说明。

### 预计影响文件

- `backend/PermissionSystem.Api/Program.cs`
- `backend/PermissionSystem.Infrastructure/Options/*`
- `backend/PermissionSystem.Api/appsettings*.json`
- `.env.example`
- `docker-compose.yml`
- `docs/deployment-guide.md`
- `docs/security-guide.md`

### DBA 影响

无数据库结构变更。需要评估 OpenIddict 已签发 Token 在密钥轮换期间的兼容性。

### 验证与验收

- Development 使用开发证书可正常签发 Token。
- Production 未配置证书时启动失败并给出明确错误。
- Production 配置证书后登录、刷新和撤销 Token 正常。
- 旧证书仍在验证集合内时，旧 Token 可正常验证。

### 回滚

保留上一版本证书配置和证书文件；应用回滚时不得删除旧密钥。

## EA-002 反向代理、HTTPS、真实 IP 与安全响应头

### 实施状态

- 状态：`[x]` 已完成
- 完成日期：2026-07-29
- 实际改动：接入仅信任显式代理地址或网段的 `ForwardedHeadersMiddleware`；新增统一客户端 IP 访问器，登录、SSO、IP 黑白名单、API Key、限流和审计不再直接解析 `X-Forwarded-For`；Production 对 AllowedHosts、CORS 和可信代理配置执行 fail-closed 校验，并启用 HTTPS/HSTS；API 与前端 Nginx 增加基础安全响应头；Docker Compose 仅信任固定地址 `172.28.0.10` 的前端 Nginx 代理。
- 配置策略：CORS、AllowedHosts 和反向代理边界均支持通过 `appsettings*.json` 配置，部署平台环境变量仅作为 ASP.NET Core 标准配置覆盖方式，不是唯一配置来源。Docker 继续定义为开发/集成环境并保留 HTTP 兼容，Production 禁止 OpenIddict Transport Security 降级。
- 数据库变更：无。不新增实体、字段、索引或 EF Migration。
- 验证结果：新增 10 个 EA-002 专项测试并通过；后端全量测试 85 个通过、4 个依赖真实 SQL Server 的 OAuth 测试因未配置测试连接而跳过；API Release 构建 0 警告、0 错误；前端生产构建通过，保留既有大 chunk 警告；全部 appsettings JSON 和 Docker Compose YAML 语法校验通过。
- 剩余风险：当前环境未安装 Docker/Nginx，未执行 Compose 全链路和 `nginx -t`；生产发布仍需使用真实域名、CORS 来源、代理 IP/网段和 TLS 证书完成预发布验证。若 Docker Compose 被用于生产，必须改用 Production 环境并在外层可信代理完成 TLS 终止，不能依赖 Docker 环境的 HTTP 兼容配置。

### 问题

多个位置直接读取并信任 `X-Forwarded-For`，且未统一配置可信代理。CORS 未配置时会放开所有来源，生产安全响应头和 HSTS 也未形成闭环。

### 目标

- 真实 IP 只能由可信反向代理注入。
- IP 黑白名单、限流和审计使用同一客户端 IP 解析结果。
- Production 强制 HTTPS/HSTS，并配置基础安全响应头。
- CORS 和 AllowedHosts 缺失时 fail-closed。

### 实施要点

1. 使用 `ForwardedHeadersMiddleware`，配置 KnownProxies/KnownNetworks 和 ForwardLimit。
2. 删除业务中间件内对 `X-Forwarded-For` 的手工解析。
3. 引入统一的客户端 IP 访问器供审计、限流、登录和 API Key 使用。
4. Production 启用 HSTS，禁止 OpenIddict Transport Security 降级。
5. 增加 CSP、`X-Content-Type-Options`、`Referrer-Policy`、`Permissions-Policy` 和 frame 限制。
6. Production 未配置允许域名和 CORS 来源时阻止启动。

### 预计影响文件

- `backend/PermissionSystem.Api/Program.cs`
- `backend/PermissionSystem.Api/Middlewares/*`
- `backend/PermissionSystem.Api/Services/*`
- `frontend/permission-admin/nginx.conf`
- `docker-compose.yml`

### DBA 影响

无数据库变更。

### 验证与验收

- 客户端伪造 `X-Forwarded-For` 不再影响真实 IP。
- 可信代理转发后能够取得正确客户端 IP。
- IP 黑白名单、登录日志、操作日志和限流记录的 IP 一致。
- Production HTTP 请求被重定向或拒绝，安全响应头存在。

## EA-003 新增 SPA Public Client + Authorization Code + PKCE

### 问题

现有浏览器客户端被注册为 Confidential Client，并使用 Password Flow 和 Client Secret，不符合 SPA 安全模型。

### 目标

在不立即中断现有前端的前提下，先新增适合 SPA 的 Public Client 和 Authorization Code + PKCE 登录链路。

### 实施要点

1. 新增 Public Client Seed，禁止 Client Secret。
2. 只授予 Authorization Code、Refresh Token、PKCE 和必要 Scope。
3. 配置严格的 Redirect URI 和 Post Logout Redirect URI 白名单。
4. 旧 Confidential Client 暂时保留，作为过渡兼容路径。
5. 增加授权码、state、nonce、PKCE、错误回调集成测试。

### 预计影响文件

- `backend/PermissionSystem.Infrastructure/SeedData/SeedDataInitializer.cs`
- `backend/PermissionSystem.Api/Program.cs`
- `backend/PermissionSystem.IntegrationTests/Authentication/*`
- `.env.example`

### DBA 影响

不需要新增业务表，但 Seed 会新增或调整 OpenIddict Applications 数据。必须确保 Seed 可重复执行。

### 验证与验收

- Public Client 无 Secret 可以完成授权码登录。
- 未带 PKCE 的请求被拒绝。
- 非白名单 Redirect URI 被拒绝。
- 旧登录链路在前端迁移完成前仍可使用。

## EA-004 前端认证会话与 Token 存储迁移

### 问题

access token 和 refresh token 存入 localStorage，任何成功执行的 XSS 都可长期窃取会话。

### 目标

确定并落地企业级前端会话模型。

### 方案决策点

优先推荐 BFF：

- 浏览器只保存 HttpOnly、Secure、SameSite Cookie。
- BFF 代表浏览器保存和刷新 Token。
- 增加 CSRF 防护。

备选 Public SPA：

- access token 仅保存在内存。
- refresh token 使用轮换、短生命周期和重用检测。
- 页面刷新后的会话恢复策略需要单独设计。

该项开始前必须由用户确认采用 BFF 还是纯 SPA。不得由 Developer 自行决定。

### 预计影响文件

- `frontend/permission-admin/src/utils/token.ts`
- `frontend/permission-admin/src/utils/request.ts`
- `frontend/permission-admin/src/api/auth.ts`
- `frontend/permission-admin/src/stores/auth.ts`
- 如果选择 BFF，新增对应 Api 入口和 CSRF 中间件

### DBA 影响

选择 BFF 通常无业务表变更；若增加 Refresh Token 重用检测或会话安全版本，可能需要调整 UserSession。

### 验证与验收

- 构建产物和浏览器存储中不再出现 Client Secret。
- localStorage 中不再保存长期 Token。
- 登录、刷新、退出、强制下线和多标签页行为正常。
- XSS 场景下无法直接读取 refresh token。

## EA-005 下线浏览器 Password Flow 与 Client Secret

### 前置依赖

EA-003、EA-004 已完成并稳定运行。

### 目标

- 前端不再提交用户名、密码到 `/connect/token`。
- 删除 `VITE_OAUTH_CLIENT_SECRET`。
- 移除 Permission Admin 客户端的 Password 和 Client Credentials 权限。
- 根据业务确认是否完全关闭服务端 Password Flow。

### 验证与验收

- 仓库、镜像构建参数和前端产物中不存在 OAuth Client Secret。
- 管理后台只通过授权码 + PKCE/BFF 登录。
- Password Grant 请求被明确拒绝。

## EA-006 服务端租户写入一致性校验

### 实施状态

- 状态：`[x]` 已完成
- 完成日期：2026-08-04
- 实际改动：新增统一 `ITenantWriteResolver`，普通用户写入目标固定为 Claim/Context 租户，提交其他租户返回 Forbidden；超级管理员必须通过请求 `TenantId` 或 `X-Tenant-Id` 显式选择目标租户，请求体与 Header 不一致时拒绝。用户、角色、部门、字典、配置、任务、文件、菜单、权限、通知模板、编号规则、SSO Provider、工作流和 Demo 业务创建链路已接入统一解析；用户部门、菜单父节点等关联对象增加目标租户一致性检查。
- 基础设施兜底：`AppDbContext.SaveChanges/SaveChangesAsync` 校验 Added/Modified/Deleted 的租户实体，阻止跨租户写入和 `TenantId` 更新；`Tenant` 实体继续强制 `TenantId == Id`。显式重新选择租户时恢复查询过滤，避免超级管理员目标租户写入期间继续处于全租户查询状态。
- 审计与响应：操作日志独立作用域继承请求目标租户，跨租户管理操作记录目标 `TenantId`；`Forbidden` 和 `ValidationFailed` 分别映射为 HTTP 403 和 422，并同步用于失败操作日志状态。
- 数据库变更：无。未新增实体、字段、索引或 EF Migration。新增只读审计脚本 `docs/ea-006-tenant-consistency-audit.sql`，仅报告历史跨租户关联异常，不自动修复数据。
- 验证结果：EA-006 解析器、普通用户越租户拒绝、超级管理员显式目标、Header/请求冲突、Added/Modified/Deleted 兜底、系统无上下文兼容、目标租户审计及 HTTP 403/422 映射测试均通过；UnitTests 46 个通过；IntegrationTests 11 个通过，4 个真实 SQL Server OAuth 测试因缺少测试连接环境变量跳过；API Release 和 Worker Release 构建均为 0 错误。
- 未执行项：`PermissionSystem.Tests` 的编译输出被本机 360 隔离，按用户确认跳过；只读历史数据审计脚本尚未在真实 SQL Server 数据库执行。
- 后续闭环：EA-006 当时为 Seed、Worker 和后台任务保留的无租户上下文写入兼容口，已由 EA-007 改为 fail-closed 与显式系统作用域；现有 `Microsoft.OpenApi`、`System.Security.Cryptography.Xml` 依赖漏洞警告仍未在本项升级处理。

### 问题

多个创建请求允许客户端传入 TenantId，当前保存逻辑不会阻止普通租户用户向其他 TenantId 写入数据。

### 目标

- 普通用户只能写入当前 Claim/Context 对应租户。
- 超级管理员跨租户操作必须显式选择目标租户并留下审计记录。
- 基础设施层提供最终兜底校验。

### 实施要点

1. Application 创建用例不再直接信任普通请求中的 TenantId。
2. 统一 `TenantIdResolver` 或用例辅助方法，区分普通用户和超级管理员。
3. `AppDbContext.SaveChanges` 检查 Added/Modified/Deleted 实体 TenantId。
4. 禁止更新实体时修改 TenantId。
5. 为用户、角色、部门、字典、配置、任务、文件等创建接口补充越租户测试。

### DBA 影响

无必需结构变更。建议增加数据审计脚本，检查历史库中是否已存在跨租户关系异常。

### 验证与验收

- 普通用户提交其他 TenantId 返回 403/422，不产生数据。
- 实体 TenantId 在更新时不可修改。
- 超级管理员必须显式指定目标租户，且操作日志记录目标租户。

## EA-007 租户过滤改为 fail-closed 与显式系统作用域

### 实施状态

- 状态：`[x]` 已完成
- 完成日期：2026-08-04
- 实际改动：租户全局查询过滤改为 fail-closed，只有已解析 TenantId 或显式 `ISystemTenantScope` 才能访问租户数据；无租户上下文默认返回空结果，写入则返回明确校验错误。移除 `ITenantContext.DisableTenantFilter` 和通用仓储 `Query(ignoreQueryFilters: true)`，新增强制 TenantId 且保留软删除条件的受限查询。超级管理员 HTTP 请求不再自动关闭租户过滤，系统作用域在 HTTP 执行期间强制拒绝，并通过结构化进入/退出日志记录用途和耗时。
- 系统入口迁移：开发 Seed、Outbox Publisher、定时任务同步与执行、Webhook 投递已进入带用途标识的显式系统作用域；RabbitMQ 通知消费者根据消息 TenantId 建立单租户上下文；SQL 报表在无租户上下文时追加恒假条件。租户管理使用只暴露 Tenant 实体且强制超级管理员身份的窄目录仓储，不向 HTTP 请求开放全库系统作用域；SSO、OIDC 和 API Client 认证前置查询改为显式单租户查询，API Client 认证现在要求显式 `X-Tenant-Id`。
- 数据库变更：无。不新增实体、字段、索引或 EF Migration。Outbox 跨租户扫描继续使用现有 `{Status, NextRetryAt, CreatedAt}` 索引；定时任务启动同步属于低频全量扫描。
- 验证结果：新增或调整 fail-closed 查询、无上下文写入拒绝、显式系统写入、嵌套作用域释放、HTTP 开启拒绝、跨租户系统读取、租户目录超级管理员限制及 API Client 显式租户测试；`PermissionSystem.UnitTests` 51 个通过，`PermissionSystem.Tests` 41 个通过，`PermissionSystem.IntegrationTests` 11 个通过、4 个真实 SQL Server OAuth 测试因未配置测试连接而跳过；API 与 Worker Release 构建均为 0 错误。
- 剩余风险：尚未在真实 SQL Server 生产数据规模下复核 Outbox 与定时任务跨租户查询执行计划；API Client 调用方必须同步携带 `X-Tenant-Id`。现有 `Microsoft.OpenApi`、`System.Security.Cryptography.Xml` 依赖漏洞警告未在本项升级处理。

### 问题

当前 TenantId 缺失时自动放开租户过滤。后台任务依赖这一行为完成跨租户扫描，但这也会让错误配置或上下文丢失变成全库访问。

### 目标

- 默认无租户上下文时拒绝访问租户数据。
- 跨租户后台任务使用显式系统作用域。
- 系统作用域必须可搜索、可审计、不可由普通 HTTP 请求开启。

### 实施要点

1. 修改全局查询过滤条件，TenantId 缺失不再等于禁用过滤。
2. 增加只允许受控服务使用的 `ISystemTenantScope`。
3. Outbox、归档、健康检查和系统初始化逐项迁移到显式系统作用域。
4. 禁止通过请求参数或普通 DI 服务直接关闭租户过滤。

### DBA 影响

无结构变更，但要验证所有跨租户后台查询的索引仍可使用。

### 验证与验收

- 未设置租户上下文访问仓储时返回明确错误或空结果。
- 系统任务仍能跨租户工作。
- HTTP 请求无法开启系统作用域。

## EA-008 租户初始化、停用与生命周期闭环

### 问题

创建租户后没有管理员、基础角色、菜单、权限、配置等初始化流程；停用租户也没有统一禁止登录、刷新 Token、API Key、后台任务和 SSO。

### 目标

建立租户从创建到停用/恢复/注销的完整生命周期。

### 实施要点

1. 明确租户状态：Initializing、Active、Disabled、Failed、Archived。
2. 创建租户与初始化过程解耦，记录初始化进度和错误。
3. 初始化内容至少包含租户管理员、基础角色、基础菜单权限和安全策略。
4. 停用租户时撤销用户会话、阻止 Token 刷新、API Key、SSO 登录和业务任务。
5. 恢复租户时不得自动恢复已撤销会话。
6. 注销/归档租户属于独立业务需求，不在本项直接实现物理删除。

### DBA 影响

预计需要 Tenant 状态、初始化状态、错误信息等字段或独立初始化记录表，需要迁移。

### 验证与验收

- 新租户初始化后可以由其管理员登录并看到基础菜单。
- 初始化失败可以重试且不会重复创建数据。
- 停用租户后现有 access token、refresh token、API Key 和 SSO 均不可继续访问。

## EA-009 登录、刷新 Token 与租户状态重新校验

### 问题

非默认租户登录失败时，失败记录和日志可能仍使用默认租户；Refresh Token 路径没有重新检查用户、租户、角色和权限状态。

### 目标

- 登录失败记录归属正确租户。
- Refresh Token 每次重新检查用户和租户状态。
- 刷新后的 Claims 来自当前数据库状态，而不是旧 principal。

### DBA 影响

可能新增用户 SecurityStamp/PermissionVersion 字段，具体与 EA-010 联合评审。

### 验证与验收

- 多租户登录失败锁定互不影响。
- 禁用用户或租户后 refresh token 立即失效。
- 权限变化后刷新所得 Token 使用最新 Claims。

## EA-010 用户、角色和权限变更即时失效

### 问题

禁用用户、管理员重置密码、分配角色、修改角色权限后，现有 access token 可能继续有效到过期。

### 目标

建立统一的授权版本和会话撤销策略。

### 实施要点

1. 用户增加 SecurityStamp 或 PermissionVersion。
2. Token 带入版本 Claim。
3. 高风险接口校验当前版本，或使用短期缓存降低数据库压力。
4. 用户禁用、删除、重置密码必须撤销全部会话和 refresh token。
5. 角色权限变化时批量提高受影响用户版本或撤销会话。
6. 明确 access token 最长残留窗口。

### DBA 影响

预计新增用户授权版本字段及索引，需要迁移和默认值兼容。

### 验证与验收

- 禁用用户后当前 access token 在目标时限内失效。
- 管理员重置密码后所有旧会话失效。
- 移除权限后用户不能继续调用对应 API。

## EA-011 RabbitMQ 关闭时的通知降级闭环

### 问题

通知统一写 Outbox，但默认 RabbitMQ 关闭时没有 Publisher/Consumer，站内通知和敏感操作验证码无法送达。

### 目标

- RabbitMQ 关闭时平台核心通知功能仍可工作。
- RabbitMQ 开启时继续使用可靠异步投递。
- 运行状态清晰，不允许消息永久积压却显示发送成功。

### 实施要点

1. 定义通知投递策略：Direct、OutboxRabbitMQ、Disabled。
2. Direct 模式直接创建站内通知，但仍保留事务和失败处理。
3. API 返回真实投递状态，不把“已写 Outbox”表述为“已发送”。
4. 健康检查和管理页面显示当前投递模式。

### DBA 影响

通常无结构变更；如记录投递通道和状态，需调整通知或投递日志表。

### 验证与验收

- RabbitMQ 关闭时站内通知能够产生和读取。
- RabbitMQ 开启时消息仍经过 Outbox/Consumer。
- 两种模式都具备自动化测试。

## EA-012 敏感操作二次验证重构

### 问题

验证码明文存储，并通过当前登录会话的站内通知发送，不构成独立第二因素。

### 目标

实现可证明的 Step-up Authentication，而不是同一会话内的形式校验。

### 方案建议

按优先顺序支持：

1. 当前密码重新验证。
2. TOTP/Passkey。
3. 已验证邮件或短信验证码。

### 实施要点

- 验证码只保存哈希、用途、尝试次数、过期时间和使用状态。
- 增加频率限制、错误次数锁定和重放保护。
- 验证通过后签发短期 Step-up Ticket，绑定用户、租户、操作码和会话。
- 敏感接口消费 Ticket，不直接消费明文验证码。
- 查询字符串不再允许传验证码。

### DBA 影响

需要调整 SensitiveOperationVerification 表或新增 StepUpSession 表。

### 验证与验收

- 数据库无法读取原始验证码。
- Ticket 不能跨用户、租户、操作码和会话复用。
- 超时、重复使用、暴力尝试均被拒绝并记录安全事件。

## EA-013 MFA 与密码过期策略真实落地

### 前置依赖

EA-003 至 EA-005、EA-012。

### 目标

- `EnableMfa` 开启后真正影响登录流程。
- `PasswordExpireDays` 到期后只能进入强制改密流程。
- 未实现前不再在 UI 中展示为可用能力。

### DBA 影响

预计需要 LastPasswordChangedAt、MustChangePassword、MFA 凭证及恢复码表。MFA 密钥必须加密存储。

### 验证与验收

- 开启 MFA 的用户无法跳过第二步认证。
- 密码过期用户不能访问普通业务接口。
- 恢复码单次使用且不可明文读取。

## EA-014 统一异常与 HTTP 状态码映射

### 问题

所有 BusinessException 当前统一返回 HTTP 400，客户端无法正确区分认证、授权、资源不存在、并发冲突和校验失败。

### 目标

- ErrorCode 与 HTTP Status 建立唯一映射。
- EF 唯一约束、并发异常等转换为稳定业务错误。
- 所有错误响应保持 ApiResult 格式和 TraceId。

### 建议映射

- BadRequest → 400
- Unauthorized → 401
- Forbidden → 403
- NotFound → 404
- Conflict → 409
- ValidationFailed → 422
- TooManyRequests → 429
- InternalServerError → 500

### DBA 影响

无数据库变更。

### 验证与验收

- 为每种 ErrorCode 增加 API 集成测试。
- 唯一索引冲突返回 409，不泄露数据库异常。
- 未处理异常在生产环境不返回堆栈或内部信息。

## EA-015 审计日志独立事务与审计操作人自动填充

### 实施状态

- 状态：`[x]` 已完成
- 完成日期：2026-07-18
- 实际改动：操作日志改为在独立异步 DI Scope 和独立 AppDbContext 中保存；新增审计上下文抽象；HTTP 请求自动使用当前用户填充 CreatedBy/UpdatedBy；Worker、Seed 和其他无登录用户场景不伪造系统用户 ID，并保留调用方已显式提供的审计操作人。
- 数据库变更：无。复用现有 CreatedBy、UpdatedBy、CreatedAt、UpdatedAt 字段，不生成 EF Migration。
- 验证结果：新增 5 个审计专项测试并通过；后端 Release 构建 0 警告、0 错误；后端全量测试 75 个通过、4 个依赖真实 SQL Server 的 OAuth 测试因未配置测试连接而跳过。
- 剩余风险：操作日志仍会缓冲请求和响应内容，日志范围、响应大小和客户端真实 IP 治理分别留在后续审计性能与 EA-002 中处理；如未来需要区分 Worker、Seed、系统任务等非用户 Actor，应单独设计 ActorType，而不是向 Guid 用户字段写入伪造标识。

### 问题

操作日志与业务请求共用 DbContext，异常路径可能提交残留跟踪实体；CreatedBy/UpdatedBy 也没有统一填充。

### 目标

- 审计日志与业务事务隔离。
- 统一自动填充创建人、修改人。
- 审计失败不影响业务，业务失败也不会因写审计而提交残留变更。

### 实施要点

1. OperationLog 使用独立 DI Scope/DbContext 或可靠异步通道。
2. 限制审计请求体/响应体类型、大小和敏感字段。
3. AppDbContext 统一使用 IAuditContext 填充 CreatedBy/UpdatedBy，避免基础设施依赖完整的 HTTP 当前用户模型。
4. 系统任务、Seed 等无登录用户场景保持操作人为空；如未来需要区分不同系统 Actor，新增独立 ActorType 设计，不复用或伪造用户 Guid。
5. 审计日志禁止普通业务接口修改和软删除。

### DBA 影响

现有字段可复用。若引入 ActorType、TargetTenantId、安全事件类型，需要迁移。

### 验证与验收

- 构造业务修改后抛异常的测试，确认业务数据未提交但审计记录存在。
- CreatedBy/UpdatedBy 在 HTTP、Worker 和系统初始化中符合预期。

## EA-016 SQL 报表执行安全隔离

### 问题

正则检查无法证明 SQL 安全，当前执行连接与应用主库连接相同，查询可以接触密码哈希、Token、密钥和跨租户数据。

### 目标

- 报表查询与业务写库账号隔离。
- 只允许访问审核过的报表视图或只读 Schema。
- 租户条件不可由报表设计者绕过。

### 实施要点

1. 增加独立 ReportConnection，只授予 SELECT 指定视图的权限。
2. 禁止直接查询业务基础表、OpenIddict 表和敏感表。
3. 每个报表定义绑定白名单数据集，不直接保存任意 SQL，或使用受限模板。
4. 租户条件由数据集定义注入，不依赖结果集中存在 TenantId 列。
5. 增加最大执行时间、最大并发、最大行数、导出异步化和审计。

### DBA 影响

需要创建只读数据库账号、Schema/Views 和权限脚本；可能不需要 EF Migration，但需要独立 DBA 发布脚本。

### 验证与验收

- 无法查询 Users.PasswordHash、OpenIddict Tokens、API Secret 等敏感数据。
- 无法通过子查询、别名或系统表绕过租户隔离。
- 超时和超限查询被终止并记录。

## EA-017 工作流与状态机并发控制

### 问题

审批和状态转换采用查询后判断再更新，缺少 RowVersion 或条件更新，并发请求可能重复审批、重复生成记录或重复推进节点。

### 目标

- 同一任务只能成功处理一次。
- 同一业务状态只能按合法前置版本转换。
- 冲突返回 409，不能产生重复副作用。

### 实施要点

1. WorkflowTask、WorkflowInstance 和关键业务实体增加 RowVersion。
2. 状态更新使用原状态 + RowVersion 条件。
3. 工作流通知/Outbox 与状态变更处于同一事务。
4. 保留 API 幂等作为第一层，数据库并发控制作为最终兜底。
5. 编写并发批准、批准与拒绝同时发生、重复启动流程测试。

### DBA 影响

需要新增 rowversion 列和迁移。评估历史数据初始化及索引影响。

### 验证与验收

- 20 个并发审批请求只能有一个成功。
- 只生成一条有效审批记录和一次业务状态变化。
- 其他请求返回 409，不返回 500。

## EA-018 文件持久化与 MinIO 能力治理

### 问题

Docker 没有挂载 uploads 持久化目录，MinIO Provider 会直接抛 NotSupportedException，仓库中还存在上传文件被跟踪的情况。

### 目标

- Production 只使用可靠对象存储或明确挂载的持久化卷。
- 未实现的 Provider 不能被配置或在 UI 中选择。
- 上传内容不进入 Git 仓库。

### 实施要点

1. 先增加启动配置校验：MinIO 未实现时禁止选择。
2. Docker Development 为 Local Storage 挂载命名卷。
3. `.gitignore` 忽略 uploads，并处理已跟踪的示例上传文件。
4. 独立阶段实现 MinIO Save/Open/Delete 和健康检查。
5. Production 禁止默认使用容器本地临时目录。

### DBA 影响

FileResource 现有字段基本可复用。对象存储迁移时需要数据迁移/校验工具。

### 验证与验收

- 重建 API 容器后文件仍可下载。
- MinIO 配置启用后上传、下载、删除和健康检查正常。
- Git 状态不再出现上传文件。

## EA-019 文件安全、业务 ACL 与存储补偿

### 问题

上传依赖扩展名和客户端 Content-Type，文件整体缓冲到内存；文件权限只有系统级 permission，没有校验用户是否有权访问对应业务单据。文件系统和数据库操作也非原子。

### 目标

- 文件内容经过真实类型识别和恶意内容扫描。
- 文件访问继承业务对象权限。
- 数据库失败不会永久留下孤儿文件，物理删除失败不会造成数据不一致。

### 实施要点

1. 流式计算 SHA-256，避免整文件 `ToArray`。
2. 校验文件魔数，增加隔离区和病毒扫描状态。
3. 增加 IFileBusinessAccessChecker，根据 BusinessType/BusinessId 校验读写权限。
4. 上传先进入 Pending，数据库成功后转 Active；失败由补偿任务清理。
5. 删除先标记 PendingDelete，由后台任务物理删除并更新状态。
6. 下载使用安全 Content-Disposition，危险类型禁止 inline。

### DBA 影响

预计增加 FileStatus、ScanStatus、ScanMessage、DeletedAt 等字段及索引。

### 验证与验收

- 普通用户不能通过猜测文件 ID 下载无权访问的业务附件。
- 伪造扩展名和 Content-Type 会被识别。
- 上传/删除的数据库失败场景可以自动补偿。

## EA-020 数据权限统一强制机制

### 问题

当前数据范围主要依靠各 Application Service 手动调用，容易在新增模块时遗漏；UserDataScope 尚未真正参与权限合并。

### 目标

- 建立统一、可复用、可测试的数据权限规范。
- 明确角色数据范围与用户覆盖范围的合并规则。
- 新业务模块不应用数据权限时必须显式声明原因。

### 实施要点

1. 先确认角色与用户范围的业务优先级，不能由技术人员猜测。
2. 形成 DataScopeContext + Specification/Query Policy。
3. 为具备 CreatedBy/OwnerUserId/DepartmentId 的业务实体提供统一过滤扩展。
4. 列表、详情、更新、删除都必须使用同一可见性查询。
5. 增加架构测试或代码扫描，识别业务查询未应用数据范围。

### DBA 影响

可能调整 UserDataScope 唯一约束和数据结构；CustomDepartmentIds 长期建议规范化为关系表。

### 验证与验收

- All、CurrentUser、CurrentDepartment、Children、Custom 均有测试。
- 详情接口和修改接口不能绕过列表可见范围。
- 多角色和用户覆盖规则符合已确认业务规则。

## EA-021 异步查询与高频查询性能治理

### 问题

Application 大量同步执行 `ToList/Count/Any`，再使用 Task.FromResult 包装；部分列表存在 N+1、全量加载后内存分页和大字段读取。

### 目标

- 所有数据库 I/O 真正异步并传递 CancellationToken。
- 高频列表使用数据库分页和 DTO 投影。
- 消除已识别 N+1 和内存分页。

### 实施顺序

该项仍需拆成子批次逐个模块处理：

1. EA-021A Repository/Query 接口设计。
2. EA-021B 用户与角色查询。
3. EA-021C 工作流待办/抄送。
4. EA-021D 报表定义及参数。
5. EA-021E 日志、Outbox、Inbox、API 调用日志。

每个子批次单独实施和验收，不允许一次重写全部 Application Service。

### DBA 影响

根据生成 SQL 和执行计划补充索引，不能仅凭代码经验添加索引。

### 验证与验收

- 查询使用异步 EF API 和 CancellationToken。
- 分页查询不先加载全部数据。
- 记录关键接口 SQL 数量、耗时和内存基线，并证明修改后无退化。

## EA-022 软删除唯一约束与通用并发模型

### 问题

软删除后唯一编码是否允许复用没有统一规则；部分唯一索引不包含 IsDeleted，部分包含后又只能保留一条已删除历史。通用实体也缺少并发控制策略。

### 目标

- 按业务对象逐表明确“删除后是否允许复用编码”。
- 选择过滤唯一索引、历史版本号或禁止复用的统一实现方式。
- 对需要多人编辑的配置实体增加并发控制。

### 实施要求

该项必须逐表确认，禁止一次性机械修改全部索引。建议顺序：

1. Users/Roles/Permissions/Menus。
2. Dictionary/SystemConfig/NumberRule。
3. Workflow/StateMachine/Report/PrintTemplate。
4. SSO/Integration。
5. Demo 与未来业务单据。

### DBA 影响

高。涉及唯一索引变更、历史重复数据检查和回滚脚本。

### 验证与验收

- 每张表在迁移说明中明确复用语义。
- 迁移前检查历史冲突数据。
- 删除、重建、多次删除重建均有集成测试。

## EA-023 真正的事务型 Outbox

### 问题

当前 OutboxService 内部立即 SaveChanges，业务数据和消息记录未必处于同一事务，存在业务成功但消息缺失或消息存在但业务回滚的窗口。

### 目标

- 业务数据与 Outbox 记录一次数据库事务提交。
- Publisher 只负责投递已提交消息。
- 消费端保持幂等。

### 实施要点

1. Outbox Enqueue 只向当前 UnitOfWork 添加实体，不自行提交。
2. 由用例事务统一 SaveChanges。
3. 可选使用 Domain Event + SaveChanges Interceptor 自动生成 Outbox。
4. 明确 MessageId、事件版本、发生时间、聚合标识和 TraceId。
5. 消费处理与 Inbox 状态更新尽量处于同一事务。

### DBA 影响

可能增加 AggregateId、EventVersion、OccurredAt、LockedAt 等字段和索引。

### 验证与验收

- 业务事务回滚时不产生 Outbox。
- Outbox 写入失败时业务事务也失败。
- Publisher 崩溃重试时消费者不会产生重复业务副作用。

## EA-024 RabbitMQ 连接、DLQ、重试与消费治理

### 目标

- 复用长连接和通道，避免每条消息创建连接。
- 配置 prefetch、publisher confirm、mandatory/return、DLX 和死信队列。
- 毒消息可查询、告警、重放和人工放弃。

### DBA 影响

消息管理状态可继续使用 Outbox/Inbox；如果增加死信管理页面，可能需要死信记录表。

### 验证与验收

- RabbitMQ 重启后连接自动恢复。
- 不可路由消息不会静默丢失。
- 消费失败达到阈值进入 DLQ。
- 管理员可以查看失败原因并按权限重放。

## EA-025 幂等请求指纹与分布式限流

### 问题

幂等缓存未保存请求体指纹；内存限流在多实例下不一致。

### 目标

- 相同 Idempotency-Key 仅能重放相同业务请求。
- 不同请求体复用同一 Key 返回 409。
- 生产限流在多副本间一致。

### 实施要点

1. 幂等记录增加 Method、Path、主体摘要、状态、响应摘要和过期时间。
2. 统一规范哪些接口必须幂等，避免依赖前端自动为所有写请求生成随机 Key。
3. 生产限流放到 API Gateway 或 Redis 分布式实现。
4. 登录、刷新、API Key、Webhook 和报表分别配置策略。

### DBA 影响

如果幂等从 Redis 扩展到持久化审计，可能新增幂等记录表；否则无业务库变更。

### 验证与验收

- 同 Key 同请求返回原响应。
- 同 Key 不同请求返回 409。
- 多 API 实例共享同一限流额度。

## EA-026 SSO/Webhook 外联安全与 SSRF 防护

### 问题

OIDC metadata、userinfo 和 Webhook 目标均可由管理端配置，当前 HTTPS 校验不能阻止访问内网 HTTPS、云元数据地址或 DNS Rebinding。

### 目标

- 所有外联统一经过受控 HttpClient 和目标校验。
- Production 强制 HTTPS metadata。
- 阻止私网、环回、链路本地和云元数据地址，除非显式白名单。

### 实施要点

1. 使用 IHttpClientFactory 和命名客户端。
2. 解析 DNS 后校验所有返回 IP，并限制重定向。
3. Provider/Webhook 支持租户级域名白名单。
4. 配置超时、重试、熔断、最大响应体和并发数。
5. `SsoOptions.RequireHttpsMetadata` 等开关必须真正进入运行逻辑。

### DBA 影响

可能增加允许域名、网络策略或外联审计配置字段。

### 验证与验收

- localhost、127.0.0.1、私网、链路本地和元数据地址默认被拒绝。
- DNS Rebinding 和重定向到内网被拒绝。
- 合法外部 OIDC 和 Webhook 正常工作。

## EA-027 健康检查、指标、日志归档与告警

### 问题

当前 `/health` 聚合所有依赖，未区分 liveness/readiness；detail 匿名暴露依赖信息；OpenTelemetry 主要只有 Trace，缺少业务指标和归档任务。

### 目标

- `/health/live` 只检查进程存活。
- `/health/ready` 检查必要依赖。
- 详细健康信息受权限或内网限制。
- 建立 Metrics、日志归档和告警基线。

### 指标建议

- 登录成功率、失败率、锁定次数。
- 401/403/429/5xx 数量。
- API 延迟分位数。
- EF 查询耗时和慢 SQL。
- Hangfire 队列长度与失败任务。
- Outbox 积压、重试和失败数。
- RabbitMQ Consumer Lag/DLQ。
- 文件存储空间和扫描失败。

### DBA 影响

日志归档可能新增归档表、分区或独立日志库。需要确定保留周期和合规要求。

### 验证与验收

- 数据库故障不会导致 liveness 失败并触发无意义重启。
- readiness 能正确阻止流量进入故障实例。
- 匿名用户不能查看依赖异常明细。
- 关键指标可被采集并配置告警。

## EA-028 Docker/生产部署安全加固

### 目标

- 明确 docker-compose 仅用于开发/集成环境，不能直接作为生产拓扑。
- 容器使用非 root 用户、只读文件系统、资源限制和持久化卷。
- SQL Server、Redis、RabbitMQ 不默认向公网暴露端口。
- 增加 restart policy、优雅停机和 Worker 独立扩缩容说明。

### 实施要点

1. API/Worker 镜像改为非 root 运行。
2. 前端 Nginx 增加静态资源缓存、压缩和安全头。
3. Local uploads 使用持久化卷；Production 使用对象存储。
4. 生产敏感配置使用 Secret，不通过普通 `.env` 长期保存。
5. 增加镜像版本固定、镜像扫描和 SBOM。

### DBA 影响

无直接业务表变更，但需要备份、恢复、迁移发布和数据库权限账号方案。

### 验证与验收

- 容器重建后业务数据和文件不丢失。
- 非 root、只读文件系统配置下服务正常运行。
- 内部基础设施端口没有对公网暴露。

## EA-029 未闭环能力的产品状态治理

### 问题

部分界面、模型和配置让用户认为能力可用，但实现仍为预留或 Demo。

### 处理原则

每个能力必须二选一：

1. 实现完整闭环并增加测试；或
2. 从生产 UI 隐藏/禁用，并明确标记 Preview/Reserved。

### 逐项子任务

- EA-029A MinIO：由 EA-018 处理。
- EA-029B MFA/密码过期：由 EA-013 处理。
- EA-029C SAML/通用 OAuth2：确认是否进入近期路线，未实现前隐藏。
- EA-029D API 报表数据源：实现或从可选类型中移除。
- EA-029E 定时任务：引入受控 Job Registry，或明确只保留 Demo。
- EA-029F 字段权限：确认业务模型后实现，未实现前不展示可编辑入口。
- EA-029G UserDataScope：完成规则或移除无效入口。

### DBA 影响

按具体子任务分别评审，不允许一次性混合迁移。

### 验证与验收

- 生产界面中不存在点击后必然抛 NotSupported 的功能。
- 文档、配置、UI 和实际运行能力一致。

## EA-030 API 版本治理与模块化单体边界

### 问题

当前是按技术层分项目的分层单体，所有模块共用 Application、Domain、DbContext 和 DI 入口，模块边界主要靠目录约定。随着 ERP/WMS 扩展，模块耦合会持续增加。

### 目标

- 建立稳定 API 版本和弃用策略。
- 建立模块依赖规则，但不立即拆微服务。
- 平台模块与未来 ERP/WMS 业务模块通过契约交互。

### 实施要点

1. 引入 `/api/v1` 或 Header Versioning，并制定兼容规则。
2. 增加 OpenAPI breaking-change 检查。
3. 定义模块清单、公开 Contracts、内部实现和禁止依赖方向。
4. 优先通过架构测试约束依赖，不立即拆分全部项目。
5. 新业务模块使用独立命名空间、注册入口和迁移组织。
6. 跨模块副作用优先使用领域事件/Outbox，不直接互相操作仓储。

### DBA 影响

短期无结构变更；长期可按模块使用 Schema 或迁移目录组织，但必须单独设计。

### 验证与验收

- API 破坏性变化能够在 CI 中被发现。
- Domain 不依赖 Infrastructure/Api。
- 业务模块不能直接访问其他模块内部服务或实体仓储。

## EA-031 CI/CD、自动化测试与前端工程化

### 问题

仓库未发现 CI/CD 配置；前端没有 test/lint 脚本；真实 SQL Server OAuth 测试依赖环境变量，当前常规测试会跳过。

### 目标

建立可重复的质量门禁。

### CI 最低步骤

1. 后端 restore/build/test。
2. SQL Server 集成测试，禁止在发布流水线静默跳过。
3. 前端 type-check/lint/unit test/build。
4. 关键 Playwright E2E：登录、401、403、菜单、强制下线、审批。
5. 数据库 migration script 生成与空迁移检查。
6. Docker Compose smoke test。
7. 依赖漏洞、Secret、容器和 SBOM 扫描。
8. OpenAPI 兼容性检查。

### 前端子任务

- EA-031A 增加 ESLint/Prettier 或项目确认的统一规则。
- EA-031B 增加 Vitest 及路由守卫、Token 刷新、权限 Store 测试。
- EA-031C 增加 Playwright 关键流程。
- EA-031D 将动态路由的大量字符串判断改为显式组件注册表。
- EA-031E 优化大 chunk、静态资源缓存和按模块分包。

### DBA 影响

CI 集成测试需要隔离测试数据库和自动清理策略，不得连接共享开发库或生产库。

### 验证与验收

- PR 未通过质量门禁不能合并。
- 发布流水线不会跳过真实数据库认证测试。
- 前端具备基础单元测试和关键 E2E。
- 构建产物体积有基线和阈值。

## 6. DBA 迁移批次建议

数据库变更不要与所有代码修复一次发布，建议按以下批次独立迁移：

### DB-01 租户生命周期

- Tenant 状态和初始化字段/记录表。
- 初始化历史数据默认标记为 Active/Completed。

### DB-02 授权版本与密码安全

- User SecurityStamp/PermissionVersion。
- LastPasswordChangedAt、MustChangePassword。
- 需要时增加 MFA 凭证与恢复码表。

### DB-03 Step-up 验证

- 验证码哈希、尝试次数、使用状态、会话绑定。
- 评估是否新增 StepUpSession 表。

### DB-04 并发字段

- WorkflowTask、WorkflowInstance、关键状态机业务实体 RowVersion。
- 根据后续范围扩展到配置实体。

### DB-05 文件状态

- FileStatus、ScanStatus、ScanMessage、PendingDelete 等字段。

### DB-06 Outbox/Inbox 增强

- AggregateId、EventVersion、OccurredAt、LockedAt 等可靠投递字段。

### DB-07 软删除唯一索引

- 必须逐表实施。
- 每张表在迁移前执行历史冲突检测。
- 每个迁移提供回滚限制说明。

### DB-08 日志归档

- 根据数据量选择归档表、分区表或独立日志库。
- 明确保留周期、查询范围和清理审批流程。

## 7. 每个修复项的验收模板

每个编号完成时 Reviewer 必须按以下模板给出结论：

### [Architect]

- 原问题是否已覆盖。
- 实际改动是否超出确认范围。
- 是否保持既有分层与模块边界。

### [DBA]

- 是否有数据库变更。
- 迁移、索引、历史数据和回滚是否安全。
- 是否存在锁表、长事务或兼容风险。

### [Developer]

- 改了什么。
- 新增或修改了哪些测试。
- 配置和文档是否同步。

### [Reviewer]

- 功能正确性。
- 认证授权与租户隔离。
- 并发和事务一致性。
- 敏感信息与审计。
- 构建、测试、lint、迁移验证结果。
- 明确给出“通过”或需退回修正的问题。

## 8. 推荐的首批实施范围

建议第一批不要直接重构完整登录前端，而是从能独立交付且风险边界清晰的项目开始：

1. EA-001 OpenIddict 生产证书配置。
2. EA-002 ForwardedHeaders、真实 IP 和生产 HTTPS。
3. EA-006 租户写入一致性校验。
4. EA-014 错误码与 HTTP 状态映射。
5. EA-015 审计日志独立事务。

上述五项仍应逐个实施、逐个验收，不建议合并为一个提交或一次数据库发布。

认证模型 EA-003 至 EA-005 需要先确认采用 BFF 还是纯 SPA；数据权限 EA-020 需要先确认角色范围和用户范围的业务合并规则；软删除索引 EA-022 需要逐表确认删除后是否允许复用编码。这三类问题在缺少确认时不得直接实施。

## 9. 当前验证基线

在本方案编制前的基线验证结果：

- 后端测试：70 个通过，4 个 SQL Server OAuth 集成测试因缺少测试连接环境变量而跳过。
- 前端生产构建：通过。
- 前端构建存在约 1.09 MB 大 chunk 警告。
- 未执行 Docker Compose 全链路、真实 SQL Server OAuth、并发压测、渗透测试和灾备演练。

后续每个修复项都应记录相对于该基线新增的测试和风险变化。

## 10. 最终完成标准

只有满足以下条件，才可以认为本轮企业级架构整改完成：

1. 所有 P0 项均完成并通过 Reviewer。
2. 租户越权、认证协议、报表 SQL、文件访问和工作流并发测试全部通过。
3. Production 不再使用开发证书、浏览器 Client Secret 或 Password Flow。
4. 用户/租户禁用及权限变更能够在明确时限内使会话失效。
5. 未闭环功能不再以生产可用状态展示。
6. Docker/生产部署具备持久化、密钥管理、健康检查和监控方案。
7. CI/CD 对后端、前端、数据库迁移、Docker 和安全扫描形成强制门禁。
8. 文档与当前实现保持一致。
