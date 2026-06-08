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
          path: 'workflow/definition/:id/designer',
          name: 'WorkflowDesigner',
          meta: {
            title: '流程设计器',
            hidden: true,
            alwaysShowTab: true,
            activeMenu: '/workflow/definition',
            permissionCode: 'workflow:definition:design',
            noCache: true,
          },
          component: () => import('../views/workflow/designer/index.vue'),
        },
        {
          path: 'workflow/instances/:id',
          name: 'WorkflowInstanceDetail',
          meta: {
            title: '审批详情',
            hidden: true,
            alwaysShowTab: true,
            activeMenu: '/workflow/task/todo',
            permissionCode: 'workflow:instance:view',
            noCache: true,
          },
          component: () => import('../views/workflow/instance/detail.vue'),
        },
        {
          path: 'demo/approval-order/:id',
          name: 'DemoApprovalOrderDetail',
          meta: {
            title: 'Demo 审批单详情',
            hidden: true,
            alwaysShowTab: true,
            activeMenu: '/demo/approval-order',
            permissionCode: 'demo-approval-order:view',
            noCache: true,
          },
          component: () => import('../views/demo/approval-order/detail.vue'),
        },
        {
          path: 'system/state-machines/:id/designer',
          name: 'StateMachineDesigner',
          meta: {
            title: '状态机设计',
            hidden: true,
            alwaysShowTab: true,
            activeMenu: '/system/state-machines',
            permissionCode: 'system:state-machine:update',
            noCache: true,
          },
          component: () => import('../views/system/state-machine/designer.vue'),
        },
        {
          path: 'system/print-templates/:id/designer',
          name: 'PrintTemplateDesigner',
          meta: {
            title: '打印模板设计',
            hidden: true,
            alwaysShowTab: true,
            activeMenu: '/system/print-templates',
            permissionCode: 'system:print-template:design',
            noCache: true,
          },
          component: () => import('../views/system/print-template/designer.vue'),
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
