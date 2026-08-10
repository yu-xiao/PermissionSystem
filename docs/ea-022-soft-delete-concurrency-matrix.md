# EA-022 软删除唯一约束与通用并发矩阵

完成日期：2026-08-10

## 统一规则

- 业务可复用键：只约束活动数据，使用 SQL Server 过滤唯一索引 `[IsDeleted] = 0`；删除后允许创建同键新记录，并允许重复执行“创建—删除—再创建”。
- 永久唯一键：幂等、会话、安全凭据、租户身份等键不因软删除释放，继续使用未过滤唯一索引。
- 并发：所有 `BaseEntity` 统一配置 SQL Server `rowversion`。主要管理配置的查询响应返回 Base64 `concurrencyToken`，更新请求回传令牌；令牌不一致和 EF 保存竞态统一映射为 HTTP 409。
- 兼容：更新令牌暂为可选，仓库外旧调用方不传令牌时仍可调用；新版管理端编辑页均回传令牌。
- 回滚：若上线后已复用业务键，旧的未过滤唯一索引无法直接恢复。Down 迁移会先检查并用 SQL 错误 51000 阻断，不自动删除、合并或改写历史数据。

## 删除后允许复用的业务键

| 表 | 唯一键 | 新索引策略 | 并发策略 | 回滚限制 |
|---|---|---|---|---|
| `Users` | TenantId + NormalizedUserName | 活动数据过滤唯一 | BaseEntity rowversion；用户编辑回传令牌 | 任意历史同名记录会阻断旧索引恢复 |
| `Roles` | TenantId + Code | 活动数据过滤唯一 | BaseEntity rowversion；角色编辑回传令牌 | 同左 |
| `Permissions` | TenantId + Code | 活动数据过滤唯一 | BaseEntity rowversion；权限编辑回传令牌 | 同左 |
| `Departments` | TenantId + Code | 活动数据过滤唯一 | BaseEntity rowversion；部门编辑回传令牌 | 同左 |
| `DictionaryTypes` | TenantId + Code | 活动数据过滤唯一 | BaseEntity rowversion；类型编辑回传令牌 | 同左 |
| `DictionaryItems` | TenantId + TypeCode + Value | 活动数据过滤唯一 | BaseEntity rowversion；字典项编辑回传令牌 | 同左 |
| `SystemConfigs` | TenantId + ConfigKey | 活动数据过滤唯一 | BaseEntity rowversion；配置编辑回传令牌 | 同左 |
| `NumberRules` | TenantId + RuleCode | 活动数据过滤唯一 | BaseEntity rowversion；规则编辑回传令牌 | 同左 |
| `NotificationTemplates` | TenantId + Code | 活动数据过滤唯一 | BaseEntity rowversion；模板编辑回传令牌 | 同左 |
| `ScheduledTasks` | TenantId + Code | 活动数据过滤唯一 | BaseEntity rowversion；任务编辑回传令牌 | 同左 |
| `IpAccessRules` | TenantId + RuleType + IpPattern | 活动数据过滤唯一 | BaseEntity rowversion；规则编辑回传令牌 | 同左 |
| `LoginFailureRecords` | TenantId + UserName + IpAddress | `[IsDeleted] = 0 AND [IpAddress] IS NOT NULL` | BaseEntity rowversion | 非空 IP 历史同键会阻断；空 IP 保持不约束 |
| `WorkflowDefinitions` (`wf_definition`) | TenantId + Code + Version | 活动数据过滤唯一 | BaseEntity rowversion；定义和设计器回传令牌 | 同键历史版本会阻断旧索引恢复 |
| `WorkflowNodes` (`wf_node`) | TenantId + DefinitionId + NodeKey | 活动数据过滤唯一 | BaseEntity rowversion；定义令牌保护设计器整体保存 | 同左 |
| `WorkflowBusinessBindings` (`wf_business_binding`) | TenantId + BusinessType | 活动数据过滤唯一 | BaseEntity rowversion；绑定编辑回传令牌 | Down 按旧 TenantId + BusinessType + IsDeleted 检查 |
| `StateMachineDefinitions` | TenantId + BusinessType | 活动数据过滤唯一 | BaseEntity rowversion；状态机编辑回传令牌 | 同左 |
| `StateDefinitions` | TenantId + MachineId + StateCode | 活动数据过滤唯一 | BaseEntity rowversion；状态编辑回传令牌 | 同左 |
| `ReportDefinitions` | TenantId + ReportCode | 活动数据过滤唯一 | BaseEntity rowversion；报表编辑回传令牌 | 同左 |
| `ReportQueryParams` | TenantId + ReportId + ParamCode | 活动数据过滤唯一 | BaseEntity rowversion；随报表事务更新 | 同左 |
| `PrintTemplates` | TenantId + TemplateCode | 活动数据过滤唯一 | BaseEntity rowversion；列表和设计器回传令牌 | 同左 |
| `SsoProviders` (`sso_provider`) | TenantId + ProviderCode | 活动数据过滤唯一 | BaseEntity rowversion；Provider 编辑回传令牌 | 同左 |
| `SsoUserBindings` (`sso_user_binding`) | TenantId + ProviderId + ExternalUserId | 活动数据过滤唯一 | BaseEntity rowversion | 同左 |
| `SsoUserBindings` (`sso_user_binding`) | TenantId + ProviderId + LocalUserId | 活动数据过滤唯一 | BaseEntity rowversion | 同左 |
| `SsoRoleMappings` (`sso_role_mapping`) | TenantId + ProviderId + ExternalRole + LocalRoleId | 活动数据过滤唯一 | BaseEntity rowversion | 同左 |
| `SsoDepartmentMappings` (`sso_department_mapping`) | TenantId + ProviderId + ExternalDepartment + LocalDepartmentId | 活动数据过滤唯一 | BaseEntity rowversion | 同左 |
| `ApiClients` | TenantId + ClientCode | 活动数据过滤唯一 | BaseEntity rowversion；客户端编辑回传令牌 | 同左 |
| `UserRoles` | TenantId + UserId + RoleId | 活动数据过滤唯一 | BaseEntity rowversion | 重复历史关系会阻断旧索引恢复 |
| `RolePermissions` | TenantId + RoleId + PermissionId | 活动数据过滤唯一 | BaseEntity rowversion | 同左 |
| `RoleMenus` | TenantId + RoleId + MenuId | 活动数据过滤唯一 | BaseEntity rowversion | 同左 |
| `RoleDataScopes` | TenantId + RoleId | 活动数据过滤唯一 | BaseEntity rowversion | 同左 |
| `UserDataScopes` | TenantId + UserId | 活动数据过滤唯一 | BaseEntity rowversion | 同左 |
| `demo_business_order` | TenantId + OrderNo | 活动数据过滤唯一 | BaseEntity rowversion | Down 按旧 TenantId + OrderNo + IsDeleted 检查 |
| `demo_approval_order` | TenantId + OrderNo | 活动数据过滤唯一 | BaseEntity rowversion | Down 按旧 TenantId + OrderNo + IsDeleted 检查 |

