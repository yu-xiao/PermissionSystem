import type { RouteRecordRaw } from 'vue-router'
import type { MenuTreeResponse } from '../api/me'

type MenuComponentLoader = NonNullable<RouteRecordRaw['component']>

interface MenuComponentEntry {
  aliases: string[]
  cacheName: string
  component: MenuComponentLoader
}

const entries: MenuComponentEntry[] = [
  {
    aliases: ['ai/provider/index', '/system/ai-providers'],
    cacheName: 'AiProvider',
    component: () => import('../views/ai/provider/index.vue'),
  },
  {
    aliases: ['sso/provider', 'sso-provider'],
    cacheName: 'SsoProvider',
    component: () => import('../views/sso/provider/index.vue'),
  },
  {
    aliases: ['sso/user-binding', 'sso-user-binding'],
    cacheName: 'SsoUserBinding',
    component: () => import('../views/sso/user-binding/index.vue'),
  },
  {
    aliases: ['sso/role-mapping', 'sso-role-mapping'],
    cacheName: 'SsoRoleMapping',
    component: () => import('../views/sso/role-mapping/index.vue'),
  },
  {
    aliases: ['sso/department-mapping', 'sso-department-mapping'],
    cacheName: 'SsoDepartmentMapping',
    component: () => import('../views/sso/department-mapping/index.vue'),
  },
  {
    aliases: ['sso/login-log', 'sso-login-log'],
    cacheName: 'SsoLoginLog',
    component: () => import('../views/sso/login-log/index.vue'),
  },
  {
    aliases: ['online-user', 'online'],
    cacheName: 'SystemOnlineUser',
    component: () => import('../views/system/online-user/index.vue'),
  },
  {
    aliases: ['system/user/index', '/system/users'],
    cacheName: 'SystemUser',
    component: () => import('../views/system/user/index.vue'),
  },
  {
    aliases: ['system/tenant/index', '/system/tenants'],
    cacheName: 'SystemTenant',
    component: () => import('../views/system/tenant/index.vue'),
  },
  {
    aliases: ['system/department/index', '/system/departments'],
    cacheName: 'SystemDepartment',
    component: () => import('../views/system/department/index.vue'),
  },
  {
    aliases: ['system/dict/index', '/system/dicts'],
    cacheName: 'SystemDict',
    component: () => import('../views/system/dict/index.vue'),
  },
  {
    aliases: ['system/config/index', '/system/configs'],
    cacheName: 'SystemConfig',
    component: () => import('../views/system/config/index.vue'),
  },
  {
    aliases: ['system/number-rule/index', '/system/number-rules'],
    cacheName: 'SystemNumberRule',
    component: () => import('../views/system/number-rule/index.vue'),
  },
  {
    aliases: ['system/state-machine/index', '/system/state-machines'],
    cacheName: 'SystemStateMachine',
    component: () => import('../views/system/state-machine/index.vue'),
  },
  {
    aliases: ['system/print-template/index', '/system/print-templates'],
    cacheName: 'SystemPrintTemplate',
    component: () => import('../views/system/print-template/index.vue'),
  },
  {
    aliases: ['system/file/index', '/system/files'],
    cacheName: 'SystemFile',
    component: () => import('../views/system/file/index.vue'),
  },
  {
    aliases: ['system/role/index', '/system/roles'],
    cacheName: 'SystemRole',
    component: () => import('../views/system/role/index.vue'),
  },
  {
    aliases: ['system/menu/index', '/system/menus'],
    cacheName: 'SystemMenu',
    component: () => import('../views/system/menu/index.vue'),
  },
  {
    aliases: ['system/permission/index', '/system/permissions'],
    cacheName: 'SystemPermission',
    component: () => import('../views/system/permission/index.vue'),
  },
  {
    aliases: ['system/operation-log/index', '/system/operation-logs'],
    cacheName: 'SystemOperationLog',
    component: () => import('../views/system/operation-log/index.vue'),
  },
  {
    aliases: ['system/login-log/index', '/system/login-logs'],
    cacheName: 'SystemLoginLog',
    component: () => import('../views/system/login-log/index.vue'),
  },
  {
    aliases: ['system/outbox-message/index', '/system/outbox-messages'],
    cacheName: 'SystemOutboxMessage',
    component: () => import('../views/system/outbox-message/index.vue'),
  },
  {
    aliases: ['system/inbox-message/index', '/system/inbox-messages'],
    cacheName: 'SystemInboxMessage',
    component: () => import('../views/system/inbox-message/index.vue'),
  },
  {
    aliases: ['system/dead-letter-message/index', '/system/dead-letter-messages'],
    cacheName: 'SystemDeadLetterMessage',
    component: () => import('../views/system/dead-letter-message/index.vue'),
  },
  {
    aliases: ['system/health/index', '/system/health'],
    cacheName: 'SystemHealth',
    component: () => import('../views/system/health/index.vue'),
  },
  {
    aliases: ['system/job/index', '/system/jobs'],
    cacheName: 'SystemJob',
    component: () => import('../views/system/job/index.vue'),
  },
  {
    aliases: ['system/notification-admin/index', '/system/notification-admin'],
    cacheName: 'SystemNotificationAdmin',
    component: () => import('../views/system/notification-admin/index.vue'),
  },
  {
    aliases: ['system/notification/index', '/system/notifications'],
    cacheName: 'SystemNotification',
    component: () => import('../views/system/notification/index.vue'),
  },
  {
    aliases: ['system/scheduled-task/index', '/system/scheduled-tasks'],
    cacheName: 'SystemScheduledTask',
    component: () => import('../views/system/scheduled-task/index.vue'),
  },
  {
    aliases: ['security/policy/index'],
    cacheName: 'SecurityPolicy',
    component: () => import('../views/security/policy/index.vue'),
  },
  {
    aliases: ['security/ip-rule/index'],
    cacheName: 'SecurityIpRule',
    component: () => import('../views/security/ip-rule/index.vue'),
  },
  {
    aliases: ['security/login-failure/index'],
    cacheName: 'SecurityLoginFailure',
    component: () => import('../views/security/login-failure/index.vue'),
  },
  {
    aliases: ['integration/client/index'],
    cacheName: 'IntegrationClient',
    component: () => import('../views/integration/client/index.vue'),
  },
  {
    aliases: ['integration/webhook/index'],
    cacheName: 'IntegrationWebhook',
    component: () => import('../views/integration/webhook/index.vue'),
  },
  {
    aliases: ['integration/log/index'],
    cacheName: 'IntegrationLog',
    component: () => import('../views/integration/log/index.vue'),
  },
  {
    aliases: ['workflow/definition/index'],
    cacheName: 'WorkflowDefinition',
    component: () => import('../views/workflow/definition/index.vue'),
  },
  {
    aliases: ['workflow/task/todo'],
    cacheName: 'WorkflowTaskTodo',
    component: () => import('../views/workflow/task/todo.vue'),
  },
  {
    aliases: ['workflow/task/done'],
    cacheName: 'WorkflowTaskDone',
    component: () => import('../views/workflow/task/done.vue'),
  },
  {
    aliases: ['workflow/instance/my-started'],
    cacheName: 'WorkflowMyStarted',
    component: () => import('../views/workflow/instance/my-started.vue'),
  },
  {
    aliases: ['workflow/cc/index'],
    cacheName: 'WorkflowCc',
    component: () => import('../views/workflow/cc/index.vue'),
  },
  {
    aliases: ['workflow/business-binding/index'],
    cacheName: 'WorkflowBusinessBinding',
    component: () => import('../views/workflow/business-binding/index.vue'),
  },
  {
    aliases: ['report/definition/index'],
    cacheName: 'ReportDefinition',
    component: () => import('../views/report/definition/index.vue'),
  },
  {
    aliases: ['report/viewer/index'],
    cacheName: 'ReportViewer',
    component: () => import('../views/report/viewer/index.vue'),
  },
  {
    aliases: ['demo/approval-order/index'],
    cacheName: 'DemoApprovalOrder',
    component: () => import('../views/demo/approval-order/index.vue'),
  },
  {
    aliases: ['demo/business-order/index'],
    cacheName: 'DemoBusinessOrder',
    component: () => import('../views/demo/business-order/index.vue'),
  },
]

