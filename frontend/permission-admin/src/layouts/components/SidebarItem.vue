<script setup lang="ts">
import * as ElementPlusIcons from '@element-plus/icons-vue'
import type { Component } from 'vue'
import type { MenuTreeResponse } from '../../api/me'

const props = defineProps<{
  item: MenuTreeResponse
}>()

defineOptions({
  name: 'SidebarItem',
})

const defaultIcon = ElementPlusIcons.Menu
const iconMap = ElementPlusIcons as unknown as Record<string, Component>

function hasVisibleChildren(item: MenuTreeResponse) {
  return (item.children ?? []).some((child) => child.visible !== false)
}

function normalizeMenuPath(path?: string) {
  if (!path) {
    return ''
  }

  return path.startsWith('/') ? path : `/${path.replace(/^\/+/, '')}`
}

function resolveMenuIndex(item: MenuTreeResponse) {
  return normalizeMenuPath(item.path) || item.id
}

function resolveIcon(icon?: string) {
  if (!icon) {
    return defaultIcon
  }

  const normalized = icon
    .split(/[-_\s:]+/)
    .filter(Boolean)
    .map((part) => `${part.charAt(0).toUpperCase()}${part.slice(1)}`)
    .join('')

  return iconMap[icon] ?? iconMap[normalized] ?? defaultIcon
}
</script>

<template>
  <el-sub-menu v-if="hasVisibleChildren(props.item)" :index="resolveMenuIndex(props.item)">
    <template #title>
      <el-icon><component :is="resolveIcon(props.item.icon)" /></el-icon>
      <span>{{ props.item.name }}</span>
    </template>

    <SidebarItem
      v-for="child in props.item.children.filter((menu) => menu.visible !== false)"
      :key="child.id"
      :item="child"
    />
  </el-sub-menu>

  <el-menu-item v-else-if="props.item.path" :index="resolveMenuIndex(props.item)">
    <el-icon><component :is="resolveIcon(props.item.icon)" /></el-icon>
    <template #title>
      <span>{{ props.item.name }}</span>
    </template>
  </el-menu-item>
</template>
