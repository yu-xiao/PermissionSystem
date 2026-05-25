import { createRouter, createWebHistory } from 'vue-router'
import AdminLayout from '../layouts/AdminLayout.vue'
import { getAccessToken } from '../utils/token'
import { useAuthStore } from '../stores/auth'
import { useTabsViewStore } from '../stores/tabsView'
import { doneProgress, resetProgress, startProgress } from '../utils/progress'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'Login',
      meta: {
        public: true,
        title: '登录',
      },
      component: () => import('../views/login/LoginView.vue'),
    },
    {
      path: '/',
      name: 'AdminRoot',
      component: AdminLayout,
      children: [
        {
          path: '',
          name: 'AdminIndex',
          meta: {
            hidden: true,
          },
          redirect: '/dashboard',
        },
        {
          path: 'dashboard',
          name: 'Dashboard',
          meta: {
            title: '首页',
            icon: 'HomeFilled',
            affix: true,
            noCache: false,
            cacheName: 'Dashboard',
          },
          component: () => import('../views/dashboard/IndexView.vue'),
        },
        {
          path: 'account/profile',
          name: 'AccountProfile',
          meta: {
            title: '个人中心',
            hidden: true,
            alwaysShowTab: true,
            noCache: false,
            cacheName: 'AccountProfile',
          },
          component: () => import('../views/account/profile/index.vue'),
        },
        {
          path: '403',
          name: 'Error403',
          meta: {
            title: '无权访问',
            hidden: true,
            noCache: true,
          },
          component: () => import('../views/error/403.vue'),
        },
        {
          path: '500',
          name: 'Error500',
          meta: {
            title: '系统异常',
            hidden: true,
            noCache: true,
          },
          component: () => import('../views/error/500.vue'),
        },
        {
          path: ':pathMatch(.*)*',
          name: 'Error404',
          meta: {
            title: '页面不存在',
            hidden: true,
            noCache: true,
          },
          component: () => import('../views/error/404.vue'),
        },
      ],
    },
  ],
})

router.beforeEach(async (to) => {
  resetProgress()
  startProgress()
  const isPublic = to.meta.public === true
  const accessToken = getAccessToken()

  if (!accessToken && !isPublic) {
    return {
      path: '/login',
      query: {
        redirect: to.fullPath,
      },
    }
  }

  if (accessToken && to.path === '/login') {
    return '/dashboard'
  }

  if (accessToken && !isPublic) {
    const authStore = useAuthStore()
    if (!authStore.isLoaded) {
      await authStore.loadCurrentUser()
      return to.fullPath
    }

    if (to.meta.permissionCode && !authStore.hasPermission(to.meta.permissionCode)) {
      return '/403'
    }
  }

  return true
})

router.afterEach((to) => {
  const tabsViewStore = useTabsViewStore()
  tabsViewStore.addView(to)
  doneProgress()
})

router.onError(() => {
  resetProgress()
})
