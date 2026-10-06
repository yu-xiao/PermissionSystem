# AIC-004：最小评测集与自动回归验收记录

> 日期：2026-10-06
>
> 状态：代码与离线闭环已实现，待验收。黄金预期、自然语言事实及真实模型尚未完成人工／环境验收，不能标为已完成或发布通过。
>
> 依据：[实施方案](ai-center-aic-004-implementation-plan.md)、[后续开发计划](ai-center-next-development-plan.md) 及 `AGENTS.md`。用户回复“确认”批准方案，随后指示继续处理；确认不包含付费模型运行、部署、数据库操作或 Git 提交／推送。

## 1. 已实现范围 [Architect] [Developer]

独立开发工具驱动现有应用编排，业务授权、租户隔离、数据范围及历史读取继续使用现有服务。API、Worker、Application、Domain 没有反向依赖评测工程，没有前端及生产业务逻辑修改。

| 文件 | 实际作用 |
| --- | --- |
| `backend/PermissionSystem.AiEvaluations/PermissionSystem.AiEvaluations.csproj` | .NET 10 控制台工程，引用既有 Application／Infrastructure；复用项目已有的 EF Core InMemory 依赖种类 |
| `EvaluationModels.cs` | 严格案例、步骤、报告、manifest、判定与人工审核契约；拒绝未知属性及重复案例 |
| `IsolatedEvaluationEnvironment.cs` | 每变体独立 InMemory；复用会话、诊断、CredentialValidator、数据范围、只读注册器、读取／追问、预算及准入服务 |
| `EvaluationGateway.cs` | 离线脚本网关、实际模型调用／工具参数记录、显式真实配置、调用／Token／估算费用预留和停止 |
| `EvaluationChecker.cs` | 工具选择、提出与生效参数、证据、事实辅助及安全检查；完整覆盖、人工绑定、基线可比性与退步检查 |
| `EvaluationRedactor.cs` | 敏感字段、凭据、链接和超长文本脱敏／裁剪；规范化确定性摘要 |
| `EvaluationRunner.cs` | 逐变体执行、案例／夹具／检查器／源码摘要、Git 与运行环境记录；异常／取消后保留已发生调用 |
| `Program.cs` | Run、Compare、ReviewTemplate 入口；JSON、Markdown、SHA 报告；复用既有安全 HTTP 传输，受控退出码 |
| `evaluations/ai-center/cases.json` | 28 个候选案例／41 个明确变体；37 个真实模型适用，4 个异常注入仅离线适用 |
| `evaluations/ai-center/README.md` | 夹具、命令、门槛、预算和审核使用说明 |
| `evaluations/ai-center/live-settings.example.json` | 故意不能直接运行的真实配置模板；没有真实凭据或已批准预算 |
| `evaluations/ai-center/feedback-to-case-template.md` | 人工脱敏筛选、规则依据及 Owner 审核模板；未自动导入实际反馈 |
| `scripts/aic-004-evaluate.ps1` | PowerShell 统一执行、比较、审核模板入口 |
| `backend/PermissionSystem.UnitTests/AiCenter/AiEvaluationTests.cs` | 新增 66 项评测测试，包括 41 个案例变体及检查器／预算／比较／脱敏／取消测试 |
| `backend/PermissionSystem.sln`、UnitTests 工程及评测 `Properties/AssemblyInfo.cs` | 工程注册、测试引用、案例复制及仅测试可见的内部支持 |
| Infrastructure `Properties/AssemblyInfo.cs` | 仅增加评测工程内部可见性以复用原 `AiHttpTransport.CreateHandler`，未修改生产 TLS／地址校验 |
| `.github/workflows/ci.yml`、`.gitignore` | Release 构建后离线评测并上传合成报告；精确忽略本地报告目录 |
| 本记录、实施方案、后续计划及原 MCP 实施计划 | 同步实际状态；仅 P5-B 最小评测部分落地，AIC-005 正式版本发布治理未实施 |

上述 C# 文件位于 `backend/PermissionSystem.AiEvaluations/`，另有标明完整相对路径的文件。CI 定义已更新，本次没有触发或验证远端 GitHub Actions。

## 2. 判定与边界 [Reviewer]

