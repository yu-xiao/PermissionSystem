<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import type { AiPermissionDiagnosticResult } from '../api/ai'
import { useAuthStore } from '../stores/auth'

const props = defineProps<{ diagnostic: AiPermissionDiagnosticResult }>()
const authStore = useAuthStore()
const router = useRouter()
const result = computed(() => props.diagnostic.data)
const conclusionLabels = {
  Allowed: '允许（已知检查范围）',
  Denied: '拒绝（已知检查范围）',
  Limited: '受限',
  InsufficientEvidence: '证据不足',
} as const
const kindLabels = { Menu: '菜单', Permission: '权限要求', DataScope: '数据范围' } as const
const entryRoutes: Record<string, { path: string; permission: string; label: string }> = {
  users: { path: '/system/users', permission: 'system:user:view', label: '用户配置' },
  roles: { path: '/system/roles', permission: 'system:role:view', label: '角色配置' },
  menus: { path: '/system/menus', permission: 'system:menu:view', label: '菜单配置' },
  permissions: {
    path: '/system/permissions',
    permission: 'system:permission:view',
    label: '权限配置',
  },
}
const entries = computed(() =>
  result.value.suggestedEntries.flatMap((entry) => {
    if (!Object.hasOwn(entryRoutes, entry.code)) return []
    const route = entryRoutes[entry.code]
    return route &&
      authStore.hasPermission(route.permission) &&
      router.getRoutes().some((item) => item.path === route.path)
      ? [{ code: entry.code, ...route }]
      : []
  }),
)

function statusLabel(status: string) {
  return status === 'Passed' ? '通过' : status === 'Failed' ? '未满足' : '未评估'
}

async function openEntry(code: string) {
  const entry = entries.value.find((item) => item.code === code)
  if (entry) await router.push(entry.path)
}
</script>

<template>
  <article class="permission-diagnostic-card" aria-label="服务端权限诊断证据">
    <template v-if="result.version === 1">
      <div class="diagnostic-header">
        <strong>权限排障 · {{ kindLabels[result.target.kind] }}</strong>
        <el-tag
          :type="
            result.conclusion === 'Denied'
              ? 'danger'
              : result.conclusion === 'Allowed'
                ? 'success'
                : 'warning'
          "
        >
          {{ conclusionLabels[result.conclusion] }}
        </el-tag>
      </div>
      <p class="diagnostic-summary">{{ result.summary }}</p>
      <dl class="diagnostic-target">
        <dt>目标用户</dt>
        <dd>{{ result.target.userId }}</dd>
        <template v-if="result.target.menuId"
          ><dt>菜单</dt>
          <dd>{{ result.target.menuId }}</dd></template
        >
        <template v-if="result.target.permissionCode"
          ><dt>权限要求</dt>
          <dd>{{ result.target.permissionCode }}</dd></template
        >
        <dt>评估口径</dt>
        <dd>
          {{
            result.evaluationBasis === 'CurrentServerIdentity' ? '当前服务端身份' : '当前配置推算'
          }}
        </dd>
        <dt>评估时间</dt>
        <dd>{{ new Date(result.evaluatedAt).toLocaleString() }}（历史快照）</dd>
      </dl>
      <ul class="diagnostic-checks">
        <li v-for="(check, index) in result.checks" :key="`${check.code}-${index}`">
          <span class="diagnostic-status">{{ statusLabel(check.status) }}</span>
          <span
            >{{ check.description }}<small>来源：{{ check.source }}</small></span
          >
        </li>
      </ul>
      <p v-if="result.isTruncated" class="diagnostic-limit">证据已截断，未展示全部配置来源。</p>
      <ul class="diagnostic-limit">
        <li v-for="item in result.limitations" :key="item">{{ item }}</li>
      </ul>
      <div v-if="entries.length" class="diagnostic-entries">
        <el-button
          v-for="entry in entries"
          :key="entry.code"
          size="small"
          @click="openEntry(entry.code)"
        >
          {{ entry.label }}
        </el-button>
      </div>
    </template>
    <p v-else>该诊断结果版本暂不支持展示，请重新诊断。</p>
  </article>
</template>

<style scoped>
.permission-diagnostic-card {
  margin: 12px 0;
  padding: 14px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-bg-color);
  overflow-wrap: anywhere;
}
.diagnostic-header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
}
.diagnostic-summary {
  line-height: 1.6;
}
.diagnostic-target {
  display: grid;
  grid-template-columns: 76px minmax(0, 1fr);
  gap: 6px;
  font-size: 12px;
}
.diagnostic-target dt,
.diagnostic-limit,
.diagnostic-checks small {
  color: var(--el-text-color-secondary);
}
.diagnostic-target dd {
  margin: 0;
}
.diagnostic-checks {
  padding: 0;
  list-style: none;
}
.diagnostic-checks li {
  display: flex;
  gap: 10px;
  margin: 10px 0;
  font-size: 13px;
  line-height: 1.6;
}
.diagnostic-status {
  flex: 0 0 42px;
}
.diagnostic-checks small {
  display: block;
}
.diagnostic-limit {
  font-size: 12px;
  line-height: 1.6;
}
.diagnostic-entries {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.diagnostic-entries .el-button + .el-button {
  margin-left: 0;
}
</style>
