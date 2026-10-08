# AIC-012 方向 5：技术元数据导出首批实施与验收记录

> 后续规划（2026-10-07）：用户选择先规划首批与凭据第二批的[联合环境验收](ai-center-aic-012-export-environment-acceptance-plan.md)。规划已完成，实测未执行；下文原验证结果、失败／跳过证据及待验收状态保留。

> 日期：2026-10-07
> 状态：用户已回复“确认”批准具体方案；导出首批已实现、待验收。运营方向 4 的原记录保留；AIC-012 整体未完成。
> 依据：[AGENTS.md](../AGENTS.md)、[导出方案](ai-center-aic-012-export-implementation-plan.md)、[后续开发计划](ai-center-next-development-plan.md)、[方向 4 验收记录](ai-center-aic-012-acceptance.md)。代码复核通过不等于目标环境、法律合规或发布验收通过。

## 1. 改了什么 [Architect] [Developer]

在原运营页新增“导出技术元数据”，只导出当前明确活动单租户、Run 创建时间左闭右开窗口内的 Run 和关联模型 usage。默认 30 天、最多 90 天，结束最多未来 5 分钟；固定用途内部技术复核，接收人为当前获准调用者。UTF-8 JSON 由本次请求直接下载，没有服务端文件、公开链接、内容缓存或外部传输。

| 模块 | 实际实现 |
| --- | --- |
| Application | [固定契约／DTO](../backend/PermissionSystem.Application/AiCenter/AiTechnicalExportModels.cs)、[导出服务](../backend/PermissionSystem.Application/AiCenter/AiTechnicalExportService.cs)及 DI；复用仓储、查询执行器、fresh 身份／权限、限流和分布式锁抽象 |
| API | [AiOperationsController](../backend/PermissionSystem.Api/Controllers/AiOperationsController.cs) 新增 `POST /api/ai/operations/technical-export`，要求 view + export 双权限；NoStore、nosniff、attachment、安全文件名 `ai-technical-{ExportId:N}.json`，错误沿用 ApiResult／异常映射 |
| 权限／菜单 | [常量](../backend/PermissionSystem.Shared/Constants/AiCenterConstants.cs)、[平台种子](../backend/PermissionSystem.Infrastructure/SeedData/SeedDataInitializer.cs)、[新租户初始化](../backend/PermissionSystem.Application/Tenants/TenantInitializationJob.cs)新增 `ai:operations:export` 及原运营菜单下 Button |
| 前端 | [既有 API](../frontend/permission-admin/src/api/ai.ts)、[原运营页](../frontend/permission-admin/src/views/ai/operations/index.vue)新增主动确认、用途／当前租户／接收方／上限说明、Blob 校验及下载；复用 Axios、Pinia、Element Plus |
| 验证 | 新增导出核心、SQL provider 离线翻译、HTTP、条件隔离 SQL、前端 API／组件用例；同步初始化计数和权限／菜单关系断言，无新增依赖 |

Run 仅输出运行／场景／版本引用、技术时间、固定状态、有效耗时和切换次数；usage 仅输出调用／Run 引用、技术时间／序次／轮次／尝试、固定状态／路由、实际与估算 Token、有效终态估算费用／币种及缺失／无效／未决标记。未知枚举输出固定 `Unknown`，负数量为 null 并标记；Pending／Running 费用不冒充已结算，未知状态可通过 status 识别。

不导出正文、用户及会话／消息引用、Prompt、快照、请求响应 JSON、错误自由字符串、TraceId、供应商请求 ID、Model／Provider 名称、凭据或配置。查询只投影白名单标量，不加载完整实体；场景不读取名称或版本快照。金额按调用自身币种表达，不换汇、不汇总为真实供应商账单。

## 2. 授权、凭据及一致性 [Reviewer]

API 和 Application 同时检查 `ai:operations:view` 与专用 `ai:operations:export`。入口、取得准备锁后、准备审计前及返回前重新核验有效身份、fresh 角色／权限、安全戳和活动目标租户。普通用户的身份租户须与目标相同；超管须同时具备当前及 fresh 超管角色，并通过 Header／Request 明确选择一个活动目标。拒绝系统作用域、缺目标或停用目标；不接收客户端字段、租户 ID、路径或外部目标。

Run／usage 读取和存续复核通过 QueryForTenant 同时限定两侧 TenantId 与非删除，usage 关联固定 Run 集合。准备及 Prepared 保存后再次检查原 Run／usage 存续和关联；发生删除、租户或关联变化拒绝返回。多次读只形成观察窗口，期间状态／数值可更新；不承诺事务同一时点快照，也不恢复已被留存清理删除的记录。