const registry = new Map<string, MenuComponentEntry>()
for (const entry of entries) {
  for (const alias of entry.aliases) {
    registry.set(normalizeKey(alias), entry)
  }
}

export function resolveMenuComponent(menu: Pick<MenuTreeResponse, 'component' | 'path'>) {
  return findEntry(menu)?.component ?? (() => import('../views/RoutePlaceholder.vue'))
}

export function resolveMenuCacheName(menu: Pick<MenuTreeResponse, 'component' | 'path'>) {
  return findEntry(menu)?.cacheName ?? 'RoutePlaceholder'
}

export function buildMenuRoutes(menuTree: MenuTreeResponse[]): RouteRecordRaw[] {
  return menuTree.flatMap((menu) => {
    const current = menu.path
      ? [
          {
            path: normalizePath(menu.path),
            name: `Menu_${menu.id}`,
            meta: {
              title: menu.name,
              icon: menu.icon,
              hidden: menu.visible === false,
              permissionCode: menu.permissionCode,
              order: menu.sort,
              noCache: false,
              cacheName: resolveMenuCacheName(menu),
            },
            component: resolveMenuComponent(menu),
          } satisfies RouteRecordRaw,
        ]
      : []

    return [...current, ...buildMenuRoutes(menu.children ?? [])]
  })
}

function findEntry(menu: Pick<MenuTreeResponse, 'component' | 'path'>) {
  const componentKey = normalizeKey(menu.component)
  const pathKey = normalizeKey(menu.path)
  return registry.get(componentKey) ?? registry.get(pathKey)
}

function normalizeKey(value?: string) {
  return (value ?? '').trim().replace(/^\/+/, '').toLowerCase()
}

function normalizePath(path: string) {
  return path.replace(/^\/+/, '')
}
