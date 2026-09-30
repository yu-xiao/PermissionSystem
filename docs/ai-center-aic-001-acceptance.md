# AIC-001：本地隔离环境验收记录

> 日期：2026-09-27。状态：阻塞（真实 MCP introspection 失败；认证修复方案待确认）。
> 用户确认范围：本地隔离环境，包含外部 MCP；随后指示继续。
> 代码基线：`0dc8c4ad10c037d29ffd23c379039af7b6dcf9c3`。
> 本文区分仓库自动化验证、本地端到端验收和仍需管理员提供的外部证据；不代表生产发布通过。

## 1. 实施方案 [Architect]

1. 复核安全修复、迁移、Swagger 和 MCP 现有实现，运行相关单元测试及 Worker 构建。
2. 新增可重复执行的本地验收脚本 `scripts/aic-001-local-acceptance.ps1` 与 HTTP 验收用例 `scripts/aic-001-http-checks.ps1`，复用现有应用、迁移及 MCP 契约／压测脚本，不修改业务实现、不新增依赖。
3. 每次创建独立命名的 SQL Server LocalDB 实例和测试库；Redis 使用已有可执行文件启动独立进程、独立端口，仅绑定回环地址。API/MCP 使用独立配置目录和端口，不读取既有运行环境的凭据。
4. 离线生成、审查前置及增量迁移 SQL，并记录源码与脚本 SHA-256。在全新库用 EF migrator 分别执行前置迁移操作，备份后执行目标增量 SQL，并验证恢复副本。保留数据库与备份供复核，不执行 Down 或删除已有数据。
5. 用真实 HTTP 验证 Swagger PKCE、精确回调、租户参数、MCP OAuth Token/introspection、数据集契约、跨租户拒绝、会话撤销和停用租户；开启真实 Redis 缓存、分布式锁和限流。
6. 汇总脱敏结果、实际覆盖与未决项，同步本计划和原安全修复文档。发现业务修复需求时先记录、评估范围，不能用测试绕过安全约束。

预计改动：本文件、验收脚本、`ai-center-next-development-plan.md`、`ai-center-p1-security-hardening.md`、`ai-center-p2-security-hardening.md`、`ai-center-mcp-implementation-plan.md`。

## 2. 数据影响及回退 [DBA]

- 无新增数据库结构设计、实体或迁移；只在本轮创建的隔离库执行仓库已有迁移。
- 仓库共有 43 个迁移；首个为 `20260512060212_InitialCreate`，目标为 `20260924032728_AddAiUsageEstimates`，直接前置为 `20260831092535_AddMcpDatasetSchemaGovernanceP4`。源文件存在不证明目标库已经应用。
- 目标 Up 仅向 `ai_usage_log` 增加 `EstimatedInputTokens`、`EstimatedOutputTokens` 两个可空 int 列，无默认值和历史回填；Down 删除这两列，不能自动用作回退。
- 先完成前置迁移，再制作备份；目标迁移失败时停止后续启动，使用另一个恢复副本检查备份可用性。已有业务数据库不在执行范围内。
- 实际部署须先迁移，再协调更新 API 和 Worker；应用回退保留新增列。隔离库演练不能代替目标部署的备份、恢复和版本证据。

### 迁移执行发现

- sqlcmd 默认会话的 `QUOTED_IDENTIFIER` 不满足过滤索引要求；验收入口统一使用 `sqlcmd -I`。
- 全量幂等脚本与普通顺序脚本均实测在 `20260525010100_AddBuiltinProtectionFlags` 报 `Invalid column name 'IsBuiltin'`。原因是生成脚本将添加列和引用新列的 UPDATE 放在同一 SQL 编译批次中；该发现不是 `AddAiUsageEstimates` 自身的问题。
- 本轮不修改历史迁移。隔离新库前置迁移改由 EF migrator 按原操作边界执行；经审查的目标增量 SQL 仍直接执行。相关源码、生成 SQL 的哈希均在执行前核对。
- 已有环境不能直接照搬从 0 开始的全量脚本。DBA 应读取实际迁移历史、按真实起点生成升级脚本，并单独解决适用的批次编译问题；本地 migrator 通过不表示历史全量 SQL 问题已修复。

## 3. 环境及证据

