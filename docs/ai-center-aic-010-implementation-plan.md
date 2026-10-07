# AIC-010：有权限边界的文档知识库实施方案

> 日期：2026-10-07
>
> 状态：用户已确认本方案；首批合成资料实现完成、待环境验收，真实批次待独立确认。实际结果见 [验收记录](ai-center-aic-010-acceptance.md)。
>
> 依据：`AGENTS.md`、[后续开发计划](ai-center-next-development-plan.md)、[总体实施计划](ai-center-mcp-implementation-plan.md)及当前代码。

## 1. 已确认范围与待确认事项 [Architect]

用户本轮选择：

- 首批使用合成资料验证；真实资料后续独立确认。
- 按“当前租户 + 知识库查询权限 + 每份文档明确允许的角色”实施；管理权限不自动获得正文读取权限。
- 真实资料更新周期、到期和撤权／删除后的保留要求，在真实批次再确认。

用户随后回复“确认”，授权本方案首批代码、测试、前端及迁移／审核 SQL 的生成。首批完成小规模文本资料的导入、版本发布、授权检索、可核验引用、撤权／删除传播及测试；工具默认关闭。此确认不授权真实资料接入或目标环境操作。

首批已确认并实施的工程限制为 UTF-8 `.txt`，单文件不超过 256 KiB、每个租户最多 100 份有效文档、每版最多 256 个片段、每片段最多 2,000 字符、单次最多返回 5 个片段。仍受现有文件大小、工具超时、模型预算和结构化封套限制约束；这些限制不代表真实资料批次的业务要求。

真实批次必须补齐资料位置、Owner、使用许可、敏感等级、是否允许发送给模型供应商、格式和容量、更新及到期规则、访问授权责任、历史／备份／供应商保留与删除要求。未补齐前不导入仓库业务资料、用户文件或生产附件。

## 2. 调研时的代码与缺口 [Architect]

调研开始时工作区无未提交改动；检索未发现现有知识库、文档片段或 Embedding 实现。

| 已核实文件（相对仓库根目录） | 当前行为与复用边界 |
| --- | --- |
| `backend/PermissionSystem.Application/Files/FileService.cs` | 已有上传大小校验、SHA-256、扫描、存储、Pending／PendingDelete 及补偿；不直接把任意已有附件作为知识来源 |
| `backend/PermissionSystem.Application/Files/FileContentScanner.cs` | 有类型检查及有限签名检查；不等同于完整防病毒、文档解析或 OCR；`.txt` 可复用，其他格式不能仅凭上传成功视为可解析 |
| `backend/PermissionSystem.Application/Files/FileBusinessAccessChecker.cs` | 无业务绑定时允许访问；业务绑定只支持 DemoBusinessOrder，未知类型拒绝；不能把知识文件存成无绑定附件 |
| `backend/PermissionSystem.Application/Abstractions/IFileBusinessAccessChecker.cs` | 当前不区分读取、上传和删除；知识库管理权限与正文读取权限分离时需要最小扩展 |
| `backend/PermissionSystem.Application/AiTools/AiReadOnlyToolRegistry.cs` | 已有工具声明、权限目录、服务端身份、超时和参数检查，可增量注册只读检索工具 |
| `backend/PermissionSystem.Application/AiCenter/AiQueryAccessGuard.cs` | 复核当前有效身份、租户、开关及权限；新工具需明确加入支持范围 |
| `backend/PermissionSystem.Infrastructure/Authentication/UserCredentialValidator.cs` | 通过数据库读取有效用户、租户、角色和权限；知识文档授权应使用当前有效角色，不能只信旧 Token 或缓存 |
| `backend/PermissionSystem.Application/AiCenter/AiStructuredResultReader.cs` | 校验封套与调用摘要并逐结果复核权限；目前不识别知识引用 |
| `backend/PermissionSystem.Application/AiCenter/AiConversationService.cs` | 模型历史仅使用用户文字；会话详情仍直接返回历史助手文字，引用从 Invocation 读取，尚无文档依赖复核 |
| `backend/PermissionSystem.Infrastructure/Ai/AiRunPersistenceFence.cs` | 已有后台 Run 租约及结算保护；新增 Run 文档依赖不能成为未保护的持久化副作用 |
| `backend/PermissionSystem.Infrastructure/Ai/AiRetentionHostedService.cs` | 已有内容及审计保留清理；新增依赖、片段和文件不能仅软删除后无限留存正文 |

