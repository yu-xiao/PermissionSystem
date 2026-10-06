# AIC-005：场景配置与版本发布治理验收记录

> 日期：2026-10-06
>
> 状态：首批代码与本地验证已完成，待验收。真实模型、人工黄金／事实审核、隔离 SQL Server 和浏览器体验尚未完成，不能标记正式发布通过。
>
> 依据：`AGENTS.md`、[已确认实施方案](ai-center-aic-005-implementation-plan.md)、[后续开发计划](ai-center-next-development-plan.md)。用户回复“确认”批准实施范围；不包含实际业务数据库迁移、付费模型调用、部署或 Git 提交／推送。

## 1. 实际范围 [Architect] [Developer]

首批只开放 `permission-assistant` 的配置与发布治理，绑定权限排障、用户查找、部门查找和角色摘要四个已有只读工具的子集。审计助手没有专用黄金案例，本轮不开放。未增加动作工具、认证体系、权限种子、第三方框架或依赖。

| 文件／模块 | 实际作用 |
| --- | --- |
| Domain `Entities/AiScenario.cs` | 场景、草稿、不可变版本、评测证据、追加发布事实；继承 BaseEntity，不可变类型带保存检查标识 |
| Application `AiCenter/AiScenarioModels.cs`、`AiScenarioCatalog.cs`、`AiScenarioSnapshot.cs` | DTO、管理员边界、原安全 Prompt、规范化配置、实际扩展 Schema、不可变快照及模型指纹 |
| Application `AiScenarioService.cs`、`AiScenarioEvaluationVerifier.cs` | 当前身份／租户／权限检查、并发、冻结、导入、人工审核、发布／停用／回退／撤销资格、确定性门禁及运行兼容检查 |
| Application `AiExecutionConfiguration.cs`、`AiConversationService.cs`、`AiConversationModels.cs` | 会话固定版本、重试版本校验、逐模型／逐工具鉴权、运行配置摘要、旧内置路由兼容与明确配置阶段 |
| Application `AiCenter/Evaluations/` | AIC-004 纯模型、检查器及脱敏逻辑迁入 Application，CLI 和应用复用；保留原命名空间以减少兼容改动，生产不引用 CLI 工程 |
| Infrastructure `Ai/AiBuildIdentity.cs`、`Configurations/AiScenarioConfiguration.cs`、`Data/AppDbContext.cs` | 四个实际程序集 SHA-256 构建身份、租户复合外键／唯一键／RowVersion、不可变记录更新／删除拒绝 |
| Domain／Infrastructure 会话与 Run 实体、映射及迁移 | 可空场景／版本关联、Run 配置及构建摘要；旧记录保持 NULL |
| Api `Controllers/AiScenarioController.cs` | DTO 与 ApiResult 入口，复用治理／聊天权限、幂等元数据、CancellationToken；报告按评测文本枚举契约返回 |
| CLI `CandidateEvaluationRuntime.cs`、`EvaluationFiles.cs`、Runner／Gateway／Program | 仅隔离进程执行未发布候选，实际记录 Prompt／Schema／参数／预算；支持 `--snapshot` 与合成模板，不注册到生产 |
| `evaluations/ai-center/cases.json`、README、`scripts/aic-004-evaluate.ps1` | 保留 28 个案例／41 个变体；受控场景停用身份在 Run 前拒绝，内置入口沿用原预期；补充候选命令与边界说明 |
| 前端 `api/aiScenario.ts`、`AiScenarioGovernance.vue`、治理页 | 草稿、冻结、导出、报告导入、逐案例事实审核、发布／停用／回退／撤销及事件历史；只读权限隐藏变更入口 |
| 前端 `AiChatDialog.vue`、`api/ai.ts` | 新会话显式选择场景、固定版本标签、明确升级新建会话；清除旧引用／时区，不隐式搬迁消息 |
| Unit／Integration／Vue 测试 | 快照、篡改、授权、发布门槛、资格绑定、运行撤销、EF 跟踪状态刷新、HTTP 契约、SQL 专项及前端审核／升级回归 |
| CI、`.gitignore` 与相关计划 | 新增候选快照离线检查／产物上传及隔离 CI 库准备步骤；本轮未触发远端 Actions，产物不提交 |

## 2. 安全、版本与发布边界 [Reviewer]

