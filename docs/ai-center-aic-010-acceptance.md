# AIC-010 首批合成文档知识库验收记录

> 日期：2026-10-07
>
> 状态：首批实现完成、待环境验收；真实批次待确认。整个 AIC-010 未完成，未执行部署、真实资料导入或数据库迁移。
>
> 依据：[已确认实施方案](ai-center-aic-010-implementation-plan.md)、`AGENTS.md`、[后续开发计划](ai-center-next-development-plan.md)。

## 1. 已确认边界 [Architect]

用户确认首批使用合成 UTF-8 `.txt`：256 KiB／文件、100 份有效文档／租户、256 片段／版本、2,000 字符／片段、5 命中／请求。Owner、许可及合成标记必须明确填写；不接入用户现有附件、生产资料、PDF／Office／OCR 或向量存储。

正文同时要求当前有效身份及租户、`ai:knowledge:query`、逐文档有效角色 ACL、当前已发布且生效／未到期版本、Active／Clean 文件。`view`／`manage` 不授予正文读取，SuperAdmin 不绕过文档 ACL。模型另需原聊天／工具权限及明确的场景白名单。

新增内部工具 `knowledge.documents.search`／`search_knowledge_documents` v1.0，`AiCenter:EnableKnowledgeDocumentTool` 默认 false，依然受 AI 总开关和租户允许列表约束。未加入外部 MCP 或原权限助手白名单；安全提示词版本为 2.4，旧场景不能静默获得新工具。

## 2. 实现结果 [Developer]

| 范围 | 已实现行为 |
| --- | --- |
| 文档管理 | 分页元数据、创建合成草稿、RoleId ACL 配置、并发令牌、受限版本导入／审核预览／发布／删除 |
| 文本解析 | 严格 UTF-8、段落／行号／Hash、容量限制、超长行切分及 surrogate 保护；不执行文本内指令或链接 |
| 版本状态 | Pending／Ready／Failed；扫描／存储失败使用稳定脱敏失败码，同文档相同 SHA-256 复用／重试，不跨文档或租户去重 |
| 发布 | 管理权限 + 正文权限及文档 ACL；短事务锁内复核 Ready、文件和授权，原子切换当前版本；未来生效版本发布后仍不能提前检索 |
| 检索 | SQL 内先租户／ACL／当前版本／有效期／文件状态过滤，再受限直接词匹配及稳定 Top-K；无结果不泄露被过滤文档存在性 |
| 引用 | 固定 DocumentId／VersionId／ChunkId／Hash，服务端重新投影标题、正文和行号；旧版本不自动跳转新版 |
| 文件入口 | 知识文件绑定文档；上传按管理权限，列表／下载按查询权限及当前版本，通用删除拒绝；响应隐藏 ObjectKey／BucketName／LastError，正文入口 NoStore |
| 模型与历史 | 工具封套长期只存引用和条件，正文仅用于当前模型调用；绑定所有来源，模型请求前后及最终提交复核，任一来源失效时屏蔽整条回答与引用 |
| 完整性 | 核对封套摘要、Run／Invocation 归属、引用集合和 Hash；缺依赖、孤立依赖、调用失败或摘要不一致默认不可读；零命中不替模型虚构结论背书 |
| 后台兼容 | 复用 AIC-008 身份／租约／取消／结算；结算丢弃未保存的知识依赖，租约丢失拒绝持久化 |
| 删除与保留 | 显式删除同步清空片段及关联知识回答正文，精确匹配对应 Run 的知识工具封套，文件转 PendingDelete，保留无正文依赖墓碑；过期 Run 清理先移除依赖 |
| 到期与中断 | 到期立即不可读，每日 retention 在显式 ValidUntil 后清空片段并安排文件删除；覆盖已删除文档的迟到文件和全部版本到期后的遗留文件。导入先保存 Pending 文件关联，再提交 Ready，取消／中断不自动视为 Ready |
| 前端 | 管理页、角色授权、导入有效期、审核分页、发布／删除、受控查询；引用卡片纯文本显示，源查看失败清除缓存正文，切换租户／重载／聊天重新打开时清除旧内容 |

角色枚举继续使用原 `system:role:view`，管理者没有此权限时可创建空 ACL 草稿，不能枚举或选择角色。后端对每个 RoleId 再校验当前租户和有效状态，界面权限仅控制可见操作。

被替换版本立即失去引用资格，源数据保留到其明确到期或文档删除；助手正文按原会话内容保留窗口清理，并在每次读取时屏蔽失效资料。删除不承诺擦除用户自行输入的文字、浏览器已展示内容、备份或模型供应商副本。

## 3. 数据评审 [DBA]

已生成 `20261007033743_AddAiKnowledgeBase`、Designer、ModelSnapshot 和 [增量审核 SQL](sql/aic-010-knowledge-review.sql)。

- 新增 `ai_knowledge_document`、`ai_knowledge_document_version`、`ai_knowledge_chunk`、`ai_knowledge_document_role`、`ai_knowledge_run_reference`。
- 继承 BaseEntity，保留 TenantId／审计／软删除／rowversion；关联使用租户组合外键及 Restrict。
- 新增 Roles、FileResources、ai_run 的 `{TenantId, Id}` 组合唯一约束；没有变更旧业务列或回填旧会话。
- 版本号／内容 Hash、片段序号、有效 ACL、Run 引用幂等唯一约束；版本有效期 check constraint。
- 模型一致性检查通过；迁移只包含上述新增表、约束、索引和迁移历史记录。未执行 SQL、Down、EnsureCreated、Migrate 或种子初始化。

