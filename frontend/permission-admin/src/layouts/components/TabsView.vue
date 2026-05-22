<script setup lang="ts">
import { Close, MoreFilled, RefreshRight } from '@element-plus/icons-vue'
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useTabsViewStore, type VisitedView } from '../../stores/tabsView'

const route = useRoute()
const router = useRouter()
const tabsViewStore = useTabsViewStore()

const contextMenuVisible = ref(false)
const contextMenuLeft = ref(0)
const contextMenuTop = ref(0)
const contextMenuRef = ref<HTMLElement>()
const selectedView = ref<VisitedView>()

const activePath = computed(() => route.path)
const currentView = computed(() => tabsViewStore.visitedViews.find((item) => item.path === route.path))
const menuTargetView = computed(() => selectedView.value ?? currentView.value)
const closableViews = computed(() => tabsViewStore.visitedViews.filter((item) => !item.affix))
const canCloseCurrent = computed(() => Boolean(menuTargetView.value && !menuTargetView.value.affix))
const canCloseOthers = computed(() =>
  Boolean(menuTargetView.value && hasClosableOtherViews(menuTargetView.value)),
)
const canCloseLeft = computed(() => {
  return Boolean(menuTargetView.value && hasClosableLeftViews(menuTargetView.value))
})
const canCloseRight = computed(() => {
  return Boolean(menuTargetView.value && hasClosableRightViews(menuTargetView.value))
})
const canCloseAll = computed(() => closableViews.value.length > 0)

function isActive(view: VisitedView) {
  return view.path === activePath.value
}

function goView(view: VisitedView) {
  closeContextMenu()
  if (!isActive(view)) {
    router.push(view.fullPath)
  }
}

function findNextView(view: VisitedView) {
  const views = tabsViewStore.visitedViews
  const index = views.findIndex((item) => item.path === view.path)
  return views[index + 1] ?? views[index - 1] ?? views.find((item) => item.affix) ?? views[0]
}

function hasClosableOtherViews(view: VisitedView) {
  return tabsViewStore.visitedViews.some((item) => !item.affix && item.path !== view.path)
}

function hasClosableLeftViews(view: VisitedView) {
  const index = tabsViewStore.visitedViews.findIndex((item) => item.path === view.path)
  if (index <= 0) {
    return false
  }

  return tabsViewStore.visitedViews.slice(0, index).some((item) => !item.affix)
}

function hasClosableRightViews(view: VisitedView) {
  const index = tabsViewStore.visitedViews.findIndex((item) => item.path === view.path)
  if (index < 0) {
    return false
  }

  return tabsViewStore.visitedViews.slice(index + 1).some((item) => !item.affix)
}

function ensureRouteStillVisited(fallbackView?: VisitedView) {
  const routeStillVisited = tabsViewStore.visitedViews.some((item) => item.path === route.path)
  const targetView = fallbackView ?? tabsViewStore.visitedViews.find((item) => item.affix) ?? tabsViewStore.visitedViews[0]

  if (!routeStillVisited && targetView) {
    router.push(targetView.fullPath)
  }
}

function closeView(view: VisitedView) {
  if (view.affix) {
    return
  }

  closeContextMenu()
  const nextView = isActive(view) ? findNextView(view) : undefined
  tabsViewStore.delView(view)

  if (nextView && isActive(view)) {
    router.push(nextView.fullPath)
  }
}

async function refreshView(view?: VisitedView) {
  const targetView = view ?? menuTargetView.value
  if (!targetView) {
    return
  }

  closeContextMenu()

  if (!isActive(targetView)) {
    await router.push(targetView.fullPath)
  }

  if (targetView.cacheName && !targetView.noCache) {
    tabsViewStore.delCachedView(targetView, true)
    await nextTick()
    tabsViewStore.addCachedViewName(targetView.cacheName)
  }

  tabsViewStore.refreshView(targetView)
}

function closeCurrentView(view?: VisitedView) {
  const targetView = view ?? menuTargetView.value
  if (targetView) {
    closeView(targetView)
  }
}

function closeOthersViews(view?: VisitedView) {
  const targetView = view ?? menuTargetView.value
  if (!targetView || !hasClosableOtherViews(targetView)) {
    return
  }

  closeContextMenu()
  tabsViewStore.delOthersViews(targetView)
  ensureRouteStillVisited(targetView)
}

function closeLeftViews(view?: VisitedView) {
  const targetView = view ?? menuTargetView.value
  if (!targetView || !hasClosableLeftViews(targetView)) {
    return
  }

  closeContextMenu()
  tabsViewStore.delLeftViews(targetView)
  ensureRouteStillVisited(targetView)
}

function closeRightViews(view?: VisitedView) {
  const targetView = view ?? menuTargetView.value
  if (!targetView || !hasClosableRightViews(targetView)) {
    return
  }

  closeContextMenu()
  tabsViewStore.delRightViews(targetView)
  ensureRouteStillVisited(targetView)
}