- 管理员只编辑展示信息、补充 Prompt、四工具子集、收紧的运行限制及模型采样参数；DTO 拒绝身份／租户覆盖、未知安全开关和字符串数字。配置不授予工具权限。
- 安全提示、工具实现、原始／实际输入与输出 Schema、RequiredPermissions、数据范围元数据、追问契约和构建身份来自代码。快照规范化后计算内容哈希，实际程序集身份缺失或不匹配时受控失败。
- 新受控会话固定当时发布版本，发布切换不更新老会话。重试检查原版本；停用和资格撤销阻止续问。升级新建会话，不迁移上下文。旧客户端与旧内置会话使用原固定 `permission-platform-agent` 路由，不因受控场景失败自动回退。
- 每轮模型、每次工具和结果保存前检查资格；场景、模型路由和供应商通过字段投影读取最新已提交状态，避免同请求的 EF 跟踪缓存掩盖变更。在途外部请求不能瞬间撤回，后续处理会拒绝继续。
- 新 Run 保存 `Admitted` 或 `ModelRequestPrepared` 阶段、基础 Prompt 摘要、实际工具目录、限制、请求模型／采样／指纹与构建。澄清前未提供工具时目录为空，不能伪造已执行配置；迁移前旧 Run 不回填今天的配置。
- 导入报告有 32 MiB、深度、数量和重复属性／案例约束。后端按随 Application 构建内嵌的案例重新计算检查，不接受上传 `Passed` 或正确率；检查候选／构建／案例、实际 Prompt、工具 Schema、模型参数及调用／费用记账。
- 人工审核默认未批准，逐案例记录事实依据；审核人和时间来自服务器认证身份。授权管理员提交的观察和“真实运行”声明仍是信任边界，摘要不证明调用来源，本轮不提供外部签名证明。
- 正常发布必须同时有已审核离线与真实证据、安全零失败、95% 工具／参数和事实正确率、完整覆盖及可比基线无退步。主／灰度／备用每个可能组合独立要求证据，密钥不进快照，供应商安全与预算继续沿用当前规则。
- 发布事实保存精确通过的证据 ID；运行资格只使用绑定证据，后来导入／审核不自动解锁模型。后续发布包括同版本重新发布，均比较上次发布绑定的基线；撤销该基线不能靠后来报告替代。首次发布要求明确基线确认。
- 回退须同租户、同场景、已发布、未停用且与当前构建／模型兼容；停用版本复制新草稿重新评测。不可变保存约束覆盖应用 EF 链路，不宣称阻止数据库管理员或直接 SQL 修改。
- 单元测试中的 `SyntheticLiveJson` 只验证状态机，不是供应商报告、黄金审核或正式发布证据。CLI 合成模板也不是实际租户的候选证据。

## 3. 数据影响与迁移 [DBA]

**有数据库结构变更，未应用实际数据库。**

迁移：`backend/PermissionSystem.Infrastructure/Data/Migrations/20261006083437_AddAiScenarioReleaseGovernance.cs`，对应 Designer 和模型快照已同步。新增五张表、会话与 Run 可空字段、租户／场景复合键与 Restrict 外键、RowVersion、版本序号／事件序号／报告唯一索引，以及关联字段成对为空的 CHECK。

仅生成本轮增量幂等 SQL：`artifacts/aic-005/aic-005-migration.sql`。已静态核对 Up 为新增结构和 EF 历史记录，不回填旧 Run、不自动发布、不修改权限／路由／预算。模型差异检查无待生成变更。SQL 脚本未实际执行，不能推定数据库编译、锁耗时或数据兼容验收通过。

新增大表索引、外键和 CHECK 可能获取锁，需要 DBA 在隔离环境评估数据规模与维护窗口。Down 会删除新增历史，仅保留为迁移定义，不是自动恢复步骤。应用回退优先停止受控场景并保留新表／历史；版本回退不能恢复旧程序集。

SQL 两项专项要求显式隔离连接且已应用审核迁移，测试自身不执行 Migrate；本地没有配置 `PERMISSION_SYSTEM_SQLSERVER_TEST_CONNECTION`，因此跳过。CI 定义在临时 CI SQL Server 容器中先完成迁移，避免这些专项与旧测试自动建库竞争；本轮没有执行 CI 或对本地／业务库迁移。

## 4. 本地验证 [Developer] [Reviewer]

环境：Windows、.NET 10、现有 npm 依赖，Release 构建。以下为本轮实际结果，不借用 AIC-004 历史通过证据。

