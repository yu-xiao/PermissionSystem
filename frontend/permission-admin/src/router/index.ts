import { createRouter, createWebHistory } from 'vue-router'
import AdminLayout from '../layouts/AdminLayout.vue'
import { getAccessToken } from '../utils/token'
import { useAuthStore } from '../stores/auth'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'Login',
      meta: {
        public: true,
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
          redirect: '/dashboard',
        },
        {
          path: 'dashboard',
          name: 'Dashboard',
          component: () => import('../views/dashboard/IndexView.vue'),
        },
      ],
    },
  ],
})

router.beforeEach(async (to) => {
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
  }

  return true
})