function closeAllViews() {
  if (!canCloseAll.value) {
    return
  }

  closeContextMenu()
  tabsViewStore.delAllViews()
  const fallbackView = tabsViewStore.visitedViews.find((item) => item.affix) ?? tabsViewStore.visitedViews[0]

  if (fallbackView && route.path !== fallbackView.path) {
    router.push(fallbackView.fullPath)
  }
}

async function openContextMenu(event: MouseEvent, view: VisitedView) {
  selectedView.value = view
  contextMenuVisible.value = true
  contextMenuLeft.value = event.clientX
  contextMenuTop.value = event.clientY

  await nextTick()
  adjustContextMenuPosition()
}

function adjustContextMenuPosition() {
  const menuElement = contextMenuRef.value
  if (!menuElement) {
    return
  }

  const rect = menuElement.getBoundingClientRect()
  const edgePadding = 8
  contextMenuLeft.value = Math.min(contextMenuLeft.value, window.innerWidth - rect.width - edgePadding)
  contextMenuTop.value = Math.min(contextMenuTop.value, window.innerHeight - rect.height - edgePadding)
  contextMenuLeft.value = Math.max(edgePadding, contextMenuLeft.value)
  contextMenuTop.value = Math.max(edgePadding, contextMenuTop.value)
}

function closeContextMenu() {
  contextMenuVisible.value = false
}

onMounted(() => {
  document.addEventListener('click', closeContextMenu)
  window.addEventListener('scroll', closeContextMenu, true)
  window.addEventListener('resize', closeContextMenu)
})

onBeforeUnmount(() => {
  document.removeEventListener('click', closeContextMenu)
  window.removeEventListener('scroll', closeContextMenu, true)
  window.removeEventListener('resize', closeContextMenu)
})

watch(
  () => route.fullPath,
  () => {
    closeContextMenu()
  },
)
</script>

<template>
  <div class="tabs-view">
    <el-scrollbar class="tabs-view__scrollbar">
      <div class="tabs-view__list">
        <button
          v-for="view in tabsViewStore.visitedViews"
          :key="view.path"
          class="tabs-view__item"
          :class="{ 'is-active': isActive(view), 'is-affix': view.affix }"
          type="button"
          @click="goView(view)"
          @contextmenu.prevent="openContextMenu($event, view)"
        >
          <span class="tabs-view__dot" />
          <span class="tabs-view__title">{{ view.title }}</span>
          <el-icon v-if="!view.affix" class="tabs-view__close" @click.stop="closeView(view)">
            <Close />
          </el-icon>
        </button>
      </div>
    </el-scrollbar>

    <div class="tabs-view__actions">
      <el-tooltip content="刷新当前页" placement="bottom">
        <el-button text :icon="RefreshRight" @click="refreshView(currentView)" />
      </el-tooltip>
      <el-dropdown trigger="click">
        <el-button text :icon="MoreFilled" />
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item @click="refreshView(currentView)">刷新当前页</el-dropdown-item>
            <el-dropdown-item :disabled="!currentView || currentView.affix" @click="closeCurrentView(currentView)">
              关闭当前页
            </el-dropdown-item>
            <el-dropdown-item :disabled="!currentView || !hasClosableLeftViews(currentView)" @click="closeLeftViews(currentView)">
              关闭左侧页签
            </el-dropdown-item>
            <el-dropdown-item :disabled="!currentView || !hasClosableRightViews(currentView)" @click="closeRightViews(currentView)">
              关闭右侧页签
            </el-dropdown-item>
            <el-dropdown-item :disabled="!currentView || !hasClosableOtherViews(currentView)" @click="closeOthersViews(currentView)">
              关闭其他页签
            </el-dropdown-item>
            <el-dropdown-item :disabled="!canCloseAll" @click="closeAllViews">关闭全部页签</el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>
    </div>

    <Teleport to="body">
      <div
        v-if="contextMenuVisible"
        ref="contextMenuRef"
        class="tabs-context-menu"
        :style="{ left: `${contextMenuLeft}px`, top: `${contextMenuTop}px` }"
        @click.stop
      >
        <button class="tabs-context-menu__item" type="button" @click="refreshView(menuTargetView)">
          刷新当前页
        </button>
        <button
          class="tabs-context-menu__item"
          type="button"
          :disabled="!canCloseCurrent"
          @click="closeCurrentView(menuTargetView)"
        >
          关闭当前页
        </button>
        <button
          class="tabs-context-menu__item"
          type="button"
          :disabled="!canCloseOthers"
          @click="closeOthersViews(menuTargetView)"
        >
          关闭其他页
        </button>
        <button
          class="tabs-context-menu__item"
          type="button"
          :disabled="!canCloseLeft"
          @click="closeLeftViews(menuTargetView)"
        >
          关闭左侧页
        </button>
        <button
          class="tabs-context-menu__item"
          type="button"
          :disabled="!canCloseRight"
          @click="closeRightViews(menuTargetView)"
        >
          关闭右侧页
        </button>
        <button
          class="tabs-context-menu__item"
          type="button"
          :disabled="!canCloseAll"
          @click="closeAllViews"
        >
          关闭全部页
        </button>
      </div>
    </Teleport>
  </div>
</template>