在现有 OperationLogs 内追加最小版本化凭据：Requested 保存成功后才读取导出群体，Prepared 保存成功且最终复核通过后才返回文件。凭据保存目标租户、操作者、ExportId、固定用途／接收语义、时间区间；Prepared 另存观察时间、实际数量、字节数及完整文件 SHA-256，不存文件正文。摘要限制在现有 4,000 字符列容量内，沿用租户写保护及既有日志权限。原 AI 路径中间件不捕获请求／响应正文。

Manifest 中 PayloadSha256 校验紧凑 UTF-8 payload 对象原字节；最终文件直接写入同一段字节，避免重序列化漂移。Hash 是完整性校验，不是签名、来源认证或法律证明。Prepared 只证明当时文件已准备并保存凭据，不能证明最终响应获准或浏览器已接收；后续撤权仍拒绝响应。失败凭据尽力追加固定代码，数据库故障可能使末状态缺失；EF 保存失败后的跟踪项也可能在后续保存成功时一并落库，任何 Prepared 存在均不应被解释为送达成功。

容量为 Run 10,000、usage 50,000、最终文件 16 MiB；查询 Take(上限 + 1)、流写入有界检查，任一超限整体拒绝，无隐含截断。每 actor 每分钟 2 次、每租户每分钟 10 次，同租户同时准备 1 份，忙时立即拒绝；准备截止 10 秒，锁期限 30 秒，取消／截止／锁期限检查贯穿流程。失败审计及锁释放各最多额外 1 秒清理，原异常保留，日志不输出原数据或敏感异常。

响应只在本导出入口设置 `Access-Control-Expose-Headers: Content-Disposition`，让既有允许来源的跨域浏览器读取固定文件名；未放宽原 CORS 来源策略。前端检查 HTTP 200、JSON MIME、attachment、安全文件名及非空／大小，拒绝错误 Blob。身份／权限／租户／日期变化、KeepAlive 停用及卸载取消导出并丢弃迟到响应，Object URL 在 finally 释放；成功文案仅称已交给浏览器下载。已传输或客户端保存的副本无法撤回。

## 3. 数据影响、部署与回退 [DBA]

**无数据库结构或迁移变更；有权限／菜单种子及审计数据写入。** 未新增实体、映射、索引、回填或留存规则，没有执行实际迁移、宿主初始化、真实数据导出或清理。新查询复用既有表／索引，覆盖与执行计划仍需隔离 SQL 验收。

平台种子及新租户初始管理员沿用已有授权模式。已有活动非默认租户不会因新租户种子变更自动获权；管理员须通过既有受控管理流程补权限／Button 及已批准角色关系，复核 view + export，禁止重跑初始化或自动扩权普通角色。本轮没有实际修改任何租户授权数据。

回退应用只撤销新增入口／能力，不需数据库结构回退或清理审计；已保存 Requested／Prepared 凭据保留原事实，不改写为送达。本批沿用现有日志存储与留存，它不是 WORM 或不可变法律保留；没有实现分层归档、法律保留、恢复或副本删除证明。

Application 编译变更影响 AIC-005 BuildIdentity／发布资格。正式场景继续遵守重新冻结／评测、版本兼容及原发布门禁；不修改 Hash 或绕过资格校验。

## 4. 验证了什么 [Developer] [Reviewer]

最终后端验证使用独立 `obj/aic012-export-final2` 产物及 `--no-build`，没有修改 DLL 权限／ACL、删除旧构建目录或弱化旧测试。全量 Unit 仅对测试进程设置工作区 TEMP／TMP，并在 finally 恢复。报告位于忽略目录 `artifacts/aic-012/export/`。

| 验证 | 实际结果 | 证据／范围 |
| --- | --- | --- |
| 后端解决方案 Release build | 成功，0 error、2 条既有 ASPDEPR004／008 warning | `dotnet build backend/PermissionSystem.sln -c Release --artifacts-path obj/aic012-export-final2 -p:UseSharedCompilation=false --nologo` |
| 导出核心／SQL provider 离线翻译 | 42/42 通过 | `tests/export-core-final.trx`；核心 41 + 翻译 1，未连接 SQL |
| 全量 UnitTests | 1004 项：1003 通过、1 失败、0 跳过 | `tests/unit-final.trx`；包含本批及方向 4 核心回归，原环境失败见下文 |
| 全量 IntegrationTests | 138 项：95 通过、43 环境跳过、0 失败 | `tests/integration-final.trx`；导出 HTTP 6/6，方向 4 HTTP 7/7，本批 SQL 3 项跳过 |
| Legacy Tests | 45/45 通过 | `tests/legacy-final.trx` |
| 前端全量 Vitest | 18 文件、101/101 通过 | `npm run test:unit`；含本批新增 API 7、页面 8 项和方向 4 原用例 |
| 前端类型／production build／预算 | 通过 | `npm run build` 包含 vue-tsc；budget total `1816281`、最大 chunk `910023` bytes，既有 Element Plus 大 chunk 提示保留 |
| 前端 lint | 0 error、4 warning | `npm run lint`；UploadFile.vue 两条既有 default-prop、运营组件测试两条 one-component-per-file（测试内 KeepAlive 宿主） |
| 修改前端文件格式 | 四文件 Prettier check 通过 | API、API test、运营页、页面 test |
| AIC-004 原 Offline 回归 | 41 个变体确定性检查通过 | `evaluations/20261007-074308-49ce200db0d74b6eadaef7cfbe011922/report.json`；release gate 仍 pending／failed |

