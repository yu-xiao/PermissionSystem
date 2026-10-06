# AIC-003：结构化结果与连续追问实现及验收记录

> 日期：2026-10-06
>
> 状态：待验收；首批实现、本地确定性验证及代码复核完成，尚未完成目标环境验收。
>
> 确认依据：用户回复“确认”，同意 [实施方案](ai-center-aic-003-implementation-plan.md)。任务范围与状态同步于 [后续开发计划](ai-center-next-development-plan.md)。

## 改了什么 [Architect] [Developer]

- 首批四个只读工具支持三种 v1 结果：`permission.diagnose` 的权限证据、`permission.users.search` 的用户表格、登录／操作日志摘要的统计卡片。条件、口径、查询时间、来源由服务端生成；匹配总量与展示行／分组数分别表达。操作日志模块前 20 组现在明确给出总分组数与截断状态。
- Application 新增 `AiStructuredResultModels`、`AiStructuredResultReader`、`AiFollowUpContextService` 和 `AiQueryAccessGuard`，复用既有最新认证状态、权限计算及数据范围。执行与历史读取均检查当前授权；用户表格校验范围指纹及批量行可见性，菜单删除后隐藏对应历史证据。
- 同一结果及最小参数上下文存于现有 Tool 消息封套；校验租户、所有者、会话、Run、成功 Invocation、支持版本、消息摘要、整个封套的调用输出摘要及输入引用摘要。参数、来源与结果均受完整性校验，旧助手文字和旧 Tool 原始 JSON 不产生可信上下文。
- 可选 `contextRef` 定位当前会话的合法结果；缺省参数继承实际历史条件，白名单补丁仅替换出现的字段，可选过滤支持 null 清除。失效或不匹配引用不能回退为无原过滤的新查询。精确示例“只看本部门”“再看上个月”由服务端强制只改变预期条件。
- 本部门仅指调用者当前有效部门，与最新授权范围取交集，不含下级；无部门或部门不可用时拒绝退化为全量查询。日志仍采用用户名／模块包含匹配、时间闭区间和最长 31 天，归一化的绝对起止值可供重查。
- 上个自然月以当次服务端时间为锚，必须使用用户在请求中明确选择的 UTC 偏移；结束值为当月起点减一 tick。缺时区或对象歧义时返回服务端非模型澄清消息，不执行工具查询。已经发生的模型调用仍记录使用量与成本。
- Agent／Prompt 更新为 2.2；模型历史只含用户文字及经校验的最小参数上下文，不使用历史助手文字或结果正文作为当前事实。请求引用、UTC 偏移和请求消息 ID 独立保存为最小 Tool 元数据；自动选择引用时更新同一份元数据，重试重新检查引用和保留期。
- 会话／Run 响应增加 `structuredResults` 及不可读／窗口限制标志，保留 `permissionDiagnostics` 兼容投影；旧 v1 诊断封套适配，旧纯文本仍可读取。会话详情直接排除 Tool 消息，避免加载隐藏的大正文。
- 前端新增结果分发、用户表格、统计摘要组件，复用原权限证据卡片和受控路由。按 Run 关联消息，同一诊断不重复展示；选择、清除、会话切换和不可读重载处理追问引用。所有字段按文本转义。
- 自然月 UTC 偏移选择器无默认值，目前覆盖 UTC−12 到 UTC+14 的整小时；API 支持分钟偏移。结果显示实际绝对时间边界，不能把浏览器本地时区当作业务时区。
- 未引入依赖、平行认证或权限体系；内部 `StructuredResult` 使用 JsonIgnore，外部 MCP 原输入／输出 Schema 和默认工具目录保持兼容，不提供会话引用解析。本轮没有扩大报表数据范围或实现 AIC-004～012。

## 数据影响 [DBA]

**无数据库结构变更、无 EF 迁移、无权限种子变更。**

沿用现有实体、租户、审计、软删除及消息内容保留机制。封套 UTF-8 最多 64 KiB，其中上下文最多 4 KiB；过大时裁剪表格行或统计分组并同步展示计数、引用计数和截断标志，保留原匹配总量和条件。最小元数据仍超限则受控失败。完整结果不复制到保留期更长的 CitationJson 或新表。

单次结构化读取最多 256 KiB，最近结果窗口最多 50 条消息、成功调用候选最多 501 条，超过窗口明确提示；指定 Run 可读取保留期内较旧结果。读取与重试主动按 `ConversationRetentionDays` 检查过期，不依赖清理任务已经执行。

本轮未连接或修改真实业务数据库，未应用迁移、部署、commit 或 push。`.tools/aic-003-test-temp` 为 Git 忽略的隔离测试临时目录，未调整系统 ACL、证书信任或 TLS 规则。

## 验证了什么 [Reviewer]

环境：Windows、.NET 10、现有内存／EF InMemory 夹具、ASP.NET Core TestHost、Vue／Vitest。确定性测试使用模型替身，不调用付费供应商或真实业务数据库。

