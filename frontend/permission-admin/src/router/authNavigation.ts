export interface AuthNavigationInput {
  accessToken: boolean
  isPublic: boolean
  path: string
  fullPath: string
  permissionCode?: string
  hasPermission?: (permissionCode: string) => boolean
}

export type AuthNavigation = true | string | { path: string; query: { redirect: string } }

export function resolveAuthNavigation(input: AuthNavigationInput): AuthNavigation {
  if (!input.accessToken && !input.isPublic) {
    return {
      path: '/login',
      query: { redirect: input.fullPath },
    }
  }

  if (input.accessToken && input.path === '/login') {
    return '/dashboard'
  }

  if (
    input.accessToken &&
    input.permissionCode &&
    input.hasPermission &&
    !input.hasPermission(input.permissionCode)
  ) {
    return '/403'
  }

  return true
}