主要风险是正文通过通用文件入口或历史助手回答继续可见；只在搜索接口过滤、只隐藏引用卡片或只依赖前端按钮均不满足 AIC-010。

## 3. 分批顺序与检索选择 [Architect]

### 3.1 首批内部步骤

1. **合成资料质量与检索验证。** 准备两个租户、至少两个角色的合成制度／操作说明；覆盖直接词匹配、中文、同义问法、冲突版本、过期资料、无依据、越权和含指令的资料。每个问题标记人工可核验的目标文档、版本及原文位置，记录文本切分质量、Top-5 命中、误命中、无依据处理及耗时。
2. **决定存储并实施基础用例。** 在上述验证后决定首批检索实现；复用文件扫描及补偿，完成文档元数据、逐文档角色授权、版本、片段及发布切换。
3. **接入内部只读工具和引用。** 完成服务端查询、结果卡片、源片段查看、历史重载和后台身份／租约兼容；不开放导入或删除为模型工具。
4. **复核与验收记录。** 执行单元、HTTP、隔离 SQL、前端和离线专项，分别记录真实模型及浏览器未验收项。

检索验证本身使用合成夹具，不写入生产种子、不执行付费调用。建议候选方案是现有 SQL Server 保存片段、租户与 ACL 关联过滤后的参数化文本匹配，按命中和稳定顺序取 Top-K，不先引入向量数据库或解析依赖。不得先全库取 Top-K，再在内存剔除无权文档。

候选检索需在进入数据库实现前记录验证报告：标记为首批支持的直接词／中文案例必须全部找到标注依据；同义问法单独记录覆盖率，不把合成样本通过当语义检索或真实业务质量证明。如果需要全文检索、分词、向量或新依赖才能满足选定场景，先更新方案并重新确认；不能静默替换存储或弱化验收。

### 3.2 后续真实批次

PDF／Office／扫描件解析、OCR、大规模异步索引、全文或向量存储、真实资料及外部 MCP 均另行评审。首批只验证小规模纯文本和权限机制，不把它标为整个 AIC-010 完成。

## 4. 权限、API 与职责 [Architect]

### 4.1 共用访问策略

新增 Application 知识用例与访问策略；Domain 定义状态及实体，Infrastructure 实现解析／检索适配及 EF 映射。Controller 只处理协议、DTO、权限入口和 CancellationToken，不直接访问 AppDbContext。

拟新增现有权限体系中的 `ai:knowledge:view`、`ai:knowledge:manage`、`ai:knowledge:query`，分别控制管理元数据入口、文档／版本／授权配置、正文及引用查询。`view`、`manage` 均不隐式授予正文读取。权限定义与菜单沿用现有种子，不自动为普通角色授予权限或文档访问。

读取正文同时要求当前有效租户／用户、`ai:knowledge:query`、文档处于可用状态、当前已发布且未到期版本，以及至少一条匹配当前有效角色的文档授权。AI 调用另需已有 `ai:chat:use`／`ai:tool:query`；历史会话仍需本人归属及会话查看权限。服务端从身份获取租户、角色和调用人，模型不能传递这些参数。

文档授权以 RoleId 关联本租户角色，不依赖可重命名的显示名称；停用、删除角色及移除 UserRole 均立即影响后续授权。空授权列表默认拒绝。平台 SuperAdmin／`*` 沿用原功能权限语义，但不绕过文档角色 ACL 或跨租户读取；需要正文时同样明确配置本租户角色授权。授权配置只能指定当前租户有效角色，禁止客户端把 TenantId 或 RoleId 用作跨租户授权。

管理用例可读取必要元数据及处理失败状态；新上传文件本身仍需管理入口鉴权。管理员没有正文权限时不得通过管理列表、详情、解析结果、错误信息或下载取得正文；由有正文权限的审核者核验后发布。上传者已持有其上传资料不等同于永久获得文档读取权限。

### 4.2 入口契约

