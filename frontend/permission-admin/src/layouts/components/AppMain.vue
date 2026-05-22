<script setup lang="ts">
import type { RouteLocationNormalizedLoaded } from 'vue-router'
import { useTabsViewStore } from '../../stores/tabsView'

const tabsViewStore = useTabsViewStore()

function getViewKey(route: RouteLocationNormalizedLoaded) {
  return `${route.fullPath}:${tabsViewStore.reloadKeys[route.path] ?? 0}`
}
</script>

<template>
  <main class="app-main">
    <div class="app-main__inner">
      <router-view v-slot="{ Component, route }">
        <keep-alive :include="tabsViewStore.cachedViews">
          <component
            :is="Component"
            v-if="!route.meta.noCache"
            :key="getViewKey(route)"
          />
        </keep-alive>
        <component
          :is="Component"
          v-if="route.meta.noCache"
          :key="getViewKey(route)"
        />
      </router-view>
    </div>
  </main>
</template>