| 验证 | 结果／证据 |
| --- | --- |
| `dotnet build backend/PermissionSystem.sln --configuration Release --no-restore -v minimal` | 通过；0 错误，既有 WebHostBuilder／TestServer 过时警告 2 个 |
| UnitTests 全量 | 603 项，602 通过、1 失败；`artifacts/aic-005/aic-005-unit-final.trx` |
| 场景与会话专项（从全量结果核对） | 53/53；包含同版本基线与跨 DbContext 读取最新场景／路由／供应商回归 |
| Legacy 全量 | 45/45；`artifacts/aic-005/aic-005-legacy.trx` |
| Integration 全量 | 61 项，37 通过、24 SQL 环境测试跳过；`artifacts/aic-005/aic-005-integration-final.trx` |
| 前端全量 Unit | 34/34，10 个文件；`artifacts/aic-005/frontend-tests.json` |
| 前端 `type-check`、`lint`、`format:check`、`build` | 通过；lint 只有既有 UploadFile 两个 default-prop 警告，构建包体预算通过，既有 Element Plus 大块提示保留 |
| EF `has-pending-model-changes` | 无变更；从仓库根目录使用现有全局工具完成，CLI 10.0.7 与 runtime 10.0.10 版本提示保留 |
| 本轮增量幂等迁移 SQL 生成 | 通过；只生成、静态审阅，未应用 |
| 候选两次离线与旧入口回归 | 各 41/41 自动检查通过；不是模型能力／发布验收通过 |
| 候选比较 | `comparable=true`、`noRegression=true`，发布门槛仍 false |
| 未审核发布检查 | 显式 `--require-release-gate` 返回 2，符合预期 |
| 差异与格式 | `git diff --check` 及本轮新增文件 whitespace 检查通过 |

全量 Unit 首次默认临时目录访问拒绝，产生额外四项文件／归档失败，记录在 `aic-005-unit.trx`。仅为测试进程设置工作区 `artifacts/aic-005/temp` 的 TEMP／TMP 后，这四项通过，没有修改产品代码或安全校验；唯一剩余失败是 `AiHttpTransportTests.SendAsync_RejectsUntrustedHttpsCertificate` 在 Windows 导入测试 PFX 时拒绝访问，与 AIC-004 已记录问题一致。TLS 校验未放宽，测试未删除／跳过，完整全量回归不能标为全部通过。

后端命令在仓库根目录执行，测试用 `--configuration Release --no-build --logger "trx;LogFileName=<对应文件>" --results-directory artifacts/aic-005`。前端命令在 `frontend/permission-admin` 执行。

最终构建产物目录如下（先前构建的报告保留作调试记录，不作为当前资格）：

| 产物 | `artifacts/aic-005/final/` 内路径 |
| --- | --- |
| 未发布合成模板 | `candidates/20261006-090612-187173aff86c473083bd03f4e652a29d/snapshot.json` |
| 候选第一次 | `first/20261006-090700-7a66466d072c46d5bb9d880bf0d23736/report.json` |
| 候选第二次 | `second/20261006-091149-dda4cb51a62241e283d41b319e61aba5/report.json` |
| 比较 | `comparison/20261006-091246-3b79f8c3048d4ec0b6a7e33c0402e136/comparison.json` |
| 未审核门槛拒绝 | `unapproved-gate/20261006-091246-8eaf91ba109244bf8ed0a056299ae38a/comparison.json` |
| 内置入口 | `builtin/20261006-091251-1978ac1796b54b2c9fdeade089dea156/report.json` |

## 5. 剩余风险与验收条件 [Reviewer]

本轮代码、分层、兼容与安全边界复核：**通过**。完整验收和正式发布门槛：**未通过，待验收**。

1. 环境管理员解决 Windows PFX 导入权限并重跑失败测试和全量回归，不能通过放宽 TLS 结论解决。
2. DBA 明确隔离 SQL Server，审核／应用迁移后验证事务原子性、RowVersion 竞争、跨租户外键、唯一键、旧记录及迁移锁／耗时。InMemory 不证明 SQL 行为。
3. 业务 Owner 审核黄金预期；管理员按每个主／灰度／备用组合确认真实供应商、网络、合规及预算后执行真实模式，在平台逐案例审核，具备资格后才发布。AIC-004 未完成条件不因本轮开发自动通过。
4. 浏览器人工验证草稿到发布／回退、停用会话、升级与错误提示；本轮 Vue 测试不代表浏览器端到端／真实 OpenIddict 已验收。
5. 严格构建身份会使代码升级后的历史版本无法继续执行，需重新冻结、评测和明确新建会话；采样参数变化使当前基线不可比，门禁不会放行，后续基线更新流程需独立确认。
6. 运行频繁检查治理状态增加查询；新增证据／版本长期累积、报告授权提交可信度与保留政策、真实费用统计及大规模性能仍需环境验收。构建部署需保留一致产物并重启进程，数据库快照不能替代程序版本恢复。

本轮没有实际业务库访问／迁移、真实模型费用、部署、commit 或 push；原有工作区与历史产物保留。下一步按上述责任边界完成验收，状态才能退出“待验收”。

后续用户明确指示先提交并推送 Git、后续统一验收。此次授权仅推进代码提交与推送，不改变上述验证结果、剩余条件及“待验收”状态。
