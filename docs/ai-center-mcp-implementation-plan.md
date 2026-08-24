# AI 中心与 MCP 分阶段实施规划

## 文档信息

| 项目 | 内容 |
| --- | --- |
| 文档状态 | 已确认范围，分阶段实施 |
| 版本 | v1.0 |
| 日期 | 2026-08-19 |
| 关联设计 | [ai-center-mcp-design.md](./ai-center-mcp-design.md) |
| 首期模型 | OpenAI Compatible 接口 |
| 首期业务单据 | `DemoBusinessOrder` |
| MCP 部署 | 独立 `PermissionSystem.McpServer` Host，复用现有业务内核 |

## 1. 实施结论

AI 中心作为 PermissionSystem 内部的一个业务模块建设，负责模型配置、会话、Agent 编排、工具治理和前端聊天入口。

MCP 采用独立 Host 部署，但不复制业务逻辑、权限逻辑或数据库访问逻辑。MCP Host 通过共享的 Application 工具服务访问现有业务能力，保持以下依赖方向：

```text
PermissionSystem.Api / PermissionSystem.McpServer / Worker
                         -> Application
                         -> Domain
Infrastructure 实现 Application/Domain 定义的接口
```

首期不实现通用低代码 Agent、不允许模型执行任意 SQL、不开放无需确认的自动制单，也不开放写入型 MCP Tool。

## 2. 首期目标与边界

### 2.1 必须交付

1. 在系统全局 `AppHeader` 增加 AI 聊天按钮，打开抽屉或弹窗。
2. 支持配置一个或多个 OpenAI Compatible 模型：Base URL、API Key、模型名、默认模型、启用状态和调用参数。
3. API Key 使用现有配置加密能力保存，前端和日志只显示掩码。
4. 支持用户进行多轮对话，AI 调用受控的只读业务工具。
5. 支持生成 `DemoBusinessOrder` 草稿，展示结构化字段、校验结果和缺失信息。
6. 用户明确确认后，调用现有 `DemoBusinessOrder` Application Service 创建正式单据。
7. 新增独立 `PermissionSystem.McpServer` Host，首期提供只读问数工具。
8. MCP 工具强制执行 OpenIddict/API Client 认证、租户隔离、权限、数据范围、限流和审计。

### 2.2 首期明确不做

- 任意 SQL、任意表名、任意 URL 或反射式方法调用。
- 通过模型直接提交、审批、删除或作废业务单据。
- 通过 MCP 修改业务数据。
- 自主循环 Agent、定时 Agent、知识库/RAG 和跨租户问数。
- 浏览器直接连接 MCP Server。

## 3. 目标架构

```text
Vue AI Chat
    -> PermissionSystem.Api
       -> AI Center Application
          -> OpenAI Compatible Model Gateway
          -> Shared AI Tool Registry
             -> Read Tools / DemoBusinessOrder Draft Tools
             -> PermissionSystem.McpServer Adapter
                -> Application Services
                   -> Domain + Infrastructure
```

### 3.1 AI 中心职责

- 维护会话、消息、Run、工具调用和模型用量。
- 根据当前用户、租户和权限生成可用工具目录。
- 让模型只返回结构化工具调用，不接受模型提供的身份和租户参数。
- 对工具参数执行 JSON Schema、业务规则、权限和数据范围校验。
- 对写操作执行草稿、确认、幂等和审计流程。

### 3.2 MCP Server 职责

- 提供 MCP Streamable HTTP 入口；`stdio` 仅用于本地诊断。
- 将 MCP 身份映射为当前租户和外部客户端权限。
- 发布经过审核的只读工具和数据集描述。
- 调用共享 Application 工具服务，不访问 `AppDbContext`。
- 独立记录协议请求、Tool 调用、耗时、状态码和客户端信息。

## 4. 分阶段路线图

### P0：技术验证与安全门槛

目标：证明 OpenAI Compatible、MCP SDK、OpenIddict 和当前分层可以组合运行。

交付物：

- `PermissionSystem.McpServer` 最小 Host 和健康检查。
- 一个不含敏感数据的 `list_datasets` 或字典只读 Tool。
- OpenAI Compatible 模型客户端最小实现，具备超时、取消和错误映射。
- MCP 专用 audience/scope 或等效的现有 OpenIddict 资源配置设计。
- Tool 输入/输出 Schema、错误码和 TraceId 约定。
- 模型供应商数据驻留、保留、不训练和密钥轮换规则。

验收：

