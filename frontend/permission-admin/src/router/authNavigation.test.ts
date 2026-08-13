import { describe, expect, it } from 'vitest'
import { resolveAuthNavigation } from './authNavigation'

describe('resolveAuthNavigation', () => {
  it('redirects anonymous users to login with the original location', () => {
    expect(
      resolveAuthNavigation({
        accessToken: false,
        isPublic: false,
        path: '/dashboard',
        fullPath: '/dashboard?tab=active',
      }),
    ).toEqual({ path: '/login', query: { redirect: '/dashboard?tab=active' } })
  })

  it('redirects authenticated users away from login', () => {
    expect(
      resolveAuthNavigation({
        accessToken: true,
        isPublic: true,
        path: '/login',
        fullPath: '/login',
      }),
    ).toBe('/dashboard')
  })

  it('returns forbidden when the permission check fails', () => {
    expect(
      resolveAuthNavigation({
        accessToken: true,
        isPublic: false,
        path: '/system/users',
        fullPath: '/system/users',
        permissionCode: 'system:user:view',
        hasPermission: () => false,
      }),
    ).toBe('/403')
  })
})
