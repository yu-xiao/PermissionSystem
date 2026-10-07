import { flushPromises, mount } from '@vue/test-utils'
import ElementPlus, { ElDatePicker, ElPagination } from 'element-plus'
import { defineComponent, KeepAlive, reactive, ref } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import OperationsPage from './index.vue'
import {
  getAiOperationsSummary,
  getAiScenarioOperations,
  exportAiTechnicalMetadata,
  getAiTechnicalExportReceipts,
  getAiCostQuality,
  type AiOperationsSummary,
  type AiScenarioOperations,
  type AiScenarioOperationsItem,
} from '../../../api/ai'

let auth: {
  effectiveTenantId: string
  currentUser: { userId: string; permissionCodes: string[] }
  hasPermission: (code: string) => boolean
}
vi.mock('../../../stores/auth', () => ({ useAuthStore: () => auth }))
vi.mock('../../../api/ai', () => ({
  getAiOperationsSummary: vi.fn(),
  getAiScenarioOperations: vi.fn(),
  exportAiTechnicalMetadata: vi.fn(),
  getAiTechnicalExportReceipts: vi.fn(),
  getAiTechnicalExportReceipt: vi.fn(),
  getAiCostQuality: vi.fn(),
}))

const summary: AiOperationsSummary = {
  from: '2026-10-01T00:00:00Z',
  to: '2026-10-02T00:00:00Z',
  runCount: 2,
  successfulRunCount: 1,
  failedRunCount: 0,
  fallbackRunCount: 0,
  inputTokens: 5,
  outputTokens: 8,
  unknownCostInvocationCount: 0,
  positiveFeedbackCount: 1,
  negativeFeedbackCount: 0,
  p95DurationMilliseconds: 10,
  costs: [],
  daily: [],
  providers: [],
}

function row(overrides: Partial<AiScenarioOperationsItem> = {}): AiScenarioOperationsItem {
  return {
    scenarioId: 'scene',
    scenarioName: '合成权限场景',
    scenarioCode: 'synthetic',
    scenarioAvailable: true,
    runCount: 4,
    pendingRunCount: 1,
    runningRunCount: 0,
    completedRunCount: 1,
    failedRunCount: 1,
    cancelledRunCount: 1,
    unknownStatusRunCount: 0,
    terminalRunCount: 3,
    timeoutFailureCount: 1,
    technicalCompletionRate: 33.33,
    feedbackEligibleRunCount: 1,
    positiveFeedbackCount: 0,
    negativeFeedbackCount: 1,
    feedbackCoverageRate: 100,
    positiveFeedbackRate: 0,
    durationSampleCount: 3,
    p95DurationMilliseconds: 200,
    inputTokens: 100,
    outputTokens: 20,
    unknownUsageInvocationCount: 1,
    unknownCostInvocationCount: 1,
    unsettledInvocationCount: 1,
    unknownStatusInvocationCount: 0,
    estimatedCosts: [
      { currency: 'USD', amount: 2 },
      { currency: 'CNY', amount: 0 },
    ],
    ...overrides,
  }
}

function statistics(items: AiScenarioOperationsItem[] = [row()]): AiScenarioOperations {
  return {
    metricsVersion: 1,
    from: summary.from,
    to: summary.to,
    observedFrom: summary.to,
    observedTo: summary.to,
    limits: { maxRuns: 10000, maxUsages: 50000, maxFeedback: 10000 },
    scenarios: {
      items,
      totalCount: items.length,
      pageIndex: 1,
      pageSize: 20,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    },
  }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason?: unknown) => void
  const promise = new Promise<T>((yes, no) => {
    resolve = yes
    reject = no
  })
  return { promise, resolve, reject }
}