| 项目 | 本轮观测 |
| --- | --- |
| 操作系统／工作区 | Windows；`E:\Projects\PermissionSystem` |
| .NET SDK／EF CLI | 10.0.401／10.0.7 |
| LocalDB 可用版本 | SQL Server 2019（15.0.4382.1）、SQL Server 2025（17.0.4025.3） |
| 已有服务 | SQL Express、Redis 正在运行；不直接复用其业务数据 |
| Docker | 当前 PATH 未找到；使用本机已安装 LocalDB 和 Redis |
| SQL 集成测试变量 | `PERMISSION_SYSTEM_SQLSERVER_TEST_CONNECTION` 本轮初始未设置；未输出连接值 |
| 单元测试证据 | `artifacts/aic-001/aic-001-unit.trx`，本机生成 |
| 已审查迁移证据 | `artifacts/aic-001/aic001_e28188f43a404e58a718facac178d806/`：前置 SQL、增量 SQL、SHA-256 manifest、离线检查结果 |
| 最新本地验收证据 | `artifacts/aic-001/aic001_2f93550e4a364cc4bfbdc4b13e35d22b/result.json`：22 个检查通过，1 个失败，后续用例未执行 |
| 独立规则复现证据 | `artifacts/aic-001/introspection-audience-proof.json`：当前 audience 被 OpenIddict 校验器拒绝；建议接收方配置通过该规则 |

## 4. 当次验证 [Developer]

2026-09-27 从仓库根目录执行：

```powershell
dotnet test backend/PermissionSystem.UnitTests/PermissionSystem.UnitTests.csproj --filter 'FullyQualifiedName~AiCenter|FullyQualifiedName~SwaggerOAuthTests|FullyQualifiedName~AuditPersistenceTests' --logger 'trx;LogFileName=aic-001-unit.trx' --results-directory artifacts/aic-001 --no-restore
dotnet build backend/PermissionSystem.Worker/PermissionSystem.Worker.csproj --no-restore
```

- 单元测试：163 个执行，163 个通过，失败 0、跳过 0。包括地址固定、TLS 拒绝、禁止重定向、Swagger 公共客户端配置、工具调用、429/5xx、超时/取消、估算结算、watchdog 和 MCP 调用者校验。
- Worker：构建通过，0 警告、0 错误。
- 上述模型测试使用替身，网络测试使用本机合成服务，预算／MCP 测试不等同于真实 SQL Server/Redis 的端到端验收。
- 现有 `OAuthSqlServerIntegrationTests` 显式关闭 Redis；其运行结果不能证明 MCP 的真实 Redis、introspection 和进程间撤销链路。

本地隔离入口（PowerShell 7，仓库根目录；不在参数中传入密钥）：

```powershell
pwsh -NoProfile -File scripts/aic-001-local-acceptance.ps1 -PrepareOnly
# 人工审查 PrepareOnly 输出目录中的 SQL，再传入该目录；下方是本轮实际目录。
pwsh -NoProfile -File scripts/aic-001-local-acceptance.ps1 -ReviewedMigrationDirectory artifacts/aic-001/aic001_e28188f43a404e58a718facac178d806 -RedisServerPath 'D:/Program Files/Redis-8.10.1-Windows-x64-msys2-with-Service/redis-server.exe'
```

当次环境验证结果：

- 离线构建 API、MCP、Worker，生成迁移 SQL，检查 EF 模型无未迁移变更：通过。
- SQL Server 2025 LocalDB 独立实例：42 个前置迁移、目标增量迁移、两个可空 int 列检查通过，最终历史记录 43 条。
- 在迁移目标版本前执行 COPY_ONLY/CHECKSUM 备份及 VERIFYONLY，恢复到另一个新库；恢复副本仍为 42 个迁移：通过。此为测试库结构恢复，不是生产数据恢复验收。
- 独立 Redis 回环进程、API 和 MCP readiness：通过；缓存和限流配置为 Redis。Worker 使用相同隔离库启动，但后台租户初始化用例尚未执行到。
- Swagger 真实 HTTP 表单登录（含 Cookie、antiforgery、授权票据）、用户同意、PKCE 无密钥换取 Token、精确回调与 state、防重放：通过。无注册回调与未知租户被拒绝。未声称浏览器手工体验或生产反向代理回调已验收。
- MCP 委托 offline_access 被拒绝、正常委托 Token 不含刷新令牌：通过。
- MCP introspection：HTTP 200、active=false；独立无 Cookie 的 Bearer HTTP 客户端调用 MCP：HTTP 401。该正向认证链路失败，验收脚本以退出码 1 停止。
- 客户端管理、字段与数据集拒绝、撤销会话、停用租户、双 MCP 实例共享 Redis 限流和 50 请求负载用例已写入脚本，但因前置失败未执行，不计为通过。现有契约／压测脚本本轮同样未执行到。
- 两个新增 PowerShell 脚本语法解析通过（0 错误）。未对前端作改动，本轮不以未执行的前端构建作验收证据。
- 本轮创建的应用／Redis 进程及 LocalDB 实例已停止，原 SQL Express／Redis 服务保持运行；未执行清库、Down、提交或推送。测试数据库和备份保留供复核，凭据仅在生成它们的进程内使用，不写入验收记录。

