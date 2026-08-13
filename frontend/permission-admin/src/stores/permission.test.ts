import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'
import { usePermissionStore } from './permission'

describe('permission store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('checks permission codes and resets authorization state', () => {
    const store = usePermissionStore()
    store.permissionCodes = ['system:user:view']

    expect(store.hasPermission('system:user:view')).toBe(true)
    expect(store.hasPermission('system:user:delete')).toBe(false)
    expect(store.hasPermission()).toBe(true)

    store.reset()
    expect(store.permissionCodes).toEqual([])
    expect(store.routesLoaded).toBe(false)
  })
})