`Menus` 当前没有经业务确认的唯一自然键，因此 EA-022 不新增菜单唯一约束；菜单实体仍获得通用 `rowversion`，编辑页回传并发令牌。

## 删除后仍禁止复用的永久唯一键

| 表 | 唯一键 | 原因 |
|---|---|---|
| `Tenants` | Code | 平台级租户身份，禁止历史租户编码被新租户冒用 |
| `OutboxMessages` | TenantId + MessageId | 消息幂等 |
| `InboxMessages` | TenantId + MessageId + Consumer | 消费幂等 |
| `UserSessions` | SessionId | 会话身份 |
| `ApiClientSecrets` | TenantId + SecretHash | 凭据安全与审计 |
| `NumberSequences` | TenantId + RuleCode + SequenceKey | 编号序列一致性 |
| `SecurityPolicies` | TenantId | 每租户单例配置 |
| `UserNotifications` | TenantId + UserId + NotificationId | 投递关系幂等 |

## 迁移保护

- Up 在删除旧索引前逐项检查活动数据重复，冲突时抛出 SQL 错误 51000，并包含具体表/键说明。
- Down 在删除过滤索引前逐项检查旧索引语义下的历史重复；检测到上线后的键复用即阻断回滚。
- 数据冲突必须由业务确认保留、归档或改码方案后人工处理，迁移不做隐式数据修复。