| 入口 | 职责与授权 |
| --- | --- |
| `GET /api/ai/knowledge/documents` | 分页管理元数据；`view`，不返回正文或原始存储路径 |
| `POST /api/ai/knowledge/documents` | 建立草稿元数据与初始角色授权；`manage` |
| `POST /api/ai/knowledge/documents/{id}/versions` | 上传并解析合成 `.txt` 新版本；`manage`，不自动发布 |
| `PUT /api/ai/knowledge/documents/{id}/access` | 替换本租户授权；`manage`，并发令牌及授权版本检查 |
| `POST /api/ai/knowledge/documents/{id}/versions/{versionId}/publish` | 核验并原子发布；`manage` + 当前文档正文查询授权 |
| `GET /api/ai/knowledge/documents/{id}/versions/{versionId}/preview` | Ready 版本逐页审核预览；`query` + 文档 ACL，未发布版本不进入模型检索 |
| `DELETE /api/ai/knowledge/documents/{id}` | 下架与删除传播；`manage`，不能通过通用文件删除留下可检索片段 |
| `GET /api/ai/knowledge/search` | 受控搜索，仅返回可见片段；`query` + 文档 ACL |
| `GET /api/ai/knowledge/documents/{id}/versions/{versionId}/chunks/{chunkId}` | 核验固定版本引用；重新授权，旧版或失效源不返回正文 |

新增版本化只读工具 `knowledge.documents.search`／`search_knowledge_documents`。输入仅允许检索词、可选 DocumentId 和受限 Limit，不接受 URL、路径、SQL、权限或租户。参数歧义请求澄清，无结果表达无可靠依据；不反馈被过滤文档的名称、存在性或总量。无权指定资源与不存在资源使用统一不可用结果。

仅内部 API／Worker 注册工具，默认关闭，沿用 AI 租户允许列表、预算和供应商合规约束。保持现有权限助手的场景工具白名单；不静默加入旧 AIC-005 发布版本，不扩展外部 MCP 工具和 Dataset 目录。

### 4.3 通用文件入口

知识文件始终绑定明确的知识文档业务类型及 DocumentId，并且同租户；不接受任意既有 FileResourceId 为知识来源，不返回公开 URL／ObjectKey。

最小扩展文件访问检查以区分读取、上传、删除操作：列表／下载按知识正文权限及 ACL；上传按知识管理权限；知识文件删除统一通过知识用例执行失效与清理，通用删除入口应拒绝直接删除或调用同一完整传播流程。Demo 和无业务绑定旧文件保留既有行为，不扩大其他业务类型。

补偿任务只负责已授权任务的物理存储收尾，不作为用户访问入口。任何接口缓存、幂等成功重放和文件下载缓存不得跳过当前文档检查；正文响应使用禁止缓存策略。首批不增加检索结果、正文或 ACL 的 Redis／进程缓存。

## 5. 导入、版本与失效流程 [Architect]

- 先创建文档草稿和权限，再通过现有文件扫描／存储链路上传明确绑定文件；仅 Active／Clean 文件可解析。扫描或存储失败保留脱敏状态，不创建可检索版本；已有扫描是基础能力，完整扫描需求留真实批次评审。
- UTF-8 严格解码，拒绝无有效正文、二进制及超限资料；按段落切分，保留段落／行号和片段序号。不宣称 `.txt` 有 PDF 页码，不执行资料中的脚本、指令、链接或外部资源。
- SHA-256 重复检查限定同租户同文档；相同内容重试复用已有版本，不跨文档／租户去重或泄露文件存在性。失败版本不发布，新版失败不会替换仍有效旧版。
- 解析状态至少区分 Pending、Ready、Failed；发布与文档当前版本指针更新在同一事务中检查 RowVersion。Pending 在中断后可由明确管理操作重试，不自动当成 Ready；首批采用受限同步解析，不另建通用索引队列。
- 首批合成资料由测试 Owner 及测试许可标记，版本显式填写有效期以验证过期拒绝；这些标记和期限不能移用到真实资料。当前版本到期即不可检索；新版发布后旧版引用标记失效，不自动跳到新版冒充原引用。
- 撤权、下架、过期或源文件不再 Active／Clean 后，新的搜索、片段读取、模型提交和回答读取均重新检查。物理清理延迟不能使失效资料恢复可见；权限检查依赖当前数据库状态，不依赖异步索引是否清理完成。
- 首批合成文档删除建议同步标记失效并清除片段正文及相关知识 Tool／助手正文，文件进入既有删除补偿；只保留受控 ID、Hash、版本、操作时间及状态。更新内容摘要并使旧封套不可用，不能靠软删除宣称正文已清除。真实资料、备份和供应商的清理期限另行确认，用户自行输入的引用文字不能由文档关联推定完整擦除。

