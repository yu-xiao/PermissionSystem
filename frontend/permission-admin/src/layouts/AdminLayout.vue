<script setup lang="ts">
import { onMounted } from 'vue'
import AppHeader from './components/AppHeader.vue'
import AppMain from './components/AppMain.vue'
import Sidebar from './components/Sidebar.vue'
import TabsView from './components/TabsView.vue'
import { useAppStore } from '../stores/app'

const appStore = useAppStore()

onMounted(() => {
  appStore.applyTheme()
})
</script>

<template>
  <div class="admin-layout" :class="{ 'is-sidebar-collapsed': appStore.sidebarCollapsed }">
    <Sidebar :collapsed="appStore.sidebarCollapsed" />

    <section class="admin-layout__body">
      <AppHeader :collapsed="appStore.sidebarCollapsed" @toggle-sidebar="appStore.toggleSidebar" />
      <TabsView />
      <AppMain />
    </section>
  </div>
</template>