describe('AI 场景运营统计', () => {
  it('只有 view 即可主动打开质量核验，不自动请求且无需 export', async () => {
    auth.currentUser.permissionCodes = ['ai:operations:view']
    const wrapper = mount(OperationsPage, { global: { plugins: [ElementPlus] } })
    await flushPromises()
    expect(getAiCostQuality).not.toHaveBeenCalled()
    const action = wrapper
      .findAll('button')
      .find((b) => b.attributes('aria-label') === '估算质量核验')!
    await action.trigger('click')
    await flushPromises()
    expect(getAiCostQuality).toHaveBeenCalledTimes(1)
    wrapper.unmount()
  })
  let wrapper: ReturnType<typeof mount>
  beforeEach(() => {
    vi.clearAllMocks()
    auth = reactive({
      effectiveTenantId: 'tenant-1',
      currentUser: { userId: 'actor', permissionCodes: ['ai:operations:view'] },
      hasPermission: (code: string) => auth.currentUser.permissionCodes.includes(code),
    })
    vi.mocked(getAiOperationsSummary).mockResolvedValue(summary)
    vi.mocked(getAiScenarioOperations).mockResolvedValue(statistics())
    vi.mocked(exportAiTechnicalMetadata).mockResolvedValue({
      content: new Blob(['{}']),
      fileName: 'ai-technical-0123456789abcdef0123456789abcdef.json',
    })
  })
  afterEach(() => wrapper?.unmount())
  async function page() {
    wrapper = mount(OperationsPage, { global: { plugins: [ElementPlus] } })
    await flushPromises()
    await wrapper.get('#tab-scenarios').trigger('click')
    await flushPromises()
  }
  async function refresh() {
    await wrapper.get('[aria-label="刷新运营统计"]').trigger('click')
    await flushPromises()
  }

  it('分别呈现技术完成率、评价覆盖、有效样本和分币种估算', async () => {
    await page()
    expect(wrapper.text()).toContain('合成权限场景')
    expect(wrapper.text()).toContain('33.33%')
    expect(wrapper.text()).toContain('100.00%')
    expect(wrapper.text()).toContain('200 ms')
    expect(wrapper.text()).toContain('3 个有效终态样本')
    expect(wrapper.text()).toContain('USD 2.000000 / CNY 0.000000')
    expect(wrapper.text()).toContain('未知 1 / 未决 1')
    expect(wrapper.text()).toContain('人工纠错率、业务完成率：未采集')
    expect(wrapper.text()).toContain('不是账单结算')
    expect(wrapper.get('[data-testid="observation"]').text()).toContain('10,000')
  })

  it('无分母和无耗时呈现缺失值，未绑定场景不补造归属', async () => {
    vi.mocked(getAiScenarioOperations).mockResolvedValue(
      statistics([
        row({
          scenarioId: null,
          scenarioName: '未绑定场景',
          scenarioCode: null,
          scenarioAvailable: false,
          technicalCompletionRate: null,
          feedbackCoverageRate: null,
          positiveFeedbackRate: null,
          p95DurationMilliseconds: null,
          durationSampleCount: 0,
          estimatedCosts: [],
        }),
      ]),
    )
    await page()
    expect(wrapper.text()).toContain('未绑定场景')
    expect(
      wrapper.findAll('.scenario-table .cell').filter((cell) => cell.text().includes('—')).length,
    ).toBeGreaterThanOrEqual(4)
    expect(wrapper.text()).toContain('暂无已知估算')
    expect(wrapper.text()).not.toContain('合成权限场景')
  })

  it('无记录提供空状态，无权限不发送运营请求', async () => {
    auth.currentUser.permissionCodes = []
    wrapper = mount(OperationsPage, { global: { plugins: [ElementPlus] } })
    await flushPromises()
    expect(getAiOperationsSummary).not.toHaveBeenCalled()
    expect(getAiScenarioOperations).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('当前无 AI 运营查看权限')
    vi.mocked(getAiScenarioOperations).mockResolvedValue(statistics([]))
    auth.currentUser.permissionCodes = ['ai:operations:view']
    await flushPromises()
    await wrapper.get('#tab-scenarios').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('所选时间范围暂无场景运行记录')
  })

  it('场景统计失败清除旧结果，并提示缩短范围与重试', async () => {
    await page()
    vi.mocked(getAiScenarioOperations).mockRejectedValue(new Error('capacity'))
    await refresh()
    expect(wrapper.text()).not.toContain('合成权限场景')
    expect(wrapper.text()).toContain('请缩短时间范围后重试')
    vi.mocked(getAiScenarioOperations).mockResolvedValue(statistics())
    await refresh()
    expect(wrapper.text()).toContain('合成权限场景')
  })

  it('旧汇总失败不阻止读取新场景统计', async () => {
    vi.mocked(getAiOperationsSummary).mockRejectedValue(new Error('overview'))
    await page()
    expect(wrapper.text()).toContain('运营汇总读取失败')
    expect(wrapper.text()).toContain('合成权限场景')
  })

  it('场景名称只按文字展示，不解释为 HTML', async () => {
    vi.mocked(getAiScenarioOperations).mockResolvedValue(
      statistics([row({ scenarioName: '<b>合成标签文字</b>' })]),
    )
    await page()
    expect(wrapper.text()).toContain('<b>合成标签文字</b>')
    expect(wrapper.find('.scenario-table b').exists()).toBe(false)
  })

  it('分页保留当前时间窗口，不在前端重新计算后端比率', async () => {
    await page()
    const first = vi.mocked(getAiScenarioOperations).mock.calls[0]![0]
    wrapper.findComponent(ElPagination).vm.$emit('current-change', 2)
    await flushPromises()
    expect(getAiScenarioOperations).toHaveBeenLastCalledWith({ ...first, pageIndex: 2 })
    wrapper.findComponent(ElPagination).vm.$emit('size-change', 10)
    await flushPromises()
    expect(getAiScenarioOperations).toHaveBeenLastCalledWith({
      ...first,
      pageIndex: 1,
      pageSize: 10,
    })
    expect(wrapper.text()).toContain('33.33%')
  })

  it('无效时间不发送请求，时间变化丢弃旧响应', async () => {
    await page()
    const old = deferred<AiScenarioOperations>()
    vi.mocked(getAiScenarioOperations).mockReturnValueOnce(old.promise)
    const refreshPromise = refresh()
    await flushPromises()
    wrapper.findComponent(ElDatePicker).vm.$emit('update:modelValue', null)
    await flushPromises()
    old.resolve(statistics([row({ scenarioName: '迟到场景' })]))
    await refreshPromise
    await flushPromises()
    expect(wrapper.text()).not.toContain('迟到场景')
    expect(wrapper.text()).toContain('请选择有效的时间范围')
    expect(getAiScenarioOperations).toHaveBeenCalledTimes(2)
  })

  it('撤权清除所有运营数据，并拒绝已发出的迟到响应', async () => {
    await page()
    const pending = deferred<AiScenarioOperations>()
    vi.mocked(getAiScenarioOperations).mockReturnValueOnce(pending.promise)
    const refreshPromise = refresh()
    await flushPromises()
    auth.currentUser.permissionCodes = []
    await flushPromises()
    pending.resolve(statistics([row({ scenarioName: '旧授权结果' })]))
    await refreshPromise
    await flushPromises()
    expect(wrapper.text()).toContain('当前无 AI 运营查看权限')
    expect(wrapper.text()).not.toContain('旧授权结果')
    expect(wrapper.find('.metrics-band').exists()).toBe(false)
  })

  it('目标租户切换清除旧数据，只接受当前请求', async () => {
    await page()
    const previous = deferred<AiScenarioOperations>()
    const next = deferred<AiScenarioOperations>()
    vi.mocked(getAiScenarioOperations)
      .mockReturnValueOnce(previous.promise)
      .mockReturnValueOnce(next.promise)
    const refreshPromise = refresh()
    await flushPromises()
    auth.effectiveTenantId = 'tenant-2'
    await flushPromises()
    expect(wrapper.text()).not.toContain('合成权限场景')
    previous.resolve(statistics([row({ scenarioName: '旧租户场景' })]))
    await refreshPromise
    await flushPromises()
    expect(wrapper.text()).not.toContain('旧租户场景')
    next.resolve(statistics([row({ scenarioName: '新租户场景' })]))
    await flushPromises()
    expect(wrapper.text()).toContain('新租户场景')
  })

  it('KeepAlive 停用清理结果，恢复时重新查询且丢弃旧响应', async () => {
    const visible = ref(true)
    wrapper = mount(
      defineComponent({
        components: { OperationsPage, KeepAlive },
        setup: () => ({ visible }),
        template: '<KeepAlive><OperationsPage v-if="visible" /></KeepAlive>',
      }),
      { global: { plugins: [ElementPlus] } },
    )
    await flushPromises()
    const pending = deferred<AiScenarioOperations>()
    vi.mocked(getAiScenarioOperations).mockReturnValueOnce(pending.promise)
    await wrapper.get('[aria-label="刷新运营统计"]').trigger('click')
    visible.value = false
    await flushPromises()
    pending.resolve(statistics([row({ scenarioName: '停用时的旧场景' })]))
    await flushPromises()
    vi.mocked(getAiScenarioOperations).mockResolvedValue(
      statistics([row({ scenarioName: '恢复后的场景' })]),
    )
    visible.value = true
    await flushPromises()
    await wrapper.get('#tab-scenarios').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('恢复后的场景')
    expect(wrapper.text()).not.toContain('停用时的旧场景')
    expect(getAiScenarioOperations).toHaveBeenCalledTimes(3)
  })

  async function exportPage() {
    auth.currentUser.permissionCodes.push('ai:operations:export')
    await page()
  }
  async function openExport() {
    await wrapper.get('[aria-label="导出技术元数据"]').trigger('click')
    await flushPromises()
  }
  async function confirmExport() {
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '确认导出 JSON')!
      .trigger('click')
    await flushPromises()
  }
  function downloadSpies() {
    const create = vi.fn(() => 'blob:synthetic')
    const revoke = vi.fn()
    vi.stubGlobal(
      'URL',
      class extends URL {
        static createObjectURL = create
        static revokeObjectURL = revoke
      },
    )
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    return { create, revoke, click }
  }
  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('查看权限不能导出，页面加载不自动下载', async () => {
    await page()
    expect(wrapper.find('[aria-label="导出技术元数据"]').exists()).toBe(false)
    expect(exportAiTechnicalMetadata).not.toHaveBeenCalled()
  })

  it('本人凭据入口只在双权限下显示，并由主动打开触发独立查询', async () => {
    await page()
    expect(wrapper.find('[aria-label="我的导出凭据"]').exists()).toBe(false)
    wrapper.unmount()
    vi.mocked(getAiTechnicalExportReceipts).mockResolvedValue({
      tenantId: 'tenant-1',
      scope: 'CurrentCaller',
      receiptFrom: '2026-10-01T00:00:00Z',
      receiptTo: '2026-10-02T00:00:00Z',
      observedFrom: '2026-10-02T00:00:00Z',
      observedTo: '2026-10-02T00:00:01Z',
      matchedRecordCount: 0,
      unreadableRecordCount: 0,
      windowInterpretable: true,
      exports: {
        items: [],
        pageIndex: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 0,
        hasPreviousPage: false,
        hasNextPage: false,
      },
    })
    await exportPage()
    expect(getAiTechnicalExportReceipts).not.toHaveBeenCalled()
    await wrapper.get('[aria-label="我的导出凭据"]').trigger('click')
    await flushPromises()
    expect(getAiTechnicalExportReceipts).toHaveBeenCalledTimes(1)
    expect(wrapper.text()).toContain('凭据记录时间')
  })

  it('主动确认当前范围后下载并释放 URL，不宣称浏览器已接收成功', async () => {
    const spies = downloadSpies()
    await exportPage()
    await openExport()
    expect(wrapper.text()).toContain('接收人：当前登录用户')
    expect(wrapper.text()).toContain('目标租户：tenant-1')
    expect(wrapper.text()).toContain('16 MiB')
    expect(exportAiTechnicalMetadata).not.toHaveBeenCalled()
    await confirmExport()
    const range = vi.mocked(getAiScenarioOperations).mock.calls[0]![0]
    expect(exportAiTechnicalMetadata).toHaveBeenCalledWith(
      { from: range.from, to: range.to },
      expect.any(AbortSignal),
    )
    expect(spies.create).toHaveBeenCalledTimes(1)
    expect(spies.click).toHaveBeenCalledTimes(1)
    expect(spies.revoke).toHaveBeenCalledWith('blob:synthetic')
  })

  it('导出失败保留重试入口，不下载错误 Blob', async () => {
    const spies = downloadSpies()
    vi.mocked(exportAiTechnicalMetadata).mockRejectedValueOnce(new Error('audit'))
    await exportPage()
    await openExport()
    await confirmExport()
    expect(wrapper.text()).toContain('导出失败')
    expect(spies.create).not.toHaveBeenCalled()
    await openExport()
    await confirmExport()
    expect(spies.click).toHaveBeenCalledTimes(1)
  })

  it.each(['tenant', 'permission', 'date', 'unmount'])(
    '%s 变化取消请求并丢弃迟到下载',
    async (change) => {
      const spies = downloadSpies()
      const pending = deferred<Awaited<ReturnType<typeof exportAiTechnicalMetadata>>>()
      vi.mocked(exportAiTechnicalMetadata).mockReturnValueOnce(pending.promise)
      await exportPage()
      await openExport()
      await confirmExport()
      const signal = vi.mocked(exportAiTechnicalMetadata).mock.calls[0]![1]
      if (change === 'tenant') auth.effectiveTenantId = 'tenant-2'
      if (change === 'permission') auth.currentUser.permissionCodes = ['ai:operations:view']
      if (change === 'date') wrapper.findComponent(ElDatePicker).vm.$emit('update:modelValue', null)
      if (change === 'unmount') wrapper.unmount()
      await flushPromises()
      expect(signal.aborted).toBe(true)
      pending.resolve({
        content: new Blob(['{}']),
        fileName: 'ai-technical-0123456789abcdef0123456789abcdef.json',
      })
      await flushPromises()
      expect(spies.create).not.toHaveBeenCalled()
      expect(spies.click).not.toHaveBeenCalled()
    },
  )

  it('KeepAlive 停用取消导出，恢复不自动重放下载', async () => {
    const spies = downloadSpies()
    const visible = ref(true)
    auth.currentUser.permissionCodes.push('ai:operations:export')
    wrapper = mount(
      defineComponent({
        components: { OperationsPage, KeepAlive },
        setup: () => ({ visible }),
        template: '<KeepAlive><OperationsPage v-if="visible" /></KeepAlive>',
      }),
      { global: { plugins: [ElementPlus] } },
    )
    await flushPromises()
    const pending = deferred<Awaited<ReturnType<typeof exportAiTechnicalMetadata>>>()
    vi.mocked(exportAiTechnicalMetadata).mockReturnValueOnce(pending.promise)
    await openExport()
    await confirmExport()
    const signal = vi.mocked(exportAiTechnicalMetadata).mock.calls[0]![1]
    visible.value = false
    await flushPromises()
    expect(signal.aborted).toBe(true)
    pending.resolve({
      content: new Blob(['{}']),
      fileName: 'ai-technical-0123456789abcdef0123456789abcdef.json',
    })
    visible.value = true
    await flushPromises()
    expect(spies.click).not.toHaveBeenCalled()
    expect(exportAiTechnicalMetadata).toHaveBeenCalledTimes(1)
  })
})