- 离线模式使用脚本化网关测试真实服务端编排及检查器，不衡量供应商模型能力；37 个适用变体也可能在服务端拒绝／澄清时没有模型请求，报告如实记录。
- 模型提出参数与服务端实际参数分别检查，UUID 标识按 GUID 语义比较，其他参数保持案例约束；服务端纠正参数不能被冒充为模型参数正确。
- AIC004-23 本次工具返回 `InsufficientEvidence`，既有读取器隐藏不可读菜单的展示投影。报告分别记录 `Tools.ServerResult` 和 `Evidence`，未放宽读取鉴权。
- 多个历史候选通过显式步骤生成；设置步骤的所有模型调用进入报告及预算。取消时保留已发生请求、耗时和费用，不继续执行后续请求。
- 离线要求所有检查通过，安全零失败；真实模式工具／参数和事实自动正确率分别至少 95%，安全仍零失败。发布还要求人工黄金与事实审核、完整覆盖、已人工确认的可比较基线及无新增自动／人工事实退步。
- 人工审核以独立文件绑定报告与案例 SHA；模板默认未批准。关键词检查仅为辅助，没有把助手生成的候选预期标为人工已核验，也没有把模型自评分作为验收依据。
- 真实模式要求显式 HTTPS 供应商、允许主机、合规确认、单价及调用／Token／估算费用上限。密钥只从 `AIC004_API_KEY` 进程环境读取；未执行真实付费请求。
- 预算计入每次请求的预留，异常、取消和缺 usage 不按零结算；超限停止后续请求。估算不是账单数学上界，需要严格账单限额时还须供应商端限额。

## 3. 数据影响 [DBA]

**无数据库结构变更、无 EF 迁移、无权限种子或生产数据变更。** 未读取业务连接串、连接实际业务数据库或执行生产初始化；身份与记录均为进程内合成夹具。

InMemory 无法证明 SQL Server 查询翻译、事务并发、索引或规模性能通过；隔离身份也不证明真实 OpenIddict／HTTP 登录链路。评测报告复用文件保存，与生产会话保留策略无耦合，访问与保留期限由使用者管理。

## 4. 实际验证 [Developer] [Reviewer]

在仓库根目录执行 Release 构建和相应测试；报告路径均相对于仓库根目录。最终版本没有以较早轮次结果代替新增取消测试后的结果。

| 验证 | 实际结果 | 证据 |
| --- | --- | --- |
| `dotnet build backend/PermissionSystem.sln --configuration Release --nologo --verbosity minimal` | 最终构建成功，0 警告／错误 | 本次命令输出；首次全量构建曾出现既有 IntegrationTests ASPDEPR004／008 弃用警告，未修改相关代码 |
| UnitTests，`FullyQualifiedName~AiEvaluationTests`，Release `--no-build` | 66/66 通过 | `artifacts/ai-evaluations/verification/aic-004-evaluation-closure.trx` |
| 全量 UnitTests，Release `--no-build`，测试子进程专用工作区 TEMP／TMP | 566 项：565 通过、1 失败、0 跳过 | `artifacts/ai-evaluations/verification/aic-004-unit-closure.trx` |
| Legacy `PermissionSystem.Tests`，Release `--no-build` | 45/45 通过 | `artifacts/ai-evaluations/verification/aic-004-legacy.trx` |
| `AiConversationApiTests` HTTP 契约，Release `--no-build` | 8/8 通过，TestServer／mock，不连接真实数据库 | `artifacts/ai-evaluations/verification/aic-004-http.trx` |
| 最终两次离线执行 | 各 41/41 变体通过，安全失败 0；两次各记录 62 个脚本模型请求 | 下列 baseline／candidate 报告 |
| 前后比较 | `comparable=true`、`noRegression=true`，发布门槛 `passed=false` | candidate 同目录 `comparison.json` |
| 规范化确定性摘要 | 两次一致：`573683b8543cbee4a2c874cf9f6e89e2df532ea6992fcbf675753adcdfe0dfae` | 两次 `report.md` |
| ReviewTemplate | 退出 0；审核者／日期为空，`goldenCasesApproved=false`，41 项均未批准 | `artifacts/ai-evaluations/20261006-080107-41d1955395304823b68e39edff2bdb9c/review-template.json` |
| Compare 加 `-RequireReleaseGate`，不提供审核 | 实际 `$LASTEXITCODE=2`，未审核无法发布通过 | `artifacts/ai-evaluations/20261006-080124-68f4716b59754bb8adff37631b64150c/comparison.json` |
| PowerShell 解析、JSON 数量及文档链接、差异／新增文件 whitespace | 通过；核对 28／41／37／4 数量及 30 个本地 Markdown 链接；保留原有文档中的 Markdown 换行空格 | 本地静态验证 |