## 5. 八项验收矩阵

| 事项 | 当前状态与证据 | 待补条件／责任角色 |
| --- | --- | --- |
| 目标迁移及前置迁移 | 本地通过：43 条迁移历史，目标两个可空 int 列存在 | 本轮执行者；真实部署由 DBA/发布负责人确认 |
| 迁移脚本、备份、恢复 | 本地通过：审查、哈希校验、增量 SQL、备份及独立恢复副本；历史全量 SQL 批次问题另列 | 本轮执行者；生产恢复目标另行确认 |
| 历史 OAuth 密钥轮换 | 未核实；新测试凭据不证明旧密钥已失效 | 凭据管理员（具体人员待指定）提供轮换时间、影响客户端、旧凭据失效证据，不提供密钥值 |
| Swagger PKCE、回调、租户、登录 | 本地通过：单元与真实 HTTP 链路；具体用例见第 4 节 | 本轮执行者；真实部署回调仍需环境管理员核对 |
| 真实供应商、Tool Calling、异常、usage、预算、取消 | 替身测试通过；真实供应商未验收 | 供应商配置及费用负责人（待指定），提供受控测试模型和预算 |
| 固定目标 IP、TLS、禁重定向实际兼容性 | 合成 TCP/TLS 测试通过；真实出站路径未验收 | 网络／供应商负责人（待指定）；需要直连的最终 URL、有效证书及 DNS/防火墙条件 |
| SQL Server/Redis 外部 MCP 安全链路 | 阻塞：正向 introspection 返回 active=false，MCP 返回 401；后续拒绝路径未执行 | 认证维护负责人（待指定）确认最小修复，本轮执行者负责复验 |
| 外部客户端、并发限流、压测、告警 | 因前置认证失败未执行到；第三方客户端矩阵、告警投递和业务性能门槛仍缺输入 | 发布／客户端／运维负责人（待指定）；脚本默认阈值不能作为已批准 SLO |

## 6. 关闭规则 [Reviewer]

- 本地演练通过只能关闭其实际覆盖的用例；真实供应商、旧凭据轮换、第三方客户端、实际 TLS/出站路径及告警投递缺证据时保持待验收。
- 单元测试通过或条件集成测试跳过不算环境通过；每条端到端结果必须包含实际环境、执行时间和可追溯结果。
- 本轮没有生产发布授权；外部 MCP 在生产等价认证、租户隔离、限流、性能和告警验收完成前不对不受控网络开放。
- 本轮结果与待办补齐后再决定 AIC-001 状态，不提前标记已完成。

## 7. 实测阻断与修复方案 [Architect] [DBA]

### MCP introspection 接收方不匹配

- 实测：成功签发的 MCP 委托 Token，使用已注册的专用 introspection 客户端查询时，HTTP 200 但 `active=false`。
- 代码：ConnectController.ConfigureTokenResources 仅设置资源 `permission-system-mcp`；注册的 introspection 客户端是 `permission-system-mcp-server`。
- 使用已安装 OpenIddict 7.6.0 的 ValidateAuthorizedParty 实际校验器复现：带 `permission-admin` presenter、只有当前资源 audience 的访问令牌被拒绝，错误为 `invalid_token`；加入专用 introspection 客户端作为 audience 后，该检查通过。此复现使用合成 ClaimsPrincipal，不签发凭据、不访问业务数据库。证据：`artifacts/aic-001/introspection-audience-proof.json`。
- 建议最小修复：只在 MCP Token 资源配置中保留原资源并加入专用 introspection 客户端；API Token 不变。保留已有认证、audience、scope、PKCE、租户、会话校验，不关闭 OpenIddict 校验器。
- 测试策略：覆盖委托／服务 Token、API-only Token 和无关 introspection 客户端的允许／拒绝；修复后重跑完整本地隔离验收。
- 数据影响：无新增数据库迁移；修复前签发的 Token 不能原地变更，需重新签发。
- 确认状态：已向用户提出认证修复范围确认，未得到确认前不修改认证业务代码。

### 验收脚本修正记录

- LocalDB 启动进程可能将重定向输出管道继承给 SQL Server，脚本在启动器退出后使用有界输出等待，避免一直等待数据库进程退出。
- 未知租户请求通过已登记回调返回 OAuth `invalid_request` 是有效拒绝；验收同时校验回调地址和错误值，不能仅用 HTTP 400 判断。
- 现有系统明确禁止 MCP 委托请求 `offline_access`；用例采用无刷新令牌的委托 Token，并将禁止 offline_access 作为独立安全检查。
- Swagger 浏览器 Cookie 与外部 MCP Bearer 客户端分离。复用浏览器 Cookie 的早期探测曾出现 HTTP 431，该结果不能归因于 MCP 的授权判定。
