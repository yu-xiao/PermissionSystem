import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import ElementPlus, { ElDatePicker, ElPagination } from 'element-plus'
import { defineComponent, h, KeepAlive, reactive, ref } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import Quality from './AiCostQuality.vue'
import { getAiCostQuality, type AiCostQualityResponse } from '../../../api/ai'

let auth: {
  effectiveTenantId: string
  currentUser: { userId: string; permissionCodes: string[]; roles: string[] }
  hasPermission: (code: string) => boolean
}
vi.mock('../../../stores/auth', () => ({ useAuthStore: () => auth }))
vi.mock('../../../api/ai', () => ({ getAiCostQuality: vi.fn(), getAiCostQualityTrends: vi.fn() }))
function response(params: {
  from?: string
  to?: string
  pageIndex?: number
  pageSize?: number
}): AiCostQualityResponse {
  return {
    metricsVersion: 1,
    scope: 'CurrentTenantReadableRunInvocations',
    costBasis: 'HistoricalSnapshotEstimateNotSupplierInvoice',
    tenantId: 'tenant-1',
    from: params.from!,
    to: params.to!,
    observedFrom: params.to!,
    observedTo: params.to!,
    limits: { maxUsages: 50000, readSeconds: 10 },
    population: {
      invocationCount: 3,
      terminalCount: 2,
      unsettledCount: 1,
      unknownStatusCount: 0,
      comparableCostCount: 1,
      consistentCostCount: 0,
      differentCostCount: 1,
      uncomparableCostCount: 1,
    },
    basis: {
      recordedBothCount: 1,
      mixedFallbackCount: 1,
      estimatedBothCount: 0,
      unusableTokenPairCount: 0,
    },
    issues: [{ code: 'MissingPrice', count: 1 }],
    inputComparison: {
      sampleCount: 0,
      recordedTokens: 0,
      estimatedTokens: 0,
      differenceTokens: 0,
      absoluteDifferenceTokens: 0,
      weightedRatioPercentage: null,
      zeroEstimatePairCount: 1,
      aboveEstimateCount: 0,
    },
    outputLimitComparison: {
      sampleCount: 1,
      recordedTokens: 300,
      estimatedTokens: 100,
      differenceTokens: 200,
      absoluteDifferenceTokens: 200,
      weightedRatioPercentage: 300,
      zeroEstimatePairCount: 0,
      aboveEstimateCount: 1,
    },
    totalTokens: { comparableCount: 1, differentCount: 1 },
    currencies: {
      items: [
        {
          currency: 'CNY',
          terminalCount: 1,
          basis: {
            recordedBothCount: 1,
            mixedFallbackCount: 0,
            estimatedBothCount: 0,
            unusableTokenPairCount: 0,
          },
          comparableCostCount: 1,
          consistentCostCount: 1,
          differentCostCount: 0,
          uncomparableCostCount: 0,
          storedCost: '0',
          recomputedCost: '0',
          differenceCost: '0',
          absoluteDifferenceCost: '0',
        },
        {
          currency: 'USD',
          terminalCount: 1,
          basis: {
            recordedBothCount: 0,
            mixedFallbackCount: 1,
            estimatedBothCount: 0,
            unusableTokenPairCount: 0,
          },
          comparableCostCount: 0,
          consistentCostCount: 0,
          differentCostCount: 0,
          uncomparableCostCount: 1,
          storedCost: null,
          recomputedCost: null,
          differenceCost: null,
          absoluteDifferenceCost: null,
        },
      ],
      totalCount: 21,
      pageIndex: params.pageIndex!,
      pageSize: params.pageSize!,
      totalPages: 2,
      hasPreviousPage: false,
      hasNextPage: true,
    },
  }
}
function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((done) => {
    resolve = done
  })
  return { promise, resolve }
}
let wrapper: VueWrapper
async function open() {
  wrapper = mount(Quality, { props: { modelValue: true }, global: { plugins: [ElementPlus] } })
  await flushPromises()
}
async function button(text: string) {
  await wrapper
    .findAll('button')
    .find((b) => b.text() === text)!
    .trigger('click')
  await flushPromises()
}

