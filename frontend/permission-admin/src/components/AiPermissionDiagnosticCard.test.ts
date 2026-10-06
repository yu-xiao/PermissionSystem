import ElementPlus from 'element-plus'
import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { defineComponent, reactive } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import AiPermissionDiagnosticCard from './AiPermissionDiagnosticCard.vue'
import type { AiPermissionDiagnosticResult } from '../api/ai'

const state = reactive({ allowed: true })
vi.mock('../stores/auth', () => ({ useAuthStore: () => ({ hasPermission: () => state.allowed }) }))

function diagnostic(): AiPermissionDiagnosticResult {
  return {
    runId: 'run-1',
    invocationId: 'call-1',
    data: {
      version: 1,
      target: { kind: 'Permission', userId: 'user-1', permissionCode: 'system:user:delete' },
      evaluationBasis: 'CurrentServerIdentity',
      evaluatedAt: '2026-10-06T08:00:00Z',
      conclusion: 'Denied',
      summary: '指定 API 权限要求不满足。',
      checks: [
        {
          code: 'api.permission',
          status: 'Failed',
          description: '没有权限',
          source: 'PermissionAuthorizationHandler',
        },
        {
          code: 'business.resource',
          status: 'NotEvaluated',
          description: '没有资源上下文',
          source: 'DataPermissionFilter',
        },
      ],
      limitations: ['未检查实际业务操作。'],
      isTruncated: true,
      suggestedEntries: [
        { code: 'users', label: '用户配置' },
        { code: 'https://untrusted.test', label: '外部入口' },
      ],
    },
  }
}

function mountCard(result = diagnostic(), routes = true) {
  const page = defineComponent({ template: '<div />' })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: routes ? [{ path: '/system/users', component: page }] : [],
  })
  const wrapper = mount(AiPermissionDiagnosticCard, {
    props: { diagnostic: result },
    global: { plugins: [ElementPlus, router] },
  })
  return { wrapper, router }
}

describe('AiPermissionDiagnosticCard', () => {
  beforeEach(() => {
    state.allowed = true
  })

  it('展示服务端拒绝、未评估条件、来源、历史时间和截断信息', () => {
    const { wrapper } = mountCard()
    expect(wrapper.text()).toContain('拒绝（已知检查范围）')
    expect(wrapper.text()).toContain('未评估')
    expect(wrapper.text()).toContain('PermissionAuthorizationHandler')
    expect(wrapper.text()).toContain('历史快照')
    expect(wrapper.text()).toContain('证据已截断')
    expect(wrapper.text()).not.toContain('外部入口')
  })

  it('仅导航到白名单中的已授权且已注册路由', async () => {
    const { wrapper, router } = mountCard()
    await wrapper.find('button').trigger('click')
    await router.isReady()
    expect(router.currentRoute.value.path).toBe('/system/users')
    expect(wrapper.findAll('button')).toHaveLength(1)
  })

  it('撤销权限后移除入口', async () => {
    const { wrapper } = mountCard()
    expect(wrapper.findAll('button')).toHaveLength(1)
    state.allowed = false
    await wrapper.vm.$nextTick()
    expect(wrapper.findAll('button')).toHaveLength(0)
  })

  it('没有注册路由时不展示入口', () => {
    expect(mountCard(diagnostic(), false).wrapper.findAll('button')).toHaveLength(0)
  })

  it('未知版本不解释为当前契约', () => {
    const result = diagnostic()
    result.data.version = 2
    const { wrapper } = mountCard(result)
    expect(wrapper.text()).toContain('暂不支持展示')
    expect(wrapper.text()).not.toContain('拒绝（已知检查范围）')
  })

  it('证据中的 HTML 仅作为文本显示', () => {
    const result = diagnostic()
    result.data.checks[0].description = '<img src=x onerror=alert(1)>'
    const { wrapper } = mountCard(result)
    expect(wrapper.findAll('img')).toHaveLength(0)
    expect(wrapper.text()).toContain('<img src=x onerror=alert(1)>')
  })
})
