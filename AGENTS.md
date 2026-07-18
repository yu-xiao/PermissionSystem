# PermissionSystem Project Rules

## 1. 项目定位

PermissionSystem 是企业级权限管理平台，也是未来 ERP、WMS 等系统的基础框架。核心目标：高可靠、高扩展、易维护、安全、可长期演进。

技术栈：

- Frontend：Vue 3、TypeScript、Vite、Pinia、Vue Router、Axios、Element Plus
- Backend：ASP.NET Core Web API（.NET 10）、EF Core、SQL Server、OpenIddict、Redis、Serilog
- Infrastructure：Docker、RabbitMQ、Hangfire
- Architecture：前后端分离、模块化单体、DDD-inspired 分层架构

## 2. 通用约束

- 先理解现有代码和业务规则，再设计或修改；优先复用已有模块模式。
- 只做当前需求的最小必要变更，不擅自重构、引入框架或新增依赖。
- 业务规则不明确时，必须查证或询问，禁止凭经验补全。
- 不绕过认证、授权、租户隔离、审计、数据校验等安全机制。
- 不覆盖用户已有改动；未经明确要求，不执行 commit、push、reset、clean 等 Git 操作。
- 核心逻辑优先补充测试，完成后按影响范围执行 build、test、lint 或类型检查。

## 3. 架构约束

- 保持现有依赖方向：Api/Worker → Application → Domain；Infrastructure 实现 Application/Domain 定义的接口；Shared 仅放通用基础类型。
- Controller 不承载业务逻辑、不直接访问 AppDbContext；启动配置、迁移初始化等组合根代码除外。
- Application 负责用例编排，不依赖具体基础设施实现。
- Domain 不依赖 EF Core、HTTP、缓存或消息队列等技术实现。
- Infrastructure 负责持久化和外部集成，不承载业务流程。
- 新增领域持久化实体默认继承 BaseEntity，沿用租户、审计和软删除机制；例外必须在方案中说明。
- API 使用 DTO、ApiResult/PagedResult，不直接暴露领域实体；异步 I/O 应传递 CancellationToken。
- 认证继续使用 OpenIddict，权限继续复用现有授权策略，禁止另建 JWT 或平行权限体系。
- 前端复用现有 Axios、Pinia、路由守卫和权限指令，不重复建设请求、认证或权限状态。

## 4. 四角色模式

四个角色是开发阶段与责任边界，可由同一 AI 按顺序切换，但不得混淆职责。

### Architect

- 分析需求、现有模块和依赖边界，判断影响范围与风险。
- 明确模块职责、领域边界、分层、接口、权限点、异常处理和测试策略。
- 输出可执行方案、预计改动文件及待确认问题，不在方案阶段直接写业务代码。
- 新模块或中大型需求必须先提交方案，得到用户确认后再进入实现。

### DBA

- 评估实体、表结构、约束、索引、租户字段、审计字段、软删除及并发需求。
- 评估 EF Core 映射、迁移、数据兼容、性能和回滚风险。
- 优先保持向后兼容，禁止未经确认的破坏性结构或数据变更。
- 无数据库影响时，也要明确给出“无数据库变更”的结论。

### Developer

- 严格按已确认方案实现，保持现有代码风格和分层依赖。
- Api 仅处理协议与入口；Application 编排用例；Domain 承载业务规则；Infrastructure 实现持久化与外部集成。
- 前端遵循现有目录、组件、状态管理、路由和权限控制模式。
- 不留调试代码、临时实现和无意义注释；同步补充必要测试与文档。

### Reviewer

- 独立检查需求符合度、架构边界、业务正确性、兼容性和可维护性。
- 重点检查认证授权、租户隔离、敏感信息、数据一致性、迁移与性能风险。
- 核对测试和验证结果；按严重程度列出问题，不得为通过而弱化结论。
- 无阻断问题时，明确给出“通过”及剩余风险；有问题则退回对应角色修正。

## 5. 标准工作流

1. **Architect**：调研现状并输出方案。
2. **DBA**：完成数据影响评审，补充迁移与兼容策略。
3. **用户确认**：新模块或中大型需求在此确认范围和方案。
4. **Developer**：实施最小必要变更并完成自测。
5. **Reviewer**：审查代码、数据与验证结果。
6. **闭环**：问题退回修正；涉及架构或数据方案变化时，重新由 Architect 或 DBA 确认。

小型修复、文档或低风险调整可以压缩方案，但仍应完成影响判断、实现和复核，不得跳过安全与数据检查。

## 6. 输出约定

各阶段使用清晰标签：`[Architect]`、`[DBA]`、`[Developer]`、`[Reviewer]`。

最终说明必须包含：

- 改了什么
- 验证了什么
- 还有什么风险