- 未认证、错误租户、停用租户均不能调用 Tool。
- MCP Host 不包含业务流程和直接数据库访问。
- 请求、异常和日志中不出现 API Key、Token、密码或连接串。
- 模型超时、取消、429 和 5xx 能转换为稳定的 ApiResult/MCP 错误。

### P1：内部 AI 聊天与只读问数 MVP

目标：在白名单租户和角色范围内提供可审计的内部问数。

交付物：

- `AiProviderConfig`、`AiConversation`、`AiMessage`、`AiRun`、`AiToolInvocation`、`AiUsageLog`。
- OpenAI Compatible Provider、默认模型和连通性测试。
- `AiController`、会话 API、聊天 API，以及 SignalR 或流式响应机制。
- 全局聊天按钮、聊天弹窗、会话列表、工具状态、来源和错误状态。
- 5 至 10 个细粒度只读 Tool；优先复用已审核的 Report Dataset。
- 工具级权限、租户过滤、数据范围校验、调用审计和 Token 限额。
- AI 运行取消、超时、重试、限流、降级和 Kill Switch。

验收：

- 回答只能来自已注册 Tool，并展示数据来源、查询时间和完整性状态。
- 跨租户、越权、超出数据范围的查询全部拒绝。
- 模型不可用或 Tool 失败时不得生成伪造数据。
- 前端刷新、取消和重复发送不会造成未记录的 Run。

### P2：`DemoBusinessOrder` 草稿生成

目标：从自然语言生成可校验、可预览的业务单据草稿，不直接落正式单据。

交付物：

- `DemoBusinessOrder` 字段 Schema、字段说明、必填规则和关联对象查询 Tool。
- `IAiBusinessActionHandler` 或等价的草稿处理抽象。
- 意图识别、字段提取、缺失信息澄清和结构化草稿生成。
- 草稿校验、错误定位、PayloadHash、过期时间和权限重新校验。
- 前端草稿预览和字段修改界面。

验收：

- 模型自由文本不能直接成为写入载荷。
- 客户、商品、数量、价格等关键字段必须通过服务端重新查询和校验。
- 草稿校验失败不产生正式业务数据。
- 草稿修改后旧的校验和确认状态立即失效。

### P3：人工确认后创建 `DemoBusinessOrder`

目标：在明确确认后复用现有业务服务创建正式单据。

前置条件：确认现有业务服务的权限、编号规则、状态机、工作流、幂等和并发控制已满足要求。

交付物：

- 一次性确认凭证或确认版本号。
- 创建操作的权限重新检查和敏感操作验证。
- 幂等键、并发令牌和重复提交保护。
- 正式创建后的单据编号、状态、跳转链接和审计结果。
- 失败补偿、人工处理和完整 AI Run 关联。

验收：

- 无确认、确认过期、草稿变化或权限变化均不能创建。
- 并发重复请求只产生一张有效单据。
- 正式创建复用 `DemoBusinessOrder` 现有 Application Service，AI 不复制业务规则。
- 创建结果可从操作日志、AI Run 和业务单据反向追踪。

### P4：MCP 独立服务化与外部智能问数

目标：让外部受信系统通过 MCP 访问 PermissionSystem 的只读业务数据。

交付物：

- 独立 `PermissionSystem.McpServer` Docker 服务和健康检查。
- `list_datasets`、`describe_dataset`、`query_dataset`、`get_document` 等只读 Tool。
- OpenIddict OAuth Client Credentials；必要时兼容现有 ApiClient 密钥认证。
- 外部客户端、租户、数据集和 Tool Scope 绑定。
- MCP 请求限流、IP 白名单、外部调用日志、脱敏和告警。
- MCP 协议契约测试、认证授权测试和跨租户测试。

验收：

- 外部客户端只能看到已授权的数据集和字段。
- 数据查询强制加入租户条件和资源上限。
- MCP Server 故障或数据源超时时返回可识别的部分失败，不泄露内部异常。
- MCP Server 可独立重启和扩容，主 API 不受协议升级影响。

### P5：企业化增强

- 多模型路由、供应商故障切换和成本中心。
- 评测集、灰度发布、质量趋势和用户反馈闭环。
- ERP/WMS 外部 MCP 连接器和受控跨系统问数。
- 经审批的写入型 MCP Action Tool。
- 知识库/RAG，但与结构化业务查询分开治理。

## 5. 预计代码与数据库影响

### 5.1 后端

首期按现有项目结构新增命名空间，不一次性创建所有未来模块：

