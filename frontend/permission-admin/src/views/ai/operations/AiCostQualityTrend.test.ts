import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import ElementPlus, { ElDatePicker, ElPagination, ElRadioGroup } from 'element-plus'
import { defineComponent, h, KeepAlive, reactive, ref } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import Quality from './AiCostQuality.vue'
import {
  getAiCostQuality,
  getAiCostQualityTrends,
  type AiCostQualityResponse,
  type AiCostQualityTrendResponse,
} from '../../../api/ai'

let auth: {
  effectiveTenantId: string
  currentUser: { userId: string; permissionCodes: string[]; roles: string[]; isSuperAdmin: boolean }
  hasPermission: (code: string) => boolean
}
vi.mock('../../../stores/auth', () => ({ useAuthStore: () => auth }))
vi.mock('../../../api/ai', () => ({ getAiCostQuality: vi.fn(), getAiCostQualityTrends: vi.fn() }))
type Params = Parameters<typeof getAiCostQualityTrends>[0]
function summary(params: Params): AiCostQualityResponse {
  return {
    metricsVersion: 1,
    scope: 'CurrentTenantReadableRunInvocations',
    costBasis: 'HistoricalSnapshotEstimateNotSupplierInvoice',
    tenantId: 'tenant-1',
    from: params.from!,
    to: params.to!,
    observedFrom: params.from!,
    observedTo: params.to!,
    limits: { maxUsages: 50000, readSeconds: 10 },
    population: {
      invocationCount: 0,
      terminalCount: 0,
      unsettledCount: 0,
      unknownStatusCount: 0,
      comparableCostCount: 0,
      consistentCostCount: 0,
      differentCostCount: 0,
      uncomparableCostCount: 0,
    },
    basis: {
      recordedBothCount: 0,
      mixedFallbackCount: 0,
      estimatedBothCount: 0,
      unusableTokenPairCount: 0,
    },
    issues: [{ code: 'MissingPrice', count: 0 }],
    inputComparison: {
      sampleCount: 0,
      recordedTokens: 0,
      estimatedTokens: 0,
      differenceTokens: 0,
      absoluteDifferenceTokens: 0,
      weightedRatioPercentage: null,
      zeroEstimatePairCount: 0,
      aboveEstimateCount: 0,
    },
    outputLimitComparison: {
      sampleCount: 0,
      recordedTokens: 0,
      estimatedTokens: 0,
      differenceTokens: 0,
      absoluteDifferenceTokens: 0,
      weightedRatioPercentage: null,
      zeroEstimatePairCount: 0,
      aboveEstimateCount: 0,
    },
    totalTokens: { comparableCount: 0, differentCount: 0 },
    currencies: {
      items: [],
      totalCount: 0,
      pageIndex: params.pageIndex!,
      pageSize: params.pageSize!,
      totalPages: 0,
      hasNextPage: false,
      hasPreviousPage: false,
    },
  }
}
function trend(params: Params): AiCostQualityTrendResponse {
  const base = summary(params)
  return {
    ...base,
    grouping: 'UsageCreatedAtUtcDay',
    bucketTimezone: 'UTC',
    daily: [
      {
        date: '2026-10-01',
        bucketFrom: params.from!,
        bucketTo: params.to!,
        isPartialDay: true,
        population: base.population,
        basis: base.basis,
        issues: base.issues,
        inputComparison: base.inputComparison,
        outputLimitComparison: base.outputLimitComparison,
        totalTokens: base.totalTokens,
      },
    ],
    currencies: {
      ...base.currencies,
      totalCount: 21,
      totalPages: 2,
      hasNextPage: true,
      items: [
        {
          storedAboveRecomputedCount: 1,
          storedBelowRecomputedCount: 1,
          summary: {
            currency: 'USD',
            terminalCount: 3,
            basis: base.basis,
            comparableCostCount: 3,
            consistentCostCount: 1,
            differentCostCount: 2,
            uncomparableCostCount: 0,
            storedCost: '1999999999999.999998',
            recomputedCost: '0',
            differenceCost: '1999999999999.999998',
            absoluteDifferenceCost: '1999999999999.999998',
          },
        },
      ],
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
async function view(value: 'summary' | 'trends') {
  wrapper.findComponent(ElRadioGroup).vm.$emit('update:modelValue', value)
  await flushPromises()
}

describe('只读质量趋势第二批', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    auth = reactive({
      effectiveTenantId: 'tenant-1',
      currentUser: {
        userId: 'actor',
        permissionCodes: ['ai:operations:view'],
        roles: [],
        isSuperAdmin: false,
      },
      hasPermission(code: string) {
        return this.currentUser.permissionCodes.includes(code)
      },
    })
    vi.mocked(getAiCostQuality).mockImplementation(async (params) => summary(params))
    vi.mocked(getAiCostQualityTrends).mockImplementation(async (params) => trend(params))
  })
  afterEach(() => {
    wrapper?.unmount()
    vi.restoreAllMocks()
  })
  it('默认仅首批摘要，主动选择趋势才请求，返回摘要重新观察', async () => {
    await open()
    expect(getAiCostQuality).toHaveBeenCalledTimes(1)
    expect(getAiCostQualityTrends).not.toHaveBeenCalled()
    await view('trends')
    expect(getAiCostQualityTrends).toHaveBeenCalledTimes(1)
    expect(getAiCostQuality).toHaveBeenCalledTimes(1)
    expect(wrapper.text()).toContain('UTC 按日质量趋势')
    expect(wrapper.text()).not.toContain('分币种历史费用一致性')
    await view('summary')
    expect(getAiCostQuality).toHaveBeenCalledTimes(2)
    expect(wrapper.text()).not.toContain('UTC 按日质量趋势')
  })
  it('显示 UTC 原日期、部分日／空日说明和差异方向，精确金额不转 Number', async () => {
    await open()
    await view('trends')
    for (const text of [
      '2026-10-01',
      '部分日',
      '当前可读范围未观察到',
      '不取每日比率平均',
      '存储高于重算',
      '存储等于重算',
      '存储低于重算',
      '1999999999999.999998',
      '0.000000',
      '—',
      '重新观察',
    ])
      expect(wrapper.text()).toContain(text)
    expect(wrapper.find('input[type=file]').exists()).toBe(false)
  })
  it('币种翻页继续使用趋势 GET 和同窗口，窗口变化复位页码', async () => {
    await open()
    await view('trends')
    const params = vi.mocked(getAiCostQualityTrends).mock.calls[0]![0]
    wrapper.findComponent(ElPagination).vm.$emit('current-change', 2)
    await flushPromises()
    expect(getAiCostQualityTrends).toHaveBeenLastCalledWith(
      { ...params, pageIndex: 2 },
      expect.any(AbortSignal),
    )
    wrapper
      .findComponent(ElDatePicker)
      .vm.$emit('update:modelValue', [
        new Date(Date.parse(params.from!) - 86400000),
        new Date(params.to!),
      ])
    await flushPromises()
    expect(vi.mocked(getAiCostQualityTrends).mock.calls.at(-1)![0].pageIndex).toBe(1)
    expect(getAiCostQuality).toHaveBeenCalledTimes(1)
  })
  it.each(['tenant', 'actor', 'permission', 'roles', 'super', 'date', 'view', 'close', 'unmount'])(
    '%s 变化取消趋势并丢弃迟到结果',
    async (change) => {
      const pending = deferred<AiCostQualityTrendResponse>()
      vi.mocked(getAiCostQualityTrends).mockReturnValueOnce(pending.promise)
      await open()
      await view('trends')
      const [params, signal] = vi.mocked(getAiCostQualityTrends).mock.calls[0]!
      if (change === 'tenant') auth.effectiveTenantId = 'tenant-2'
      if (change === 'actor') auth.currentUser.userId = 'other'
      if (change === 'permission') auth.currentUser.permissionCodes = []
      if (change === 'roles') auth.currentUser.roles = ['changed']
      if (change === 'super') auth.currentUser.isSuperAdmin = true
      if (change === 'date') wrapper.findComponent(ElDatePicker).vm.$emit('update:modelValue', null)
      if (change === 'view') await view('summary')
      if (change === 'close') await wrapper.setProps({ modelValue: false })
      if (change === 'unmount') wrapper.unmount()
      await flushPromises()
      expect(signal.aborted).toBe(true)
      pending.resolve(trend(params))
      await flushPromises()
      expect(wrapper.text()).not.toContain('全窗口分币种费用差异方向')
    },
  )
  it.each(['tenant', 'window', 'scope', 'page', 'version', 'grouping', 'timezone', 'basis'])(
    '拒绝 %s 不符的趋势响应',
    async (change) => {
      vi.mocked(getAiCostQualityTrends).mockImplementationOnce(async (params) => {
        const value = trend(params)
        if (change === 'tenant') value.tenantId = 'tenant-2'
        if (change === 'window') value.to = '2000-01-01T00:00:00Z'
        if (change === 'scope') value.scope = 'unknown' as typeof value.scope
        if (change === 'page') value.currencies.pageSize = 50
        if (change === 'version') value.metricsVersion = 2
        if (change === 'grouping') value.grouping = 'unknown' as typeof value.grouping
        if (change === 'timezone') value.bucketTimezone = 'browser' as typeof value.bucketTimezone
        if (change === 'basis') value.costBasis = 'unknown' as typeof value.costBasis
        return value
      })
      await open()
      await view('trends')
      expect(wrapper.text()).toContain('估算质量读取失败')
      expect(wrapper.text()).not.toContain('UTC 按日质量趋势')
    },
  )
  it('趋势失败可重试，非法日期不继续读，关闭后默认恢复摘要', async () => {
    vi.mocked(getAiCostQualityTrends).mockRejectedValueOnce(new Error('synthetic'))
    await open()
    await view('trends')
    expect(wrapper.text()).toContain('估算质量读取失败')
    await wrapper
      .findAll('button')
      .find((b) => b.text() === '查询质量')!
      .trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('UTC 按日质量趋势')
    wrapper.findComponent(ElDatePicker).vm.$emit('update:modelValue', null)
    await flushPromises()
    expect(getAiCostQualityTrends).toHaveBeenCalledTimes(2)
    await wrapper.setProps({ modelValue: false })
    await wrapper.setProps({ modelValue: true })
    await flushPromises()
    expect(wrapper.findComponent(ElRadioGroup).props('modelValue')).toBe('summary')
    expect(getAiCostQualityTrends).toHaveBeenCalledTimes(2)
  })
  it('重查后的趋势不能被旧观察覆盖', async () => {
    const pending = deferred<AiCostQualityTrendResponse>()
    vi.mocked(getAiCostQualityTrends).mockReturnValueOnce(pending.promise)
    await open()
    await view('trends')
    const [params, signal] = vi.mocked(getAiCostQualityTrends).mock.calls[0]!
    wrapper
      .findComponent(ElDatePicker)
      .vm.$emit('update:modelValue', [
        new Date(Date.parse(params.from!) - 86400000),
        new Date(params.to!),
      ])
    await flushPromises()
    expect(signal.aborted).toBe(true)
    const old = trend(params)
    old.daily[0]!.date = 'DO_NOT_DISPLAY'
    pending.resolve(old)
    await flushPromises()
    expect(wrapper.text()).not.toContain('DO_NOT_DISPLAY')
    expect(wrapper.text()).toContain('2026-10-01')
  })
  it('无权不请求趋势', async () => {
    auth.currentUser.permissionCodes = []
    await open()
    expect(wrapper.findComponent(ElRadioGroup).exists()).toBe(false)
    expect(getAiCostQuality).not.toHaveBeenCalled()
    expect(getAiCostQualityTrends).not.toHaveBeenCalled()
  })
  it('KeepAlive 停用清理趋势，不自动恢复请求', async () => {
    const visible = ref(true),
      opened = ref(true),
      pending = deferred<AiCostQualityTrendResponse>()
    vi.mocked(getAiCostQualityTrends).mockReturnValueOnce(pending.promise)
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
    await view('trends')
    const [params, signal] = vi.mocked(getAiCostQualityTrends).mock.calls[0]!
    visible.value = false
    await flushPromises()
    expect(signal.aborted).toBe(true)
    expect(opened.value).toBe(false)
    pending.resolve(trend(params))
    visible.value = true
    await flushPromises()
    expect(getAiCostQualityTrends).toHaveBeenCalledTimes(1)
    expect(wrapper.text()).not.toContain('UTC 按日质量趋势')
  })
})
