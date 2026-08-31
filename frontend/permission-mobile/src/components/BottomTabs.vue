<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { usePermissionStore } from '../stores/permission'

const props = withDefaults(defineProps<{ unread?: number; todoCount?: number }>(), { unread: 0, todoCount: 0 })
const route = useRoute()
const router = useRouter()
const permission = usePermissionStore()

const tabs = computed(() => [
  { path: '/home', label: '工作台', icon: '⌂', active: route.path === '/home', visible: true },
  { path: '/tasks/todo', label: '待办', icon: '✓', active: route.path.startsWith('/tasks'), visible: permission.hasPermission('workflow:task:todo') },
  { path: '/notifications', label: '通知', icon: '♢', active: route.path.startsWith('/notifications'), visible: permission.hasPermission('system:notification:view') },
  { path: '/profile', label: '我的', icon: '○', active: route.path.startsWith('/profile') || route.path.startsWith('/sessions'), visible: true },
].filter((tab) => tab.visible))

function countFor(path: string) {
  if (path === '/notifications') return props.unread
  if (path === '/tasks/todo') return props.todoCount
  return 0
}

function navigate(path: string) {
  if (route.path !== path) void router.push(path)
}
</script>

<template>
  <nav class="bottom-tabs" aria-label="主导航">
    <div class="bottom-tabs__inner">
      <button
        v-for="tab in tabs"
        :key="tab.path"
        class="bottom-tab"
        :class="{ 'bottom-tab--active': tab.active }"
        type="button"
        :aria-current="tab.active ? 'page' : undefined"
        @click="navigate(tab.path)"
      >
        <span class="bottom-tab__icon" aria-hidden="true">
          {{ tab.icon }}
          <span v-if="countFor(tab.path) > 0" class="badge">{{ countFor(tab.path) > 99 ? '99+' : countFor(tab.path) }}</span>
        </span>
        <span>{{ tab.label }}</span>
      </button>
    </div>
  </nav>
</template>