| 验证 | 实际结果 |
| --- | --- |
| 后端解决方案构建 | 通过，0 错误；本轮首次构建出现既有授权集成测试的 2 个弃用 API 警告，最终增量构建为 0 警告 |
| 结果、追问、会话、工具注册器、诊断、数据范围与授权筛选单元测试 | 184 项通过，0 失败／跳过；含自动选择引用只存一份元数据、隐藏元数据及重试再校验回归测试 |
| 会话／诊断 HTTP 契约与既有 API 授权测试 | 19 项通过，0 失败／跳过；包括 8 项新增会话测试，覆盖认证、权限、引用／时区绑定、新字段序列化及非法绑定拒绝 |
| 前端 `npm run test:unit` | 29 项通过；新结果类型、计数／截断、未知版本、文本转义、旧诊断去重、重载引用失效及明确时区选择 |
| 前端 `npm run lint` | 0 错误；既有 `UploadFile.vue` 的 2 个可选 prop 默认值警告 |
| 前端 `npm run build` | 通过，包含 type-check 与 bundle budget；既有 Element Plus vendor chunk 体积提示仍存在 |
| 全量后端单元测试 | 500 项中 499 项通过、1 项失败、0 项跳过；失败为 Windows 测试证书导入拒绝访问，详见下文 |
| 差异检查 | `git diff --check` 及新增文件 whitespace 检查通过；无依赖、领域实体或 EF 映射变更 |

筛选的 184 项已包含在全量 500 项中，不是额外 184 项；HTTP 19 项和前端 29 项来自独立测试工程。前端验证在本轮前端实现完成后执行，最后会话元数据调整及补充回归仅影响后端。

覆盖检查包括：参数继承／null 清除／诊断类型切换、未知字段和错类型拒绝、明确时区与月末／跨年／偏移的自然月边界、本部门与不同授权范围的交集、过期及已清理消息、对象删除、撤权／缩小范围、停用身份／租户、陈旧部门／超级管理员身份、跨会话／租户／所有者引用、封套篡改与重算单一摘要、失败调用／未知版本、Unicode 字节上限、最近窗口／单次输出限制，以及名称和历史文字中的指令不能成为身份或授权参数。

复现命令（仓库根目录；前端命令在 `frontend/permission-admin` 执行）：

```powershell
dotnet build backend/PermissionSystem.sln --no-restore -m:1
dotnet test backend/PermissionSystem.UnitTests/PermissionSystem.UnitTests.csproj --no-build --no-restore --filter 'FullyQualifiedName~AiStructuredResultTests|FullyQualifiedName~AiFollowUpContextTests|FullyQualifiedName~AiConversationServiceTests|FullyQualifiedName~AiReadOnlyToolRegistryTests|FullyQualifiedName~PermissionDiagnostic|FullyQualifiedName~DataPermissions|FullyQualifiedName~Authorization' --logger 'console;verbosity=minimal'
dotnet test backend/PermissionSystem.IntegrationTests/PermissionSystem.IntegrationTests.csproj --no-build --no-restore --filter 'FullyQualifiedName~AiConversationApiTests|FullyQualifiedName~PermissionDiagnosticApiTests|FullyQualifiedName~ApiAuthorizationIntegrationTests' --logger 'console;verbosity=minimal'
npm run test:unit
npm run lint
npm run build
git diff --check
```

全量测试默认 Windows 临时目录最初有 5 项失败；仅对测试命令进程设置 TEMP／TMP 为工作区隔离目录后，4 项临时目录测试恢复通过。最终全量为 **499/500**，仍失败的 `AiHttpTransportTests.SendAsync_RejectsUntrustedHttpsCertificate` 在 `X509CertificateLoader.LoadPkcs12` 阶段抛出 `CryptographicException: 拒绝访问`，尚未进入 TLS 断言。未修改该测试或弱化 TLS 校验，不能记录为通过。

## 剩余风险与目标环境验收 [Reviewer]

代码审查结论：**通过本轮实现、分层与安全边界复核；完整环境验收尚未通过，AIC-003 保持待验收。**

1. 环境管理员处理 Windows 测试证书导入权限后重跑 TLS 用例及全量回归。本轮没有生产 TLS 验收证据。
2. 隔离 SQL Server 验证消息 JSON 筛选、分组／总量、时间 tick 精度、查询计划及长会话窗口成本；验证既有全局过滤与新显式租户条件共同生效。当前内存夹具和 TestHost 不能替代真实数据库验证。
3. 真实供应商验证可选参数与 contextRef Schema、工具选择、明确对象澄清和解释质量；浏览器验证卡片布局、选择／切换／重载、UTC 偏移和受控入口。确定性测试不保证模型一定选对工具或给出无误自然语言。
4. 追问条件示例的固定服务端约束针对两个精确文本；其他自然语言变更仍需模型解释白名单参数并经过处理器校验，不能把它当作通用自然语言规则引擎。
5. 历史卡片为有时间标记的快照，重载不重新查询、不产生模型费用。权限变化会隐藏不可读卡片，但既有助手纯文本保持原读取策略，不追溯清除已展示或已保存内容。
6. 当前 UI 只提供整小时 UTC 偏移，分钟偏移可通过 API 指定；固定 UTC 偏移不表达夏令时地区规则，不能推定为业务时区配置。
7. 最近结果窗口及输出字节上限会限制长会话展示，提示后需选择可读结果或明确新查询；不静默从窗口外猜选对象。并发撤权仍遵守既有请求检查语义，需要目标环境验证。

目标环境验收建议：在隔离租户创建授权／范围受限／无部门身份，完成用户查询后选择卡片分别追问启用状态、本部门；检查继承条件及交集。分别完成日志查询，在明确偏移后追问上个月并核对绝对边界、总量和超过 20 个模块的截断。对原结果撤权、删除对象和越过保留期后重载及重试，核对隐藏或澄清且不发起扩大范围查询；同时验证旧 v1 诊断和纯文本会话可读。完成真实供应商、SQL Server 与浏览器证据后再决定退出状态。

## 发布与回退

未执行发布或回退。无 Down 迁移或数据回填；旧代码继续隐藏 Tool 消息并忽略新响应字段，已有封套和请求元数据沿用消息保留期限。应用回退可能失去新版本追问／重试语义，回退前应结束活跃 Run；不得把旧版本重新查询等同于恢复原上下文。