describe('只读估算质量', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    auth = reactive({
      effectiveTenantId: 'tenant-1',
      currentUser: { userId: 'actor', permissionCodes: ['ai:operations:view'], roles: [] },
      hasPermission(code: string) {
        return this.currentUser.permissionCodes.includes(code)
      },
    })
    vi.mocked(getAiCostQuality).mockImplementation(async (params) => response(params))
  })
  afterEach(() => {
    wrapper?.unmount()
    vi.restoreAllMocks()
  })
  it('关闭或无权不读，只有 view 主动打开无需 export', async () => {
    wrapper = mount(Quality, { props: { modelValue: false }, global: { plugins: [ElementPlus] } })
    await flushPromises()
    expect(getAiCostQuality).not.toHaveBeenCalled()
    auth.currentUser.permissionCodes = []
    await wrapper.setProps({ modelValue: true })
    await flushPromises()
    expect(getAiCostQuality).not.toHaveBeenCalled()
    wrapper.unmount()
    auth.currentUser.permissionCodes = ['ai:operations:view']
    await open()
    expect(getAiCostQuality).toHaveBeenCalledTimes(1)
  })
  it('显示独立调用时间、样本、未知与零，不提供调价或导入动作', async () => {
    await open()
    expect(wrapper.text()).toContain('调用记录时间')
    expect(wrapper.text()).toContain('不是完整历史或供应商账期')
    expect(wrapper.text()).toContain('成对样本')
    expect(wrapper.text()).toContain('300%')
    expect(wrapper.text()).toContain('0.000000')
    expect(wrapper.text()).toContain('—')
    expect(wrapper.text()).toContain('缺单价快照')
    expect(wrapper.text()).toContain('问题计数可以重叠')
    expect(wrapper.find('input[type=file]').exists()).toBe(false)
    expect(wrapper.findAll('button').some((b) => /导入|改价|修复/.test(b.text()))).toBe(false)
  })
  it('币种分页保留相同调用窗口并用取消信号，不复用上页结果', async () => {
    await open()
    const original = vi.mocked(getAiCostQuality).mock.calls[0]![0]
    wrapper.findComponent(ElPagination).vm.$emit('update:currentPage', 2)
    wrapper.findComponent(ElPagination).vm.$emit('current-change', 2)
    await flushPromises()
    expect(getAiCostQuality).toHaveBeenLastCalledWith(
      { ...original, pageIndex: 2 },
      expect.any(AbortSignal),
    )
    expect(wrapper.text()).toContain('窗口全局样本不随页变化')
  })
  it.each(['tenant', 'actor', 'permission', 'roles', 'date', 'close', 'unmount'])(
    '%s 变化取消请求并丢弃迟到金额',
    async (change) => {
      const pending = deferred<AiCostQualityResponse>()
      vi.mocked(getAiCostQuality).mockReturnValueOnce(pending.promise)
      await open()
      const [params, signal] = vi.mocked(getAiCostQuality).mock.calls[0]!
      if (change === 'tenant') auth.effectiveTenantId = 'tenant-2'
      if (change === 'actor') auth.currentUser.userId = 'other'
      if (change === 'permission') auth.currentUser.permissionCodes = []
      if (change === 'roles') auth.currentUser.roles = ['changed']
      if (change === 'date') wrapper.findComponent(ElDatePicker).vm.$emit('update:modelValue', null)
      if (change === 'close') await wrapper.setProps({ modelValue: false })
      if (change === 'unmount') wrapper.unmount()
      await flushPromises()
      expect(signal.aborted).toBe(true)
      pending.resolve(response(params))
      await flushPromises()
      expect(wrapper.text()).not.toContain('分币种历史费用一致性')
    },
  )
  it.each(['tenant', 'window', 'scope', 'page', 'version'])(
    '拒绝 %s 不符的响应',
    async (change) => {
      vi.mocked(getAiCostQuality).mockImplementationOnce(async (params) => {
        const value = response(params)
        if (change === 'tenant') value.tenantId = 'tenant-2'
        if (change === 'window') value.from = '2000-01-01T00:00:00Z'
        if (change === 'scope') value.scope = 'unknown' as typeof value.scope
        if (change === 'page') value.currencies.pageIndex = 7
        if (change === 'version') value.metricsVersion = 2
        return value
      })
      await open()
      expect(wrapper.text()).toContain('估算质量读取失败')
      expect(wrapper.text()).not.toContain('分币种历史费用一致性')
    },
  )
  it('有效窗口以外先拒绝，失败可以重试', async () => {
    vi.mocked(getAiCostQuality).mockRejectedValueOnce(new Error('synthetic'))
    await open()
    expect(wrapper.text()).toContain('估算质量读取失败')
    await button('查询质量')
    expect(wrapper.text()).toContain('分币种历史费用一致性')
    wrapper.findComponent(ElDatePicker).vm.$emit('update:modelValue', [new Date(0), new Date()])
    await flushPromises()
    expect(wrapper.text()).toContain('最多 90 天')
    expect(getAiCostQuality).toHaveBeenCalledTimes(2)
  })
  it('窗口变化发起新查询，旧响应不能盖住新结果', async () => {
    const pending = deferred<AiCostQualityResponse>()
    vi.mocked(getAiCostQuality).mockReturnValueOnce(pending.promise)
    await open()
    const [params, signal] = vi.mocked(getAiCostQuality).mock.calls[0]!
    wrapper
      .findComponent(ElDatePicker)
      .vm.$emit('update:modelValue', [
        new Date(Date.parse(params.from!) - 86400000),
        new Date(params.to!),
      ])
    await flushPromises()
    expect(signal.aborted).toBe(true)
    const old = response(params)
    old.issues = [{ code: 'ArithmeticOverflow', count: 999 }]
    pending.resolve(old)
    await flushPromises()
    expect(wrapper.text()).not.toContain('999')
  })
  it('大额十进制字符串展示保留六位精度，不转为浏览器浮点数', async () => {
    vi.mocked(getAiCostQuality).mockImplementationOnce(async (params) => {
      const value = response(params)
      value.currencies.items[0]!.storedCost = '1999999999999.999998'
      return value
    })
    await open()
    expect(wrapper.text()).toContain('1999999999999.999998')
  })
  it('KeepAlive 停用清理且不自动恢复查询', async () => {
    const visible = ref(true),
      opened = ref(true)
    const pending = deferred<AiCostQualityResponse>()
    vi.mocked(getAiCostQuality).mockReturnValueOnce(pending.promise)
    wrapper = mount(
      defineComponent({
        setup: () => () =>
          h(KeepAlive, () =>
            visible.value
              ? h(Quality, {
                  modelValue: opened.value,
                  'onUpdate:modelValue': (value: boolean) => {
                    opened.value = value
                  },
                })
              : null,
          ),
      }),
      { global: { plugins: [ElementPlus] } },
    )
    await flushPromises()
    const [params, signal] = vi.mocked(getAiCostQuality).mock.calls[0]!
    visible.value = false
    await flushPromises()
    expect(signal.aborted).toBe(true)
    expect(opened.value).toBe(false)
    pending.resolve(response(params))
    visible.value = true
    await flushPromises()
    expect(getAiCostQuality).toHaveBeenCalledTimes(1)
    expect(wrapper.text()).not.toContain('分币种历史费用一致性')
  })
})