最终 baseline：`artifacts/ai-evaluations/20261006-075729-5e0739a746fe45c69f0aaf4518f91690/report.json`。

最终 candidate：`artifacts/ai-evaluations/20261006-080035-8c38198d27354ea797a766a125a4f655/report.json`。

这两个是未人工批准的离线比较夹具报告，不是正式黄金／真实模型发布基线。离线币种 `XXX`、Token 与费用为合成估算，不代表实际收费；耗时及估算预留可变化，规范化摘要只验证确定性事实字段。

复现入口：

```powershell
./scripts/aic-004-evaluate.ps1 -Configuration Release -NoBuild
./scripts/aic-004-evaluate.ps1 -Configuration Release -NoBuild -Baseline artifacts/ai-evaluations/20261006-075729-5e0739a746fe45c69f0aaf4518f91690/report.json
./scripts/aic-004-evaluate.ps1 -Command ReviewTemplate -Configuration Release -NoBuild -Candidate artifacts/ai-evaluations/20261006-080035-8c38198d27354ea797a766a125a4f655/report.json
```

如果还没有 Release 构建，去掉 `-NoBuild`。命令每次新建独立报告目录，不覆盖旧产物。

### 已知测试环境问题

全量回归唯一失败：`AiHttpTransportTests.SendAsync_RejectsUntrustedHttpsCertificate`，在 `X509CertificateLoader.ImportPfx` 导入测试证书时抛出 `CryptographicException: 拒绝访问`（原测试第 22 行）。失败发生在证书导入阶段，不能据此判定实际 TLS 拒绝行为已验证。

默认 Windows TEMP 首次还有 4 项文件目录拒绝访问；仅对测试子进程改用工作区的新临时目录后，这 4 项通过，剩余证书导入失败仍存在。没有修改用户系统环境、TLS 校验、测试断言或跳过失败用例；环境管理员修复证书导入权限后须重跑全量回归。

## 5. 复核结论与待验收项 [Reviewer]

**通过本轮代码、分层、最小变更及安全边界复核；完整环境验收未通过，任务保持待验收。**

| 待完成项 | 责任与退出证据 |
| --- | --- |
| 黄金案例输入／预期及自然语言事实审核 | 业务 Owner 逐变体确认契约与解释依据，形成绑定准确报告／案例摘要的独立审核文件；不能直接批准助手候选 |
| 实际负反馈筛选转案例 | 业务 Owner 使用转换模板，提供脱敏来源、业务规则及审核依据；新增案例后更新版本并重建审核基线 |
| 真实模型运行与比较 | 管理员明确供应商合规、允许主机、网络策略、模型及预算，安全注入进程密钥；37 个适用变体执行并人工审核，首次审核后才能形成真实模式基线 |
| TLS 与目标环境 | 修复 Windows 测试证书导入权限，重跑全量；按 AIC-002／003 环境验收验证隔离 SQL Server、真实认证及适用的性能行为 |
| CI 远端验证 | 后续在用户授权的提交／推送后观察 GitHub Actions；本轮仅修改 CI 定义并本地验证同一离线命令 |
| 正式版本与发布治理 | AIC-005 单独确认并实现不可变构建／发布快照、绑定与发布流程；源码摘要和本地门槛不能替代该能力 |

实现与本地验证阶段未部署、未迁移、未调用付费模型、未 commit／push。随后用户明确要求先提交并推送 Git、验收延后；本次提交不代表人工审核、真实模型或环境验收通过，状态仍为待验收。回退限开发工具、案例、测试及 CI／文档变更，无 Down 或数据删除；保留用户改动及既有报告。
