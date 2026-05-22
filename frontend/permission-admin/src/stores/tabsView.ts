import { defineStore } from 'pinia'
import type { RouteLocationNormalizedLoaded } from 'vue-router'

const tabsViewStorageKey = 'permission_system_tabs_view'

export interface VisitedView {
  name?: string
  path: string
  fullPath: string
  title: string
  affix: boolean
  noCache: boolean
  cacheName?: string
}

interface StoredTabsViewState {
  visitedViews: VisitedView[]
  cachedViews: string[]
}

const affixDashboardView: VisitedView = {
  name: 'Dashboard',
  path: '/dashboard',
  fullPath: '/dashboard',
  title: '首页',
  affix: true,
  noCache: false,
  cacheName: 'Dashboard',
}

function createDefaultState(): StoredTabsViewState {
  return {
    visitedViews: [affixDashboardView],
    cachedViews: ['Dashboard'],
  }
}

function readStoredState(): StoredTabsViewState {
  const rawValue = sessionStorage.getItem(tabsViewStorageKey)
  if (!rawValue) {
    return createDefaultState()
  }

  try {
    const parsed = JSON.parse(rawValue) as Partial<StoredTabsViewState>
    const visitedViews = Array.isArray(parsed.visitedViews) ? parsed.visitedViews : []
    const cachedViews = Array.isArray(parsed.cachedViews) ? parsed.cachedViews : []
    const normalizedViews = [
      affixDashboardView,
      ...visitedViews.filter((item) => item.path && item.path !== affixDashboardView.path),
    ]

    return {
      visitedViews: normalizedViews,
      cachedViews: Array.from(new Set(['Dashboard', ...cachedViews])),
    }
  } catch {
    return createDefaultState()
  }
}

function getRouteTitle(route: RouteLocationNormalizedLoaded) {
  return String(route.meta.title ?? route.name ?? route.path)
}

function getCacheName(route: RouteLocationNormalizedLoaded) {
  return typeof route.meta.cacheName === 'string'
    ? route.meta.cacheName
    : typeof route.name === 'string'
      ? route.name
      : undefined
}

function createVisitedView(route: RouteLocationNormalizedLoaded): VisitedView {
  return {
    name: typeof route.name === 'string' ? route.name : undefined,
    path: route.path,
    fullPath: route.fullPath,
    title: getRouteTitle(route),
    affix: route.meta.affix === true,
    noCache: route.meta.noCache === true,
    cacheName: getCacheName(route),
  }
}

export const useTabsViewStore = defineStore('tabsView', {
  state: () => ({
    ...readStoredState(),
    reloadKeys: {} as Record<string, number>,
  }),
  actions: {
    addView(route: RouteLocationNormalizedLoaded) {
      this.addVisitedView(route)
      this.addCachedView(route)
    },
    addVisitedView(route: RouteLocationNormalizedLoaded) {
      if (route.meta.public === true || route.meta.hidden === true) {
        return
      }

      const view = createVisitedView(route)
      const existing = this.visitedViews.find((item) => item.path === view.path)

      if (existing) {
        Object.assign(existing, view)
        this.persist()
        return
      }

      this.visitedViews.push(view)
      this.persist()
    },
    addCachedView(route: RouteLocationNormalizedLoaded) {
      if (route.meta.public === true || route.meta.hidden === true || route.meta.noCache === true) {
        return
      }

      const cacheName = getCacheName(route)
      if (cacheName && !this.cachedViews.includes(cacheName)) {
        this.cachedViews.push(cacheName)
        this.persist()
      }
    },
    delView(view: VisitedView) {
      this.delVisitedView(view)
      this.delCachedView(view)
    },
    delVisitedView(view: VisitedView) {
      this.visitedViews = this.visitedViews.filter((item) => item.affix || item.path !== view.path)
      this.persist()
    },
    delCachedView(view: VisitedView, force = false) {
      if (!view.cacheName || (view.affix && !force)) {
        return
      }

      this.cachedViews = this.cachedViews.filter((item) => item !== view.cacheName)
      this.persist()
    },
    addCachedViewName(cacheName: string) {
      if (!this.cachedViews.includes(cacheName)) {
        this.cachedViews.push(cacheName)
        this.persist()
      }
    },
    delOthersViews(view: VisitedView) {
      this.visitedViews = this.visitedViews.filter((item) => item.affix || item.path === view.path)
      this.cachedViews = this.visitedViews
        .filter((item) => !item.noCache && item.cacheName)
        .map((item) => item.cacheName as string)
      this.persist()
    },
    delLeftViews(view: VisitedView) {
      const index = this.visitedViews.findIndex((item) => item.path === view.path)
      if (index <= 0) {
        return
      }

      this.visitedViews = this.visitedViews.filter((item, itemIndex) => item.affix || itemIndex >= index)
      this.syncCachedViews()
    },
    delRightViews(view: VisitedView) {
      const index = this.visitedViews.findIndex((item) => item.path === view.path)
      if (index < 0) {
        return
      }

      this.visitedViews = this.visitedViews.filter((item, itemIndex) => item.affix || itemIndex <= index)
      this.syncCachedViews()
    },
    delAllViews() {
      this.visitedViews = this.visitedViews.filter((item) => item.affix)
      this.syncCachedViews()
    },
    updateVisitedView(route: RouteLocationNormalizedLoaded) {
      const view = createVisitedView(route)
      const existing = this.visitedViews.find((item) => item.path === view.path)
      if (existing) {
        Object.assign(existing, view)
        this.persist()
      }
    },
    reset() {
      this.visitedViews = [affixDashboardView]
      this.cachedViews = ['Dashboard']
      this.reloadKeys = {}
      sessionStorage.removeItem(tabsViewStorageKey)
    },
    syncCachedViews() {
      this.cachedViews = this.visitedViews
        .filter((item) => !item.noCache && item.cacheName)
        .map((item) => item.cacheName as string)
      this.persist()
    },
    refreshView(view: VisitedView) {
      this.reloadKeys = {
        ...this.reloadKeys,
        [view.path]: (this.reloadKeys[view.path] ?? 0) + 1,
      }
    },
    persist() {
      sessionStorage.setItem(
        tabsViewStorageKey,
        JSON.stringify({
          visitedViews: this.visitedViews,
          cachedViews: this.cachedViews,
        }),
      )
    },
  },
})