## 6. 引用、历史与后台 Run [Architect]

检索结果增加独立知识引用类型，记录服务端产生的 DocumentId、VersionId、ChunkId、内容 Hash、段落／行号、版本有效期和查询时间；名称和摘要只在当前可见时返回。AsOf 表达文档版本生效时间，不把查询时间包装成业务实时性。引用源查看按固定版本返回，失效时显示“来源已不可用”，不暴露失效原因对应的敏感元数据。

持久化工具封套只保存受限引用及检索条件；片段全文在当前 Run 模型调用中使用，不重复作为长期追问上下文。引用 Reader 从当前可见版本重新投影片段并校验 Hash；保持现有封套 64 KiB、上下文 4 KiB、单次读取 256 KiB 上限和旧会话兼容。

新增 Run 与所有已交给模型的文档版本之间的服务端依赖记录。依赖由会话编排层写入，不让只读 Handler 自行写表，不信模型自报引用。即使模型未引用某片段，整个回答也可能受其影响，仍需绑定该片段的版本。

历史重载、即时 Run 响应、旧等待入口、取消／重试响应及引用 API 都通过同一依赖复核：若任一文档不再授权、版本已替换／过期／删除，则整条知识回答和对应引用不可用，不只隐藏卡片。知识工具调用缺少完整依赖记录时默认不可读；原非知识 Run 保持既有行为。旧助手文字仍不送回模型，追问只继承受控检索条件并重新查询，不能复用旧片段授权。

在向模型发送片段前，以及模型返回、保存和响应投影时再次复核文档与有效身份。运行期间撤权或替换版本则丢弃相应答案，不返回旧内容；不会声称可撤回已发给供应商或已被用户看到的资料。数据库授权变更提交作为后续新读取的边界，短事务处理发布／ACL 并发，禁止为了覆盖模型耗时持有长事务锁。

后台 Run 继续使用 AIC-008 的当前身份重建、取消／租约及结算保护。新增依赖与调用／消息持久化保持一致，结算、租约丢失、迟到结果不能留下可用引用或新消息；工具失败、全部失效或无可靠命中不能仅因 ToolCallCount 大于零让模型给出事实答案。历史依赖写入与清理必须纳入 fence 和 retention 专项测试，不能直接把现有机制视为已覆盖。

安全提示词和工具说明明确：文档内容仅为不可信资料，不能变更权限、工具白名单或执行指令；实时库存、订单、权限和日志事实仍走原受控业务工具。前端按文本显示片段，引用只能导航服务端受控源入口，不执行文档 HTML 或任意链接。

## 7. 数据影响、兼容与回退 [DBA]

**首批新增 5 张表及既有 Roles、FileResources、ai_run 的租户组合唯一约束。** 实体均继承 BaseEntity，沿用 TenantId、审计、软删除和 RowVersion；已生成 `20261007033743_AddAiKnowledgeBase`、ModelSnapshot 和 [增量审核 SQL](sql/aic-010-knowledge-review.sql)，未执行实际迁移。

| 新增实体 | 最小职责／约束 |
| --- | --- |
| `AiKnowledgeDocument` | 标题、Owner／许可／分类、状态、当前版本及 ACL 版本；租户／状态索引 |
| `AiKnowledgeDocumentVersion` | DocumentId、版本号、FileResourceId、SHA-256、解析器版本、解析状态、有效期和脱敏错误码；租户／文档／版本唯一约束 |
| `AiKnowledgeChunk` | DocumentId／VersionId、序号、段落／行号、受限正文及 Hash；租户／版本／序号唯一约束 |
| `AiKnowledgeDocumentRole` | DocumentId、RoleId；租户／文档／角色的有效授权唯一约束 |
| `AiKnowledgeRunReference` | RunId／InvocationId 与 DocumentId／VersionId／ChunkId／Hash；无正文，幂等依赖唯一约束及租户／文档／Run 清理索引 |

