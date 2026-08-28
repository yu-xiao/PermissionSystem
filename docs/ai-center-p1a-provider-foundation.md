# AI 中心 P1-A Provider 与持久化基础

## 1. 范围

P1-A 只交付 AI 中心持久化基础、Provider 安全管理、权限和全局 Kill Switch。
聊天编排、只读 Tool、SignalR 和前端工作台在 P1-B/P1-C 实施。

数据库新增 6 张表：

- `ai_provider_config`
- `ai_conversation`
- `ai_message`
- `ai_run`
- `ai_tool_invocation`
- `ai_usage_log`

所有实体继承 `BaseEntity`，沿用租户过滤、审计、软删除和 RowVersion。表之间使用受限外键，不使用级联删除。

## 2. 配置

```json
{
  "Ai": {
    "Enabled": false,
    "AllowedTenantIds": [],
    "ConversationRetentionDays": 30,
    "AuditRetentionDays": 180
  }
}
```

- `Enabled` 是全局 Kill Switch，默认关闭。
- `AllowedTenantIds` 是显式试点租户白名单；空数组表示所有租户均不可调用模型。
- 会话原文默认保留 30 天，Run/Tool 审计默认保留 180 天。
- Provider API Key 使用 `Security:SystemConfigEncryptionKey` 加密，API 和日志只显示掩码。
- 生产开启 AI 时，加密密钥必须至少 32 字符，且租户白名单不能为空。

## 3. Provider API

| 方法 | 路径 | 权限 |
| --- | --- | --- |
| `GET` | `/api/ai/providers` | `ai:provider:view` |
| `GET` | `/api/ai/providers/{id}` | `ai:provider:view` |
| `POST` | `/api/ai/providers` | `ai:provider:create` |
| `PUT` | `/api/ai/providers/{id}` | `ai:provider:update` |
| `DELETE` | `/api/ai/providers/{id}` | `ai:provider:delete` |
| `PUT` | `/api/ai/providers/{id}/enabled` | `ai:provider:update` |
| `POST` | `/api/ai/providers/{id}/default` | `ai:provider:update` |
| `POST` | `/api/ai/providers/{id}/test` | `ai:provider:test` |

所有写请求要求 `X-Idempotency-Key`。Provider Code 在租户内唯一，每个租户最多一个默认 Provider。

连通性测试只有同时满足以下条件时才会发起外部请求：

1. 全局 AI 已开启。
2. Provider 所属租户在白名单内。
3. Provider 已启用。
4. Base URL 使用允许的协议且 Host 精确命中白名单。
5. DNS 解析结果通过私网、Loopback、Link-local 和保留地址校验。

## 4. 权限

P1-A 初始化以下权限，并沿用现有机制授予默认超级管理员：

- `ai:chat:use`
- `ai:conversation:view`
- `ai:tool:query`
- `ai:provider:view`
- `ai:provider:create`
- `ai:provider:update`
- `ai:provider:delete`
- `ai:provider:test`

普通角色不会自动获得 AI 权限。后续授权仍需与原业务查看权限、Tool 权限和数据范围取交集。

## 5. 迁移与回滚

迁移：`AddAiCenterP1`。

上线顺序：

1. 保持 `Ai:Enabled=false`。
2. 应用数据库迁移。
3. 配置强加密密钥。
4. 创建并测试 Provider。
5. 配置试点租户白名单。
6. P1-B/P1-C 验收后再开启全局开关。

应用回滚时先关闭 Kill Switch，再回滚应用。生产表包含审计数据，不应直接执行迁移 `Down`；需要结构回滚时先备份并完成数据保留评审。
