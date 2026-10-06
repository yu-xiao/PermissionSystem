# AIC-002：权限排障助手实现与验收记录

> 日期：2026-10-06
>
> 状态：待验收；代码实现与本地确定性验证完成，尚未完成目标部署环境验收。
>
> 确认依据：用户在本次会话回复“确认”，同意 [实施方案](ai-center-aic-002-implementation-plan.md) 的三类诊断、授权矩阵、共用计算和专用证据卡片。

## 改了什么 [Architect] [Developer]

- Application 新增 `IPermissionDiagnosticService`，支持 `Menu`、`Permission`、`DataScope`，默认查询自己；返回版本、目标、评估口径、时间、服务端结论、检查项、来源、限制和建议入口。
- 自查权限调用既有 `ICurrentUserService.HasPermission`；API 权限匹配及组合要求解析提取到 `PermissionEvaluation`。他人配置复用 `IUserCredentialValidator.GetAuthenticationStateAsync`，不读取/输出密码、Token、SecurityStamp 或会话标识。
- `CurrentUserAppService` 同时实现 `IUserMenuResolver`，当前菜单入口与诊断复用角色菜单关系、Visible、祖先展开及树构建。无菜单查看权限时不披露隐藏配置；有权限时可显示隐藏/缺失祖先及角色直接分配检查。
- `DataScopeService` 同时实现 `IDataScopeResolver`，原当前用户入口与明确服务端身份快照复用同一计算逻辑，保留用户覆盖、启用角色并集、默认本人、空范围等规则。补充范围来源标识及角色范围/部门展开的显式租户条件。
- 查询他人先检查完整权限矩阵及调用者最新认证快照的数据可见范围，再读取目标配置；跨租户、软删除、不存在、范围外和无代查权限的目标均返回同一不可查询错误。调用者停用、租户停用、过期超级管理员或部门身份拒绝继续诊断；同租户已授权停用目标仅返回停用证据。
- 新增 `POST /api/ai/permission-diagnostics` 和 `permission.diagnose` 只读工具；参数不接受 actor、租户或权限身份字段。继承工具超时、取消、审计和引用链路。一次返回一个目标，检查项最多 20 项、序列化结果上限 16,384 字符；必要时裁剪可选角色来源，保留结论及必要检查并标记截断。
- 工具注册采用主应用显式启用：`AddApplication` 调用 `AddAiCenterCore(includePermissionDiagnostics: true)`。外部 MCP 默认仍为原六个工具，未扩大外部工具暴露范围，也没有修改 AIC-001 的认证配置。
- 诊断结果使用服务端生成的类型/版本/RunId/InvocationId 封套，复用既有 Tool 消息 Content 保存。读取时检查消息角色、模型生成标志、会话与运行归属、租户、成功工具调用、输出摘要和当前授权；不解析助手文字为证据、不重算历史结论。过期内容及旧消息不产生卡片。
- 聊天运行和会话详情返回可选诊断列表；新增 `AiPermissionDiagnosticCard.vue`。卡片直接展示服务端事实、时间和限制；配置入口采用固定标识和路由白名单，并检查前端当前权限及实际注册路由。
- 新运行 Agent/Prompt 版本更新为 `2.1`；旧会话仍可读取。提示词要求澄清歧义、尊重服务端结论及限制，不修改既有授权语义。

## 数据影响 [DBA]

**无数据库结构变更、无 EF 迁移、无权限种子变更。**

证据沿用已有 `AiMessage.Content` 及其保留清理机制，不复制完整结果到保留时间更长的 CitationJson。领域实体、EF 映射及历史迁移未修改。新增响应字段为可选/空列表，旧会话保留纯文本兼容路径。

实现与本地验证阶段没有连接或修改真实业务数据库，没有应用迁移或部署，没有执行 commit/push，没有清理既有 `artifacts/`。随后用户明确要求先提交并推送 Git，后续统一验收；AIC-002 保持待验收。

## 验证了什么 [Reviewer]

环境：Windows、.NET 10、EF InMemory、ASP.NET Core TestHost、现有 Vue/Vitest 工具链。确定性测试不调用付费模型，不使用真实 OAuth 凭据，不读取业务生产库。