上表报告路径相对 `artifacts/aic-012/export/`。Offline 使用原 cases.json、直接运行最终编译 AiEvaluations DLL，未执行 Live 或真实模型请求。前端在最后生产变更后已完成全量测试／build；最终新增修正仅后端响应头及测试断言，不影响该前端产物。

核心覆盖白名单、双 Hash、空数据、半开时间、90 天／未来限制、稳定排序、10,000 Run 边界、记录／字节超限、实际／估算／未知／无效／未决、跨租户／软删除／关联错配、双权限／fresh 撤权／安全戳／停用、准备审计后变化、审计失败、限流／锁忙／失效、取消／截止及资源释放。HTTP 验证为 TestServer 契约，不能替代真实 OpenIddict 浏览器登录；离线 SQL 翻译不能替代真实 SQL 约束、计划、性能与并发。

全量 Unit 唯一失败仍为既有 `AiHttpTransportTests.SendAsync_RejectsUntrustedHttpsCertificate`：`X509CertificateLoader.LoadPkcs12` 导入合成测试证书时 Windows“拒绝访问”，尚未进入 TLS 拒绝断言。工作区 TEMP／TMP 下仍复现；没有跳过原测试、改 ACL 或弱化断言，不能宣称全量测试全绿。

本批三个 SQL 用例要求同时具备 `PERMISSION_SYSTEM_AIC012_SQL_TEST_CONNECTION` 与 `PERMISSION_SYSTEM_AIC012_SQL_TEST_ISOLATED=1`。夹具要求已审核迁移的隔离库、合成数据及外层事务回滚，使用真实 UserCredentialValidator 验证数据库权限；不自动迁移、不打印连接值。本轮环境缺条件，3 项全部跳过。限流使用替身、锁仅 Memory 路径，不代表 Redis 跨实例验证。

五份本批方案／计划／验收文档的 115 个相对文件引用有效，`git diff --check` 通过；未提交／推送。全量结果同时确认 AIC-012 核心 88/88、HTTP 13/13 通过，两个批次 SQL 共 6 项环境跳过。文档保持运营及导出首批待验收，不把其他方向或 P5-C／P5-D／P5-E 标为完成。

## 5. 还有什么风险与退出条件 [Reviewer]

| 未完成事项 | 责任／验收条件 |
| --- | --- |
| 隔离 SQL 结果、审计、撤权与来源变化 | 开发／DBA 在明确隔离库运行本批 3 项及相关回归；补 SQL 审计持久化失败、租户保护、最大容量、并发来源变化及实际查询计划 |
| Redis 多实例限流／锁与故障 | 运维／开发使用既有配置验收跨实例同租户排他、actor／tenant 限流、连接故障、租约失效及截止；Memory／替身不保证多实例隔离 |
| 正式规模与准备截止 | Owner／DBA／运维签认规模、延迟、内存、下载及并发目标；测查询／排序／读放大及 10 秒准备保护，16 MiB 上限不是内存或压测证明 |
| 真实认证浏览器下载 | 前端／验收验证同源与允许跨域部署、真实登录、双权限、租户切换、撤权／取消、KeepAlive、宽窄屏及真实文件保存／Hash 核对；组件及 TestServer 不等于端到端 |
| Owner／人员及下载副本规则 | 业务／安全签认内部技术复核用途、获权人员、目标租户、客户端保存／删除及副本处理；文件一旦保存无法服务端撤回 |
| 既有租户权限补齐 | 租户管理员按批准角色通过现有管理流程补 export permission／Button／关系，验证只获 view 的用户不可下载；不重跑初始化 |
| Windows 测试证书失败 | 环境维护人员查明证书导入拒绝原因后重跑原测试；未解决前保留 1 项失败记录 |
| 审计／一致性与法律用途 | 观察窗口不是快照，Prepared 不是送达凭据，SHA-256 不是签名，OperationLogs 不是 WORM；真实账单、法律保留及归档须单独明确规则并立项 |
| AIC-005 发布资格 | 发布负责人按新 BuildIdentity 重新冻结／评测与兼容核验；Offline 不代表真实模型、人工审核或 release gate 已通过 |

Reviewer 对已确认代码范围结论：**通过，导出首批待验收。** 未实施真实导出、外部传输、数据库迁移、清理、部署、commit 或 push；方向 5 的分层归档／法律保留及整个 AIC-012 尚未完成。
