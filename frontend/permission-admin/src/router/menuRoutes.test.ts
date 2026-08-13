import { describe, expect, it } from 'vitest'
import { buildMenuRoutes, resolveMenuCacheName } from './menuRoutes'

describe('menu route registry', () => {
  it('resolves exact component identifiers without substring collisions', () => {
    expect(resolveMenuCacheName({ component: 'system/user/index', path: '/system/users' })).toBe(
      'SystemUser',
    )
    expect(
      resolveMenuCacheName({
        component: 'system/operation-log/index',
        path: '/system/operation-logs',
      }),
    ).toBe('SystemOperationLog')
    expect(resolveMenuCacheName({ component: 'unknown/component', path: '/custom' })).toBe(
      'RoutePlaceholder',
    )
  })

  it('builds stable named routes recursively', () => {
    const routes = buildMenuRoutes([
      {
        id: 'parent',
        name: 'Users',
        path: '/system/users',
        component: 'system/user/index',
        sort: 1,
        visible: true,
        children: [
          {
            id: 'child',
            name: 'Custom',
            path: '/custom',
            component: 'unknown/component',
            sort: 2,
            visible: true,
            children: [],
          },
        ],
      },
    ])

    expect(routes.map((route) => route.name)).toEqual(['Menu_parent', 'Menu_child'])
    expect(routes[0]?.path).toBe('system/users')
  })
})
