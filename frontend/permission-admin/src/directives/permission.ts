import type { App, DirectiveBinding } from 'vue'
import { useAuthStore } from '../stores/auth'
import { usePermissionStore } from '../stores/permission'

export function setupPermissionDirective(app: App) {
  app.directive('permission', {
    mounted(element: HTMLElement, binding: DirectiveBinding<string>) {
      const authStore = useAuthStore()
      const permissionStore = usePermissionStore()

      if (!authStore.isSuperAdmin && !permissionStore.hasPermission(binding.value)) {
        element.remove()
      }
    },
  })
}
