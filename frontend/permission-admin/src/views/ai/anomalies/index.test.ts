import { flushPromises, mount } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { reactive } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import AnomalyPage from './index.vue'
import {
  createAnomalyRule,
  getAnomalyEvent,
  getAnomalyEvents,
  getAnomalyRules,
  triggerAnomalyCheck,
  type AiAnomalyRule,
} from '../../../api/ai-anomalies'

const state = vi.hoisted(() => ({ permissions: [] as string[], tenant: 'tenant' }))
const route = vi.hoisted(() => ({ query: {} as Record<string, string> }))
vi.mock('vue-router', () => ({ useRoute: () => route }))
let auth: {
  effectiveTenantId: string
  currentUser: { userId: string; permissionCodes: string[] }
  hasPermission: (code: string) => boolean
}
vi.mock('../../../stores/auth', () => ({ useAuthStore: () => auth }))
vi.mock('../../../api/ai-anomalies', () => ({
  getAnomalyRules: vi.fn(),
  createAnomalyRule: vi.fn(),
  setAnomalyEnabled: vi.fn(),
  triggerAnomalyCheck: vi.fn(),
  getAnomalyEvents: vi.fn(),
  getAnomalyEvent: vi.fn(),
  closeAnomalyEvent: vi.fn(),
}))
const result = <T>(items: T[]) => ({
  items,
  totalCount: items.length,
  pageIndex: 1,
  pageSize: 20,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
})
const rule: AiAnomalyRule = {
  id: 'rule',
  isEnabled: true,
  rowVersion: 'AAAAAAAAAAA=',
  basis: '当前 Pending 完整数量 ≥ 1',
  intervalMinutes: 5,
  cooldownHours: 24,
  timeZone: 'Asia/Shanghai',
}
describe('个人 Demo 提醒', () => {
  let wrapper: ReturnType<typeof mount>
  beforeEach(() => {
    vi.clearAllMocks()
    route.query = {}
    state.permissions = [
      'system:scheduled-task:view',
      'system:scheduled-task:create',
      'system:scheduled-task:update',
      'system:scheduled-task:trigger',
      'demo-business-order:view',
    ]
    auth = reactive({
      effectiveTenantId: state.tenant,
      currentUser: { userId: 'user', permissionCodes: state.permissions },
      hasPermission: (code: string) => auth.currentUser.permissionCodes.includes(code),
    })
    vi.mocked(getAnomalyRules).mockResolvedValue(result([]))
    vi.mocked(getAnomalyEvents).mockResolvedValue(result([]))
  })
  afterEach(() => wrapper?.unmount())
  async function page() {
    wrapper = mount(AnomalyPage, {
      global: {
        plugins: [ElementPlus],
        stubs: {
          ElDialog: { template: '<div v-if="modelValue"><slot /></div>', props: ['modelValue'] },
        },
      },
    })
    await flushPromises()
  }
  async function click(text: string) {
    await wrapper
      .findAll('button')
      .find((button) => button.text() === text)!
      .trigger('click')
    await flushPromises()
  }
  it('无业务查看权限不发送查询或显示创建入口', async () => {
    auth.currentUser.permissionCodes = [
      'system:scheduled-task:view',
      'system:scheduled-task:create',
    ]
    await page()
    expect(getAnomalyRules).not.toHaveBeenCalled()
    expect(wrapper.text()).not.toContain('创建个人提醒')
  })
  it('固定规则说明与默认暂停创建，不提交任意身份或参数', async () => {
    await page()
    expect(wrapper.text()).toContain('不是审批超时规则')
    expect(wrapper.text()).toContain('24 小时冷却')
    await click('创建个人提醒')
    expect(createAnomalyRule).toHaveBeenCalledWith()
  })
  it('立即检查只入队，显示刷新提示而不伪造执行结果', async () => {
    vi.mocked(getAnomalyRules).mockResolvedValue(result([rule]))
    await page()
    await click('立即检查')
    expect(triggerAnomalyCheck).toHaveBeenCalledWith('rule')
    expect(getAnomalyRules).toHaveBeenCalledTimes(2)
  })
  it('范围变化隐藏旧数量，Queued 不显示为已投递', async () => {
    vi.mocked(getAnomalyRules).mockResolvedValue(result([rule]))
    vi.mocked(getAnomalyEvents).mockResolvedValue(
      result([
        {
          id: 'event',
          ruleId: 'rule',
          episodeSequence: 1,
          observedAt: '2026-10-07T00:00:00Z',
          deliveryStatus: 'Queued',
          attemptCount: 1,
          evidenceUnavailable: true,
          rowVersion: '',
          basis: '固定口径',
        },
      ]),
    )
    await page()
    expect(wrapper.text()).toContain('范围已变化，证据隐藏')
    expect(wrapper.text()).toContain('已入队，待消费')
    expect(wrapper.text()).not.toContain('已保存站内通知')
  })
  it('撤权清除已展示事件并忽略晚到响应', async () => {
    let resolve: (value: ReturnType<typeof result<AiAnomalyRule>>) => void = () => {}
    vi.mocked(getAnomalyRules).mockReturnValue(
      new Promise((done) => {
        resolve = done
      }),
    )
    await page()
    auth.currentUser.permissionCodes = []
    await flushPromises()
    resolve(result([rule]))
    await flushPromises()
    expect(wrapper.text()).not.toContain('已启用')
    expect(getAnomalyEvents).not.toHaveBeenCalled()
  })
  it('租户变化清除旧详情，读取失败不保留旧业务数量', async () => {
    const id = '11111111-1111-1111-1111-111111111111'
    route.query = { eventId: id }
    vi.mocked(getAnomalyEvent).mockResolvedValue({
      id,
      ruleId: 'rule',
      episodeSequence: 1,
      observedCount: 777,
      observedAt: '2026-10-07T00:00:00Z',
      deliveryStatus: 'Delivered',
      attemptCount: 1,
      rowVersion: '',
      evidenceUnavailable: false,
      basis: '固定口径',
    })
    await page()
    expect(wrapper.text()).toContain('777 单')
    vi.mocked(getAnomalyRules).mockRejectedValue(new Error('denied'))
    auth.effectiveTenantId = 'different'
    await flushPromises()
    expect(wrapper.text()).not.toContain('777 单')
    expect(wrapper.text()).toContain('读取失败')
  })
})