```text
backend/PermissionSystem.Application/AiCenter/
backend/PermissionSystem.Application/AiTools/
backend/PermissionSystem.Infrastructure/Ai/
backend/PermissionSystem.Infrastructure/Configurations/Ai*.cs
backend/PermissionSystem.Domain/Entities/Ai*.cs
backend/PermissionSystem.Api/Controllers/AiController.cs
backend/PermissionSystem.Api/Hubs/AiHub.cs
backend/PermissionSystem.McpServer/
```

`PermissionSystem.McpServer` 复用 Application/Infrastructure 的注册方式，协议适配代码只放在 MCP 项目中。

### 5.2 前端

```text
frontend/permission-admin/src/api/ai.ts
frontend/permission-admin/src/stores/ai.ts
frontend/permission-admin/src/components/AiChatDialog/
frontend/permission-admin/src/views/system/ai-provider/
frontend/permission-admin/src/layouts/components/AppHeader.vue
```

聊天按钮放在全局 `AppHeader`，保证首页和后台业务页面均可访问；模型密钥管理页面只对具备管理权限的用户显示。

### 5.3 数据库

数据库变更是分阶段的，不能在首个提交中一次性加入全部未来实体：

| 阶段 | 主要实体 |
| --- | --- |
| P1 | `AiProviderConfig`、`AiConversation`、`AiMessage`、`AiRun`、`AiToolInvocation`、`AiUsageLog` |
| P2 | `AiDocumentDraft`、草稿校验/确认记录 |
| P4 | MCP Server、客户端绑定、数据集授权和 MCP 审计扩展 |

所有实体默认继承 `BaseEntity`，索引至少包含 `TenantId`、状态和创建时间；API Key 使用现有加密保护器，迁移文件不得写入真实密钥。

## 6. 权限与安全基线

建议权限码：

```text
ai:chat:use
ai:provider:view
ai:provider:create
ai:provider:update
ai:provider:delete
ai:provider:test
ai:conversation:view
ai:document:draft
ai:document:execute
ai:tool:query
mcp:dataset:query
```

安全约束：

- 模型输出、MCP Tool 描述和外部结果均视为不可信输入。
- Tool 服务端重新计算当前用户、租户、权限和数据范围。
- 不把用户 Access Token、系统密钥或数据库连接串发送给模型。
- 生产模型只允许访问经过数据分级和脱敏的数据。
- AI 日志保存摘要、版本、参数哈希和结果元数据，避免默认保存完整敏感内容。
- 所有模型、Tool、MCP Server 和 Agent 均具备独立启停开关。

## 7. 测试与验收策略

每个阶段完成后分别运行，不等待全部功能完成后再集中验证：

- 单元测试：Provider 配置、Tool Schema、参数转换、草稿校验、确认状态机。
- API 集成测试：认证、权限、租户、限流、错误映射和取消。
- MCP 契约测试：初始化、工具发现、调用、Schema、超时和协议错误。
- 数据安全测试：跨租户、数据范围、敏感字段、越权和提示词注入。
- 业务测试：DemoBusinessOrder 草稿、并发确认、幂等创建和工作流关联。
- 前端测试：聊天弹窗、会话切换、流式/分段响应、失败重试和权限隐藏。
- 性能测试：模型调用 P95、Tool 查询 P95、并发 Run、日志写入和 MCP 扩容。

## 8. 每阶段开发流程

每个阶段开始前必须单独输出并确认：

1. 文件级实施清单和依赖变更。
2. DBA 数据结构、索引、迁移和回滚评审。
3. API/前端契约和权限矩阵。
4. 测试用例及验收数据。

每个阶段结束后由 Reviewer 检查需求符合度、分层边界、租户隔离、敏感信息、迁移兼容性和测试结果。阶段未通过时，不扩大下一阶段能力。

## 9. 当前待确认事项

三项首期方向已经确认，后续开发前仍需在对应阶段明确：

- OpenAI Compatible 服务的实际 Base URL、部署位置、数据保留和不训练策略。
- DemoBusinessOrder 的可由自然语言填写字段、字段默认值和关联对象选择规则。
- 首批试点租户、角色、允许的数据范围和模型调用配额。
- MCP 外部客户端的身份方式、租户映射和首批允许数据集。
- 会话原文、AI Run、模型调用日志和 MCP 审计日志的保留周期。

## 10. 推荐开发顺序

建议按 `P0 → P1 → P2 → P3 → P4` 顺序实施。第一轮开发只做 P0 的技术验证和 P1 的最小聊天闭环；P1 通过权限、租户、审计和只读问数验收后，再进入 DemoBusinessOrder 草稿；正式创建单据通过并发、幂等和确认评审后，最后开放独立 MCP Server 的外部访问。