| 验证 | 实际结果 |
| --- | --- |
| 后端解决方案 `dotnet build backend/PermissionSystem.sln --no-restore -m:1` | 通过；0 错误，现有授权集成测试的 2 个弃用 API 警告，新增 HTTP 测试未引入相同警告 |
| 诊断、历史证据、会话、既有授权和数据范围筛选单元测试 | 101 项通过；覆盖实际授权一致性、通配符/大小写/组合权限、超级管理员、隐藏祖先、启用角色、用户覆盖、角色范围并集及空范围 |
| 同组安全与兼容测试 | 覆盖代查矩阵每个权限、跨租户、软删除、范围外目标、停用调用者/目标/租户、撤权和缩小范围、过期超级管理员身份、伪造参数/上下文、取消、超量参数、证据截断、历史封套篡改、未知版本、旧消息、模型解释与卡片结论不一致 |
| Controller HTTP 测试及既有 API 授权测试 | 11 项通过，其中新增诊断 Controller 契约测试 7 项：401、403、ApiResult/枚举契约和未知身份字段/非法绑定拒绝 |
| 前端 `npm run test:unit` | 20 项通过，其中新增卡片 6 项：结论/来源/未评估/截断展示、路由白名单、撤权、未注册路由、未知版本、HTML 文本转义 |
| 前端 `npm run lint` | 0 错误；既有 `UploadFile.vue` 2 个可选 prop 默认值警告 |
| 前端 `npm run build`（包含 `type-check` 和 bundle budget） | 通过；Element Plus vendor chunk 仍有体积提示，现有包体预算通过 |
| `git diff --check` | 通过；未添加依赖或无关改动 |

确定性筛选命令：

```powershell
dotnet test backend/PermissionSystem.UnitTests/PermissionSystem.UnitTests.csproj --no-restore -m:1 --filter 'FullyQualifiedName~PermissionDiagnostic|FullyQualifiedName~DiagnosticTool|FullyQualifiedName~DataPermissions|FullyQualifiedName~Authorization' --logger 'console;verbosity=minimal'
dotnet test backend/PermissionSystem.IntegrationTests/PermissionSystem.IntegrationTests.csproj --no-build --no-restore --filter 'FullyQualifiedName~PermissionDiagnosticApiTests|FullyQualifiedName~ApiAuthorizationIntegrationTests' --logger 'console;verbosity=minimal'
```

全量单元测试也已执行。默认 Windows 临时目录下最初有 5 项访问权限失败；仅在测试进程中将 TEMP/TMP 指向被 Git 忽略的 `backend/PermissionSystem.UnitTests/bin/Debug/net10.0/aic-002-temp` 后，4 项临时文件/目录测试恢复通过。测试结束恢复原 TEMP/TMP，未调整系统目录 ACL、证书信任或 TLS 校验，也未修改这些既有测试。

尚未通过的全量测试为 `AiHttpTransportTests.SendAsync_RejectsUntrustedHttpsCertificate`：`X509CertificateLoader.LoadPkcs12` 在 Windows 临时证书导入阶段发生 `CryptographicException: 拒绝访问`，尚未进入 TLS 断言。不能将其记为通过，也不能据此判定生产 TLS 已验收。

最终全量单元测试结果：**434 项中 433 项通过、1 项失败、0 项跳过**。筛选的 101 项本任务相关检查包含在这 434 项中，不是额外的 101 项；HTTP 的 11 项和前端的 20 项来自独立测试工程。

一次并行构建期间发生 MSBuild 子进程 `CreateProcess` 异常；改为单节点串行构建后构建和 HTTP 测试通过。该异常没有通过修改业务代码或弱化断言规避。

## 剩余风险与未验证项 [Reviewer]

代码审查结论：**通过本轮实现与安全边界复核；完整环境验收尚未通过，AIC-002 保持待验收。**

1. Windows 证书导入权限需环境管理员处理，然后重跑上述 TLS 测试及全量回归。本次未修改系统证书或权限配置。
2. SQL Server 上的实际查询、数据库规模及并发权限变更需要在明确隔离的目标环境验收；本次只执行 EF InMemory 及无数据库 HTTP 测试，没有自动沿用 AIC-001 的遗留测试库。
3. 真实供应商的工具选择、参数契约、解释质量和浏览器人工体验未验收；不把模型替身或组件测试当成真实供应商/部署通过证据。
4. 前端普通用户权限匹配仍与后端在大小写、通配符、组合要求上有差异；诊断如实分别显示，未改变现有系统规则。
5. 他人结果是当前配置推算；没有目标浏览器或真实业务资源上下文时不能解释全部操作失败或某条业务数据不可见的原因。
6. 新卡片及重新查询逐次鉴权；历史自然语言仍按原会话读取规则保留，不承诺撤权后追溯清除所有既有助手文字。
7. 历史卡片读取需重新检查目标及证据授权；长会话的实际查询成本需目标环境验证。未在本任务引入 AIC-003 的通用结果上下文或后台运行方案。

## 发布与回退

正式部署前完成隔离 SQL Server、真实供应商、浏览器体验及 TLS 环境复核。生产开放仍遵守既有 AI kill switch、AllowedTenantIds、业务权限和会话认证链。

代码回退不需要 Down 迁移或数据删除；旧代码隐藏 Tool 消息并忽略新响应字段，新增诊断封套沿用现有保留清理机制。未执行任何发布或回退操作。