关联写入和读取均校验同租户，数据库映射尽量以租户组合键／外键约束跨租户关联；索引按实际查询计划验证，不为 Contains 文本检索虚构普通索引的加速效果。过滤唯一索引与软删除、删除正文后的版本墓碑及文件关联兼容性在迁移评审中明确。

生成新增迁移、ModelSnapshot 及审核 SQL；不自动执行任何实际数据库迁移。隔离 SQL Server 验证关系约束、排序／中文匹配、唯一性、发布／撤权并发和删除事务。EF InMemory 不作为上述验收证据。没有环境条件时标记待验收。

不回填旧会话、不扫描旧附件、不新增生产示例资料。应用回退先关闭知识工具和管理入口；对曾使用知识库的会话，不能回到直接返回原助手正文的旧版本，除非继续保留访问屏蔽或已按批准流程清理相关正文。保持新表与审计记录，不自动执行 Down、清表或恢复宽松授权。

## 8. 文件范围与实施顺序 [Developer]

下表记录已确认的实施范围；实际文件、验证与限制见验收记录，架构／数据／授权范围变化仍需重新确认。

| 范围 | 预计文件 |
| --- | --- |
| 合成质量验证 | 新增 `evaluations/ai-center/knowledge/` 资料、标注问题及验证报告；不修改原案例黄金预期 |
| Domain | 新增 `backend/PermissionSystem.Domain/Entities/AiKnowledge*.cs` 及必要状态枚举 |
| Application | 新增 `backend/PermissionSystem.Application/AiKnowledge/` 模型、管理／检索用例、访问策略、引用和历史依赖 Reader；在 `Abstractions` 定义必要的解析／检索接口 |
| 文件安全 | 最小修改 `Application/Abstractions/IFileBusinessAccessChecker.cs`、`Application/Files/FileBusinessAccessChecker.cs`、`FileService.cs` 及知识文件删除处理 |
| 工具／会话 | 新增 `Application/AiTools/KnowledgeDocumentSearchAiToolHandler.cs`；增量修改 `AiToolModels.cs`、`AiQueryAccessGuard.cs`、`AiConversationService.cs`、结构化模型／Reader／追问处理及 DI；不重构其他查询分支 |
| Infrastructure | 新增解析／检索适配、`Configurations/AiKnowledge*.cs`；增量修改 `Data/AppDbContext.cs`、`SeedData/SeedDataInitializer.cs`、DI、Run fence／retention；新增迁移和审核 SQL |
| Shared／API | 在 `Shared/Constants/AiCenterConstants.cs` 增加权限；新增 `Api/Controllers/AiKnowledgeController.cs`，必要时增加知识文件的资源检查；复用 ApiResult／PagedResult 和现有授权、审计 |
| 前端 | 新增 `frontend/permission-admin/src/api/aiKnowledge.ts`、管理入口 `views/ai/knowledge/index.vue`、引用卡片 `components/AiKnowledgeCitationCard.vue`；增量修改 `api/ai.ts`、结构化卡片、聊天历史处理、路由组件映射和测试，复用 Axios／权限指令／Element Plus |
| 测试 | 新增 `backend/PermissionSystem.UnitTests/AiCenter/Aic010*.cs`、`PermissionSystem.IntegrationTests/AiCenter/Aic010*.cs`；补文件安全、会话／后台 Run／结构化 Reader 测试及前端对应测试 |
| 文档 | 本方案、AIC-010 验收记录、后续开发计划和总体实施计划，按实际证据更新状态 |

顺序遵循质量验证 → 数据及共用授权 → 导入／版本 → 搜索／引用 → 历史及后台兼容 → 前端 → 验证复核。首批不新增框架或运行依赖；若验证发现必须新增依赖，先说明证据和替代方案。

## 9. 验证矩阵与退出条件 [Reviewer]

