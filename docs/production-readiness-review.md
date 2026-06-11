# Production Readiness Review

审查日期：2026-06-08

审查范围：基于当前仓库代码、配置、Docker Compose、README、测试项目与前端路由实现进行静态生产就绪审查。未新增业务功能，未做重构。

## 总体结论

当前项目已经具备较完整的企业权限平台骨架：后端分层清晰，RBAC 权限模型、OpenIddict、SSO、审计日志、缓存抽象、RabbitMQ 可选消息总线、Hangfire 后台任务、Docker Compose 和自动化测试均已落地。

但从生产上线角度看，仍不建议直接上线。主要阻断点集中在生产密钥/证书策略、前端 confidential client secret 暴露风险、Docker 生产化参数校验、公开健康明细、以及部分查询/索引与测试覆盖不足。

当前生产就绪评分：78 / 100。

## 构建验证

- `dotnet build`：首次在 `backend` 目录执行时失败，原因是本机已有 `PermissionSystem.Api (PID 27276)` 进程锁定 `backend/PermissionSystem.Api/bin/Debug/net10.0/*.dll`，不是代码编译错误。
- `dotnet build -p:BaseOutputPath=E:\Projects\PermissionSystem\.dotnet-build\`：通过，0 警告，0 错误。
- `npm run build`：在 `frontend/permission-admin` 目录执行通过。Vite 提示 `request-*.js` chunk 超过 500 kB，属于构建体积警告，不阻断构建。

## 检查项结论

| 序号 | 检查项 | 结论 | 说明 |
| --- | --- | --- | --- |
| 1 | 架构分层是否清晰 | 达标 | `Api`、`Application`、`Domain`、`Infrastructure`、`Shared`、`Worker` 分层清楚。Controller 基本只做入口编排，业务逻辑集中在 Application，EF/Redis/RabbitMQ/Hangfire 位于 Infrastructure。 |
| 2 | 权限系统是否安全 | 基本达标 | 使用 `[Permission]` + `PermissionAuthorizationHandler`，SuperAdmin 通过角色 claim 放行。业务 API 权限码覆盖良好。风险是权限变更后普通用户 token 内权限 claim 需要重新登录才完全刷新。 |
| 3 | admin / SuperAdmin 是否被保护 | 达标 | `admin`、`SuperAdmin` 有内置标识与服务层保护；禁止删除/禁用 admin，禁止移除最后一个 SuperAdmin，敏感变更要求二次校验。已有 `BuiltinProtectionTests` 覆盖关键常量与内置标识。 |
| 4 | OAuth2 / SSO 是否安全 | 中风险 | 使用 OpenIddict 官方实现，支持 password/refresh/client_credentials/authorization code + PKCE。SSO state/nonce/login_code 有一次性消费与过期控制。但服务端仍使用开发签名/加密证书，前端配置了 confidential client secret，SSO HTTPS 元数据要求默认关闭。 |
| 5 | 审批流是否可扩展 | 基本达标 | WorkflowDefinition/Node/Edge/Condition/Instance/Task/Record/Cc/BusinessBinding 模型完整，并通过业务 handler 对接示例审批单。可扩展性较好。后续需要更多复杂流程测试。 |
| 6 | 缓存、RabbitMQ、Hangfire 是否可选启用 | 基本达标 | Cache 支持 Memory/Redis；RabbitMQ 默认关闭并注册 `NullMessageBus`；消费者和 Outbox Publisher 可分别开关。Hangfire 存储总是注册，Worker 启动后执行任务，尚缺显式禁用 Hangfire 的总开关。 |
| 7 | Docker Compose 是否可运行 | 基本达标 | Compose 包含 SQL Server、Redis、API、Worker、Frontend，RabbitMQ 使用 `mq` profile。运行依赖 `.env` 中必填密码、连接串、密钥；默认 Redis 总是启动。未实际执行 `docker compose up`。 |
| 8 | 配置文件是否存在生产安全风险 | 高风险 | `AllowedHosts` 为 `*`；OpenIddict 使用开发证书；`Sso:RequireHttpsMetadata=false`；Docker 环境名为 `Docker` 会关闭传输安全要求；前端构建参数包含 `VITE_OAUTH_CLIENT_SECRET`。 |
| 9 | 是否有自动化测试 | 基本达标 | 有 xUnit 测试项目，覆盖内置保护、SSO 安全、报表 SQL 安全、编号、打印、开放集成、状态流转、Demo 审批集成。缺少 API 授权集成测试、前端路由守卫测试、Docker smoke test。 |
| 10 | 是否有敏感信息泄露 | 基本达标 | 未发现已提交的真实密码、token、连接串。示例文件留空，操作日志对 password/secret/token 等字段做脱敏。风险是前端 OAuth client secret 会进入构建产物，且健康明细公开可能暴露内部错误信息。 |
| 11 | 是否有缺失的索引 | 中风险 | 63 个 EF 配置文件均存在索引配置，主路径覆盖较好。建议补充 `Users(TenantId, Email)`、`Users(TenantId, PhoneNumber)` 以支撑 SSO 自动绑定精确匹配，并结合生产慢查询继续评估日志表归档/分区。 |
| 12 | 是否有 N+1 查询风险 | 中风险 | 多数查询为显式批量读取，但 `UserService.ToResponse` 在分页用户列表中逐用户查询角色，存在 N+1 风险。部分服务存在 `ToList().Select(...)` 后内存关联，数据量增大后需要优化。 |
| 13 | 是否有接口缺少权限码 | 达标 | 静态扫描结果显示，除 `/connect/token`、`/connect/revoke`、`/connect/logout` OAuth 端点外，业务 Controller action 均有 `[Permission]`、`[Authorize]` 或 `[AllowAnonymous]`。 |
| 14 | 是否有前端路由未加权限保护 | 基本达标 | 全局路由守卫要求 token；动态菜单路由带 `permissionCode`；隐藏详情/设计器路由多数带权限码。`Dashboard`、`AccountProfile`、错误页仅要求登录或公开，符合常规预期。 |
| 15 | 是否有 README 与实际不一致 | 基本达标 | README 与当前实现大体一致，覆盖本地密钥、Docker、RabbitMQ、SSO、内置保护等。需补充生产证书、前端 OAuth secret 不应公开、公开 health detail、Hangfire 无总开关等上线注意事项。 |

## 已达标项

- 后端分层与 AGENTS.md 基本一致：Api 不直接访问 DbContext，Application 编排用例，Domain 保持实体/枚举，Infrastructure 承载 EF、缓存、消息、任务、外部集成。
- 所有 Domain 实体继承 `BaseEntity`，`AppDbContext` 统一处理软删除、租户过滤和审计字段。
- RBAC 权限模型完整，权限码通过 claim 校验，SuperAdmin 有统一特权判断。
- admin / SuperAdmin 保护已在 UserService、RoleService、DataScopeService、SSO 映射中体现。
- OpenIddict 官方实现已接入，没有自研 JWT 服务。
- 登录失败记录、账号策略、IP 访问规则、敏感操作二次验证、用户会话撤销、操作日志脱敏均已实现。
- SSO OIDC 实现包含 state、nonce、PKCE、id_token issuer/audience/lifetime/signing key 校验，login_code 一次性消费。
- RabbitMQ 默认关闭，关闭时使用 `NullMessageBus`，不会强依赖 MQ。
- Cache 默认 Memory，可切换 Redis；Redis 仅在配置启用时注册连接。
- Docker Compose 覆盖主要运行组件，并通过环境变量注入敏感配置。
- EF 配置广泛设置索引，关系表、日志表、工作流表、SSO 表均有关键索引。
- 已有后端自动化测试项目，覆盖了部分安全与平台能力核心逻辑。

## 高风险问题

1. 生产环境仍使用 OpenIddict 开发证书。
   - 位置：`backend/PermissionSystem.Api/Program.cs`
   - 现状：调用 `AddDevelopmentEncryptionCertificate()` 与 `AddDevelopmentSigningCertificate()`。
   - 风险：生产 token 签名/加密密钥不可控，不满足证书轮换、备份与合规要求。
   - 建议：生产使用受管证书或密钥材料，通过环境变量/Key Vault/证书存储注入，并保留开发证书仅限 Development。

2. Docker 环境会关闭 OpenIddict 传输安全要求。
   - 位置：`backend/PermissionSystem.Api/Program.cs`
   - 现状：`Development` 或 `Docker` 环境执行 `DisableTransportSecurityRequirement()`。
   - 风险：若 Docker Compose 被直接用于公网或准生产，token 端点可在 HTTP 下工作。
   - 建议：区分 `DockerDevelopment` 与 `Production`，生产容器必须由 HTTPS 反向代理终止 TLS，并保留安全要求。

3. 前端包含 confidential OAuth client secret。
   - 位置：`frontend/permission-admin/.env.example`、`docker-compose.yml`、`src/api/auth.ts`、`src/utils/request.ts`
   - 现状：`VITE_OAUTH_CLIENT_SECRET` 会进入浏览器构建产物。
   - 风险：浏览器端无法保密 secret，等同公开；当前 seed client 类型为 `Confidential`，与 SPA 形态不匹配。
   - 建议：SPA 改用 Authorization Code + PKCE public client；移除前端 secret；password flow 如保留，仅限可信后端代理或内部过渡方案。

4. 生产 SSO HTTPS 元数据要求默认关闭且未在 OIDC 客户端中强校验。
   - 位置：`backend/PermissionSystem.Api/appsettings.json`、`backend/PermissionSystem.Infrastructure/Sso/OidcClientService.cs`
   - 现状：`Sso:RequireHttpsMetadata=false`，`OidcClientService` 未读取该选项阻止 HTTP metadata/authority。
   - 风险：错误配置可能允许非 HTTPS IdP 元数据，增加中间人攻击面。
   - 建议：生产默认必须要求 HTTPS metadata，并在服务层校验 metadata/authority scheme。

## 中风险问题

1. 公开健康明细可能暴露内部信息。
   - 位置：`HealthController.GetDetailAsync`
   - 现状：`[AllowAnonymous]` 类级别开放，`/health/detail` 返回 entries、exception message、data。
   - 建议：摘要健康检查可匿名，detail 应加权限、内网限制或仅开发环境开放。

2. Hangfire 没有总开关。
   - 位置：`DependencyInjection.AddHangfireInfrastructure`、`Worker.Program`
   - 现状：Hangfire 存储和 Dashboard 始终注册，Worker 启动即注册 server。
   - 建议：新增 `Hangfire:Enabled` 配置，关闭时禁用 Dashboard/Worker/server，并让健康检查返回 disabled。

3. 用户分页存在 N+1 查询风险。
   - 位置：`UserService.GetPagedAsync` / `ToResponse`
   - 现状：分页取用户后，每个用户在 `ToResponse` 中查询 roleIds 和 roleCodes。
   - 建议：分页后批量查询 UserRoles/Roles 后组装响应。

4. 权限变更对已签发 access token 不即时生效。
   - 位置：权限 claim 签发与 README 说明一致。
   - 现状：缓存会清理，但普通用户权限码也嵌入 access token。
   - 建议：缩短 access token 生命周期、权限变更后撤销受影响会话，或改为服务端实时权限版本校验。

5. Docker Compose 缺少生产启动前硬校验。
   - 现状：`.env.example` 留空是安全的，但 Compose 本身不会明确阻止弱密码/空密钥。
   - 建议：增加启动说明和 CI smoke test，生产使用 secrets 管理，不直接依赖 `.env`。

6. 测试覆盖偏后端单元/轻集成。
   - 现状：缺少完整 API 授权集成测试、前端构建后的权限路由测试、Docker Compose smoke test。
   - 建议：上线前至少补充关键 Controller 401/403/200 流程和前端守卫用例。

## 可后置优化项

- 为 `Users(TenantId, Email)`、`Users(TenantId, PhoneNumber)` 增加索引，支撑 SSO 自动绑定。
- 对 LoginLog、OperationLog、ExternalApiCallLog、WorkflowRecord 等高增长表设计归档或分区策略。
- 将用户列表、角色矩阵等高频查询统一改为批量查询/投影 DTO，减少内存关联和同步 EF 查询。
- 把 SSO provider metadata discovery 增加缓存，避免每次 challenge/callback 都重新拉取元数据。
- 为 RabbitMQ enabled 模式增加连接失败降级策略和更完整的重试观测指标。
- 前端动态路由的 component 映射较长，可后续收敛为白名单表，但不影响当前生产阻断判断。
- 当前部分中文源码显示为乱码，建议统一文件编码为 UTF-8，避免构建产物或日志文案异常。

## 上线前必须完成项

1. 替换 OpenIddict 开发签名/加密证书，建立生产证书配置、轮换和备份流程。
2. 移除浏览器端 `VITE_OAUTH_CLIENT_SECRET`，将管理后台 OAuth 客户端调整为适合 SPA 的 public client + Authorization Code + PKCE，或通过后端代理承载 secret。
3. 生产环境禁止 `DisableTransportSecurityRequirement()`，并确认 TLS 终止、HSTS、反向代理头处理策略。
4. 将 `Sso:RequireHttpsMetadata` 在生产设为 true，并在 OIDC 客户端服务中强制 HTTPS metadata/authority。
5. 限制 `/health/detail` 访问范围，避免匿名暴露内部异常和依赖详情。
6. 明确 Hangfire 是否可选；若生产不需要 Worker，增加禁用开关或部署分离策略。
7. 补充 API 授权集成测试：至少覆盖普通用户无权限 403、未登录 401、SuperAdmin 放行、敏感操作二次验证。
8. 对 Docker Compose 做一次真实 smoke test，验证 `.env` 必填项、迁移、种子数据、前后端联通、Redis/RabbitMQ 开关路径。
9. 审核生产配置：`AllowedHosts`、CORS、Admin 初始密码、OAuth secret、系统配置加密密钥、RabbitMQ 密码、SQL Server 密码均必须由安全渠道注入。

## 敏感信息审查

- 未发现仓库中提交真实数据库连接串、真实 token、真实密码、私钥或证书。
- `.env.example`、`appsettings*.json` 中敏感值为空，占位方式合理。
- `OperationLogMiddleware` 会递归脱敏 JSON 中的 password、secret、token、client_secret 等字段。
- 仍需注意：前端 `VITE_*` 变量是公开构建时变量，不能承载任何真实 secret。

## 权限覆盖审查

静态扫描 Controller action：

- 业务 API 均发现 `[Permission]`、`[Authorize]` 或 `[AllowAnonymous]`。
- 仅 `/connect/token`、`/connect/revoke`、`/connect/logout` 未使用权限码，属于 OAuth 协议端点，应由 OpenIddict 客户端认证、grant 校验、token/session 撤销逻辑保护。
- 建议上线前用集成测试固化权限覆盖，避免新增 Controller 时遗漏权限码。

## README 一致性

README 与当前实现总体一致，尤其是 Docker、RabbitMQ 可选、SSO、权限矩阵、内置账号保护说明基本匹配。

建议补充或更正：

- 生产不能使用 OpenIddict development certificate。
- SPA 前端不能保存 OAuth confidential client secret。
- `/health/detail` 当前匿名开放，生产需限制。
- Hangfire 当前没有总开关，不完全等同“可选启用”。
- 权限变更后普通用户需要重新登录或撤销会话，才能确保 token claim 立即更新。
