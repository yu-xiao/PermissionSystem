# AI 中心 P0 技术验证结论

## 1. 结论

P0 采用独立 `PermissionSystem.McpServer` Host，使用官方 C# MCP SDK 的 Streamable HTTP 实现。MCP Host 只承担协议、认证和调用适配，不包含业务流程，也不直接访问 `AppDbContext`。

技术基线：

| 组件 | 版本/决策 |
| --- | --- |
| Runtime | .NET 10 |
| MCP SDK | `ModelContextProtocol.AspNetCore` 2.2.0 |
| MCP 传输 | Streamable HTTP，stateless，端点 `/mcp` |
| OAuth/OIDC | OpenIddict 7.6.0 |
| Token 验证 | 独立机密客户端调用 OpenIddict introspection |
| MCP audience/scope | `permission-system-mcp` |
| 用户委托 Token | 5 分钟，不允许 `offline_access` |
| P0 Tool | `list_datasets` |
| P0 数据集 | 仅 `platform-capabilities`，分类为 Public |
| 模型协议 | OpenAI Compatible `POST /v1/chat/completions` 最小子集 |

当前组合已经通过项目编译和自动化测试，可以进入 P1 的内部只读问数 MVP。生产部署前仍必须完成真实模型供应商和完整 OAuth 链路的环境验收。

## 2. 身份与租户链路

1. PermissionSystem API 发行包含 `permission-system-mcp` scope/audience 的短期用户 Token。
2. MCP Host 使用 `permission-system-mcp-server` 机密客户端向 API 的 `/connect/introspect` 验证 Token。
3. MCP Host 只从 Token Claim 恢复 `UserId`、`TenantId`、`SessionId`、`SecurityStamp`、角色和权限。
4. 请求携带的 `X-Tenant-Id` 只能与 Token TenantId 相同，不能切换租户，包括超级管理员。
5. 每次 MCP 调用重新检查租户状态、用户会话和 SecurityStamp。
6. Tool 再次检查 `mcp:dataset:query` 权限和 Application 租户上下文。

API 资源服务器显式只接受 `permission-system-api` audience，MCP Host 显式只接受 `permission-system-mcp` audience，两个 Token 不能跨资源使用。授权服务器使用固定 `OpenIddict:Issuer`；生产值必须是 API 与 MCP Host 都可访问的统一 HTTPS 地址。

P0 不接受只有客户端身份、没有用户会话的 Token。外部服务 Actor 和客户端租户绑定属于 P4 范围。

## 3. Tool 契约

`list_datasets` 无输入参数，返回结构化内容：

- `data`：已批准的数据集描述。
- `source`：固定来源标识。
- `queriedAt`：UTC 查询时间。
- `isComplete`：结果完整性状态。
- `traceId`：调用链路标识。

P0 不暴露报表 SQL、数据库表名、View 名、连接串、任意 URL 或写操作。现有 Report Dataset 在 P1 完成逐项数据分级和权限矩阵后才能加入 AI Tool 目录。

## 4. 模型网关安全基线

OpenAI Compatible 客户端默认关闭。启用时必须配置：

- `Ai:OpenAiCompatible:BaseUrl`
- `Ai:OpenAiCompatible:ChatCompletionsPath`
- `Ai:OpenAiCompatible:ApiKey`
- `Ai:OpenAiCompatible:Model`
- `Ai:OpenAiCompatible:AllowedHosts`

控制项：

- 默认只允许 HTTPS。
- Base URL Host 必须精确命中 allowlist。
- 默认拒绝解析到 loopback、内网、link-local 或保留地址的 Host。
- 私有部署必须显式启用 `AllowPrivateNetwork`；开发 HTTP 必须显式启用 `AllowInsecureHttp`。
- API Key 只写入 `Authorization` Header，不写入请求模型、异常或日志。
- 调用支持 CancellationToken 和独立超时。
- 429、5xx、4xx、超时、网络错误和无效响应映射为稳定错误类型，不回传供应商响应正文。

DNS 校验和实际连接之间仍存在 DNS rebinding 时间窗口。生产外联应叠加网络出口代理、防火墙和 DNS 策略，不能只依赖应用层校验。

## 5. 配置与密钥

必须通过本地未提交配置、Secret Manager 或环境变量提供：

| 配置 | 用途 |
| --- | --- |
| `SeedData:McpIntrospectionClientSecret` | API 初始化 introspection 客户端 |
| `McpAuthentication:IntrospectionClientSecret` | MCP Host 调用 introspection |
| `McpAuthentication:ResourceUrl` | MCP 对外绝对 URL，用于 OAuth Protected Resource Metadata 与 401 Challenge；禁止从请求 Host 推导 |
| `Ai:OpenAiCompatible:ApiKey` | 模型供应商凭据 |

`SeedData:McpIntrospectionClientSecret` 与 `McpAuthentication:IntrospectionClientSecret` 必须使用同一个强随机值。仓库中的配置和 `.env.example` 不包含真实密钥。

MCP introspection secret 至少 32 个字符；生产建议使用密码学安全随机值并由 Secret Manager 注入。

建议至少每 90 天轮换 MCP introspection secret 和模型 API Key；发生泄漏、人员变更或供应商安全事件时立即轮换。轮换应通过双凭据或维护窗口完成，避免将旧值写入日志。

## 6. 模型供应商上线门槛

在启用真实 Provider 前，业务方和安全负责人必须确认：

- 部署地区与数据驻留地区。
- 输入、输出和 Abuse Monitoring 数据的保留周期。
- 企业数据是否用于训练，以及相应的关闭方式和合同条款。
- Confidential 数据是否允许发送，字段脱敏清单和阻断规则。
- Token 单价、预算、配额、告警阈值和费用责任人。
- API Key 创建、保管、使用主体、轮换和吊销流程。

未完成以上确认时，`Ai:OpenAiCompatible:Enabled` 必须保持 `false`。

## 7. 数据库评审

P0 无数据库表结构变化，无 EF Core Migration。

数据变化仅包括开发初始化数据：

- 新增 `mcp:dataset:query` 权限并授予默认超级管理员。
- 更新 `permission-admin` 客户端允许请求 MCP scope。
- 新增 `permission-system-mcp-server` introspection 客户端。

回滚时可停用 MCP Host、移除客户端和权限初始化数据；不涉及业务表回滚。

## 8. 已验证与限制

自动化验证覆盖：

- Tool 权限、租户一致性和 Public 数据集白名单。
- MCP 用户委托身份、Tenant Header 一致性和服务 Actor 拒绝。
- 模型成功响应、Token 用量、429、5xx、4xx和调用取消。
- 供应商响应正文和测试密钥不进入异常。
- MCP Host 可独立启动，`/health/live` 返回 200，未认证 `/mcp` 返回 401。

尚需环境验证：

- API 与 MCP Host 的完整 Token 发行、introspection、会话撤销链路。
- 停用租户后的在线拒绝。
- Docker 内部 HTTP authority 的网络隔离，以及生产 HTTPS/mTLS 终止方式。
- 真实 OpenAI Compatible Provider 的协议差异、数据政策和限流行为。
- Redis 分布式限流在 MCP Tool 维度的 P1 配置。

## 9. 退出条件

出现以下任一情况时不得进入 P1 上线：

- MCP SDK 无法稳定支持目标客户端的 Streamable HTTP 协议版本。
- introspection 不能恢复完整用户、租户、会话和权限 Claim。
- 会话撤销或租户停用不能在下一次 Tool 调用生效。
- 模型供应商不能满足数据驻留、不训练、保留或密钥管理要求。
- 生产环境不能为 MCP 和模型外联提供受控网络边界。