1. 合成资料、中文和直接匹配、同义表达限制、无依据、冲突／过期版本、超限／无正文／重复上传及解析中断；报告只描述实际支持能力。
2. 当前有效身份及租户，查询／管理权限分离，逐文档角色矩阵，空 ACL、跨租户角色、角色停用／删除、移除成员和陈旧 Token；SuperAdmin 不绕过 ACL。
3. 搜索在数据库内先按租户和 ACL 过滤再 Top-K；搜索总量、管理元数据、源查看、通用文件列表／下载／删除、错误及缓存重放路径不泄露正文。
4. 发布并发、重复版本、失败新版不替换旧版、撤权／删除后搜索和历史返回不可用；任一知识依赖失效时整个回答屏蔽，旧非知识会话兼容。
5. 模型调用前后撤权、无命中或工具失败时拒绝事实输出、文档含指令时仍为资料；引用版本／Hash／位置必须由服务端证据核验，不能伪造来源。
6. 后台身份、重复投递、取消／超时／租约丢失／迟到答案、依赖写入及结算清理、人工重试必须重新授权；不开放草稿动作。
7. 删除后的片段／消息正文清理、源文件补偿失败及重试、文档墓碑、保留清理和回退后的历史防护。
8. 前端允许／拒绝按钮、引用固定版本、资料当文本显示、来源失效和历史刷新；旧响应无知识字段时兼容，不能只凭前端完成撤权。

确认后按实际改动执行后端 Release build、UnitTests 核心及适用回归、Tests／IntegrationTests；前端已核实脚本为 `test:unit`、`type-check`、`lint`、`build`。原 AIC-004 离线 41 变体与新增知识专项分别记录，原只读评测不能替代知识检索质量、真实模型或 SQL 证据。

只有合成闭环实现及适用检查通过才可记“首批待验收”；真实资料 Owner／许可／访问与保留规则、目标数据库／供应商／浏览器及真实问答质量全部满足退出条件后，才能判断整个 AIC-010 是否完成。

本轮 Reviewer 结论：首批实现的分层、权限与历史防护代码复核通过，环境及发布验收未通过。剩余事项包括真实 SQL 的约束／并发／执行计划、真实模型和浏览器验收，以及真实资料和供应商保留规则；不能将本地合成测试当作上述证据。

## 10. 确认记录及实际实现说明 [Architect][DBA]

用户已确认首批方案：合成 UTF-8 文本资料、上述规模限制、逐文档角色 ACL（包含 SuperAdmin 不绕过文档 ACL）、管理与查询权限分离、引用／历史整条回答失效、合成删除正文清理、默认关闭的内部工具，以及检索质量验证后才确定 SQL 文本存储实现。候选直接词匹配 8/8，同义表达 0/1，已选择现有 SQL Server 的受限文本匹配，无新增运行依赖。

确认覆盖首批业务代码、必要测试、前端和迁移／审核脚本的生成；不包含实际数据库迁移、开启目标环境配置、真实资料导入、付费模型调用、部署或 Git 提交。新依赖／存储方案、真实资料和保留规则另行确认。

管理页的角色选项沿用 `system:role:view`，未具备角色查看权限时只允许创建空 ACL 草稿，不能隐式枚举角色。发布还需正文查询权限及文档 ACL；审核预览每页 5 个片段。模型工具仍只内部注册，已有权限助手白名单未扩展，安全提示词版本为 2.4，使用知识工具的场景需要独立明确发布和评测。

最终回答提交在短 SQL 事务中持有文档、当前身份／授权及文件／版本锁并复核来源；发布也在锁内重新验证状态。历史读取核对工具封套摘要、精确 Run／Invocation／来源集合及当前权限，依赖不完整默认屏蔽。后台结算丢弃未保存的来源依赖。

合成版本到期立即失去读取资格，现有每日 retention 在明确 ValidUntil 后清除片段正文并将源文件转入删除补偿；被新版替换的版本在原到期前保留源数据，但不能再作为引用读取。助手正文沿用既有会话内容保留窗口，并且每次读取重新屏蔽失效来源；显式删除文档则同步清除相关知识回答正文。真实资料保留期限、备份及供应商删除仍未定义。
