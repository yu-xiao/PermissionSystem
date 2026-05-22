<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { HomeFilled } from '@element-plus/icons-vue'
import { usePermissionStore } from '../../stores/permission'
import SidebarItem from './SidebarItem.vue'
import SidebarLogo from './SidebarLogo.vue'

defineProps<{
  collapsed: boolean
}>()

const route = useRoute()
const permissionStore = usePermissionStore()

const activeMenu = computed(() => {
  const metaActiveMenu = route.meta.activeMenu
  return typeof metaActiveMenu === 'string' ? metaActiveMenu : route.path
})

const visibleMenus = computed(() => permissionStore.menus.filter((menu) => menu.visible !== false))
</script>

<template>
  <aside class="app-sidebar">
    <SidebarLogo :collapsed="collapsed" />

    <el-scrollbar class="app-sidebar__scrollbar">
      <el-menu
        class="app-sidebar__menu"
        router
        unique-opened
        :collapse="collapsed"
        :collapse-transition="false"
        :default-active="activeMenu"
      >
        <el-menu-item index="/dashboard">
          <el-icon><HomeFilled /></el-icon>
          <template #title>
            <span>首页</span>
          </template>
        </el-menu-item>

        <SidebarItem v-for="menu in visibleMenus" :key="menu.id" :item="menu" />
      </el-menu>
    </el-scrollbar>
  </aside>
</template>