生成过程使用仓库忽略目录 `artifacts/aic-010/ef-design` 的独立 EF 设计时入口，仅构建模型，未读取业务连接配置或启动 API／Worker／Hangfire。EF CLI 10.0.7 提示运行时版本 10.0.10 较新；本次未安装或升级依赖。

目标库须独立审核新增唯一约束的建索引锁、容量及维护窗口。回退首先关闭知识工具和入口，保留新表及历史屏蔽代码；不能回到直接返回知识助手正文的旧应用，不能自动执行 Down 清表。

## 4. 实际验证 [Reviewer]

| 检查 | 结果 | 证据及限制 |
| --- | --- | --- |
| AIC-010 核心专项 | 46/46 通过 | 解析、版本、失败重试、容量、ACL／权限分离／SuperAdmin／跨租户、文件、引用完整性、历史撤权、模型期间撤权、追问、后台结算／租约；核心数据夹具为 EF InMemory，不证明 SQL 事务或外键 |
| SQL 查询翻译／模型元数据 | 通过，计入上述专项 | 实际 Search 用例的 SQL Server ToQueryString，检查参数化 literal LIKE、权限子查询、状态／有效期及 Top-K；未连接 SQL Server |
| HTTP 集成 | 77 通过，32 跳过 | 新增知识 API 4 项通过：原认证授权策略、manage 不隐式 query、NoStore、multipart 错误令牌；用例替身仅验证 HTTP 契约，不能替代真实 ACL／SQL。新增 SQL 4 项及原环境测试跳过 |
| 全量 UnitTests | 864 通过，1 失败，共 865 | 原 `AiHttpTransportTests.SendAsync_RejectsUntrustedHttpsCertificate` 在未改动的第 22 行导入临时 PKCS12 私钥时发生 Windows `CryptographicException: 拒绝访问`；未弱化断言、替换测试或宣称全绿 |
| 原 Tests 回归 | 45/45 通过 | 原项目适用回归 |
| 前端 Vitest | 67/67 通过，14 文件 | 包含新管理页、引用卡片、知识结构化展示及聊天重开清缓存；为组件测试，不是浏览器实测 |
| 前端类型／lint／format | 通过 | lint 0 error，原 UploadFile.vue 的 2 个 default-prop warning 保留 |
| 前端 production build | 通过 | 包体预算通过：总 JS 1,797,138 bytes，最大 vendor-element-plus 910,023 bytes；保留既有大 chunk 提示 |
| Release solution build | 通过 | API／Worker／其余项目均编译，0 error；原 IntegrationTests 的 2 个过时 API warning 保留。使用独立 artifacts 输出避免既有 DLL 写入限制 |
| EF model consistency | 通过 | `has-pending-model-changes` 无未生成模型变化 |
| 候选检索专项 | 直接词 8/8，同义表达 0/1 | [报告](../evaluations/ai-center/knowledge/validation-report.md)，Python 合成候选，不是 SQL 执行或人工黄金验收 |
| 原 AIC-004 离线评测 | 41/41 自动通过，安全失败 0 | 供应商 scripted，未付费／联网；人工黄金／对比基线发布门槛未通过，也不覆盖知识模型行为 |
| 工作区检查 | diff 空白检查通过 | 不 commit／push，不改目标配置，不执行真实资料导入 |

本地 TRX 位于 `artifacts/aic-010/test-results/`：`aic010-core.trx`、`unit-full.trx`、`integration.trx`、`legacy.trx`。离线报告位于 `artifacts/aic-010/evaluations/`；这些运行产物已忽略，不提交。

默认及部分既有独立构建输出的 AiEvaluations refint DLL 无法覆写；本次使用新的输出目录完成编译，并仅对测试子进程设置工作区 TEMP／TMP。未修改系统权限、永久环境变量或删除旧输出。全量 TLS 证书导入问题仍未解决，因此发布／全量验收不能记为通过。

## 5. 尚待验收与风险 [Reviewer]

1. **隔离 SQL**：准备明确隔离且手工审查／应用迁移的测试库。新增 SQL 测试只有在 `PERMISSION_SYSTEM_AIC010_SQL_TEST_CONNECTION` 存在且 `PERMISSION_SYSTEM_AIC010_SQL_TEST_ISOLATED=1` 时运行，并回滚各自合成夹具；不复用 API 配置连接。需验收组合 FK、唯一约束、rowversion、到期清理，并继续完成多连接发布／撤权／删除／迟到答案、锁顺序、事务回滚和执行计划。当前未取得这些证据。
2. **实际模型及发布门槛**：明确供应商对合成资料的发送许可，单独批准包含知识工具的场景并冻结／评测。验证注入文本、无依据、冲突版本、引用事实及取消／超时／重试全过程，完成精确报告绑定的人工黄金和基线审核。原离线 41 变体不替代该验收。
3. **浏览器**：在隔离环境验证全部预览分页、权限按钮、键盘／窄屏、来源不可用、租户切换、历史刷新及后台断线恢复。当前仅组件验证，未对运行中的页面截图或进行端到端操作。
4. **基础设施及清理**：验证扫描／存储异常、文件补偿重试和每日 retention 的 SQL 执行；中断的 Pending 版本由显式管理重试恢复，不自动发布。文件扫描为项目已有基础类型／签名扫描，不代表完整防病毒。
5. **真实批次**：Owner、许可、资料位置／敏感等级、供应商发送许可、真实格式／容量、更新／到期、授权责任、历史／备份／供应商保留及删除期限全部仍待独立确认。直接词匹配不能承诺语义召回，大规模性能也未验证。

代码复核结论：**首批实现通过代码审查；环境及发布验收未通过。** 保持首批待验收、真实批次待确认，不将整个 AIC-010 标为完成。
