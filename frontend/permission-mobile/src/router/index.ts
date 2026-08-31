import { createRouter, createWebHistory } from 'vue-router'
import MainLayout from '../layouts/MainLayout.vue'
import { getAccessToken, getRefreshToken, isAccessTokenExpired } from '../utils/token'
import { useAuthStore } from '../stores/auth'
import { usePermissionStore } from '../stores/permission'
import { refreshAccessTokenOnce } from '../utils/request'

export const router = createRouter({
  history: createWebHistory(),
  scrollBehavior: () => ({ top: 0 }),
  routes: [
    {
      path: '/login',
      name: 'MobileLogin',
      meta: { public: true, title: '登录' },
      component: () => import('../views/login/LoginView.vue'),
    },
    {
      path: '/authorize/callback',
      name: 'MobileAuthorizeCallback',
      meta: { public: true, title: '登录授权' },
      component: () => import('../views/login/AuthorizeCallbackView.vue'),
    },
    {
      path: '/',
      component: MainLayout,
      children: [
        { path: '', redirect: '/home' },
        {
          path: 'home',
          name: 'MobileHome',
          meta: { title: '工作台' },
          component: () => import('../views/home/HomeView.vue'),
        },
        {
          path: 'tasks/todo',
          name: 'MobileTodoTasks',
          meta: { title: '我的待办', permission: 'workflow:task:todo' },
          component: () => import('../views/tasks/TaskListView.vue'),
        },
        {
          path: 'tasks/done',
          name: 'MobileDoneTasks',
          meta: { title: '已办记录', permission: 'workflow:task:todo' },
          component: () => import('../views/tasks/TaskListView.vue'),
          props: { done: true },
        },
        {
          path: 'tasks/:id',
          name: 'MobileTaskDetail',
          meta: { title: '审批详情', showBack: true, hideBottomTabs: true, permission: 'workflow:task:todo' },
          component: () => import('../views/tasks/TaskDetailView.vue'),
        },
        {
          path: 'notifications',
          name: 'MobileNotifications',
          meta: { title: '通知中心', permission: 'system:notification:view' },
          component: () => import('../views/notifications/NotificationListView.vue'),
        },
        {
          path: 'orders',
          name: 'MobileOrders',
          meta: { title: '业务单据', permissionAny: ['demo-business-order:view', 'demo-approval-order:view'] },
          component: () => import('../views/orders/OrderListView.vue'),
        },
        {
          path: 'orders/new',
          name: 'MobileOrderNew',
          meta: { title: '新建单据', showBack: true, hideBottomTabs: true, permissionAny: ['demo-business-order:create', 'demo-approval-order:create'] },
          component: () => import('../views/orders/OrderEditView.vue'),
        },
        {
          path: 'orders/:id/edit',
          name: 'MobileOrderEdit',
          meta: { title: '编辑单据', showBack: true, hideBottomTabs: true, permissionAny: ['demo-business-order:update', 'demo-approval-order:update'] },
          component: () => import('../views/orders/OrderEditView.vue'),
        },
        {
          path: 'orders/:id',
          name: 'MobileOrderDetail',
          meta: { title: '单据详情', showBack: true, hideBottomTabs: true, permissionAny: ['demo-business-order:view', 'demo-approval-order:view'] },
          component: () => import('../views/orders/OrderDetailView.vue'),
        },
        {
          path: 'profile',
          name: 'MobileProfile',
          meta: { title: '我的' },
          component: () => import('../views/profile/ProfileView.vue'),
        },
        {
          path: 'sessions',
          name: 'MobileSessions',
          meta: { title: '当前会话', showBack: true, hideBottomTabs: true },
          component: () => import('../views/profile/SessionsView.vue'),
        },
        {
          path: '403',
          name: 'MobileForbidden',
          meta: { title: '无权访问', showBack: true, hideBottomTabs: true },
          component: () => import('../views/error/ForbiddenView.vue'),
        },
        {
          path: ':pathMatch(.*)*',
          name: 'MobileNotFound',
          meta: { title: '页面不存在', showBack: true, hideBottomTabs: true },
          component: () => import('../views/error/NotFoundView.vue'),
        },
      ],
    },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  const permission = usePermissionStore()
  const accessToken = getAccessToken()
  if (to.meta.public === true) {
    if (accessToken && to.path === '/login') return '/home'
    return true
  }
  if (!accessToken || isAccessTokenExpired()) {
    if (!getRefreshToken()) {
      return { path: '/login', query: { redirect: to.fullPath } }
    }
    const refreshed = await refreshAccessTokenOnce()
    if (!refreshed) return { path: '/login', query: { redirect: to.fullPath } }
  }
  if (!auth.isLoaded) {
    try {
      await auth.initialize()
    } catch {
      return { path: '/login', query: { redirect: to.fullPath } }
    }
  }
  const required = to.meta.permission as string | undefined
  const anyRequired = to.meta.permissionAny as string[] | undefined
  if (required && !permission.hasPermission(required)) return { path: '/403' }
  if (anyRequired?.length && !permission.canAny(anyRequired)) return { path: '/403' }
  return true
})

export default router
