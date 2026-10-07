import { mount } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { describe, expect, it } from 'vitest'
import type { AiControlledUserMetrics, AiStructuredResult } from '../api/ai'
import AiControlledMetricsCard from './AiControlledMetricsCard.vue'
import AiStructuredResultCard from './AiStructuredResultCard.vue'

function metrics(): AiControlledUserMetrics {
  return {
    dimension: 'DepartmentId',
    definitions: [
      { code: 'user-count', name: '用户总数', definition: '按用户 ID，一人一条记录', unit: '人' },
    ],
    totals: { userCount: 5000, enabledUserCount: 4000, disabledUserCount: 1000 },
    totalGroupCount: 5,
    displayedGroupCount: 1,
    isTruncated: true,
    groups: [
      { key: null, values: { userCount: 1000, enabledUserCount: 800, disabledUserCount: 200 } },
    ],
  }
}

function result(): AiStructuredResult {
  return {
    runId: 'run',
    invocationId: 'call',
    type: 'controlled-report',
    version: 1,
    toolCode: 'permission.reports.query_dataset',
    toolVersion: '2.0',
    queriedAt: '2026-10-06T08:00:00Z',
    evaluationBasis: '当前授权范围与筛选取交集',
    isTruncated: true,
    context: {
      version: 1,
      parameters: {
        reportDefinitionId: 'report',
        mode: 'Metrics',
        params: { endTime: '2026-10-01T00:00:00Z', keyword: '<b>说明</b>' },
      },
    },
    citation: {
      sourceSystem: 'PermissionSystem',
      toolCode: 'permission.reports.query_dataset',
      toolVersion: '2.0',
      queriedAt: '2026-10-06T08:00:00Z',
      rowCount: 1,
      queryParametersDigest: 'digest',
    },
    limitations: ['实时读取当前状态，非历史人员快照；无独立数据水位'],
    metrics: metrics(),
    report: {
      reportDefinitionId: 'report',
      datasetKey: 'system-users-scoped',
      datasetVersion: '1.0',
      definitionFingerprint: 'hash',
    },
  }
}

describe('AIC-006 用户指标', () => {
  it('总数独立于已截断分组，使用人数单位和空部门口径', () => {
    const wrapper = mount(AiControlledMetricsCard, { props: { metrics: metrics() } })
    expect(wrapper.text()).toContain('匹配用户总数：5000 人')
    expect(wrapper.text()).toContain('启用：4000 人；停用：1000 人')
    expect(wrapper.text()).toContain('共 5 组，展示 1 组')
    expect(wrapper.text()).toContain('指标总数仍为完整匹配集')
    expect(wrapper.text()).toContain('未分配部门：总数 1000 人')
    expect(wrapper.text()).not.toContain('日志')
  })

  it('零数据不编造部门或启停分组', () => {
    const value = metrics()
    value.dimension = 'None'
    value.totals = { userCount: 0, enabledUserCount: 0, disabledUserCount: 0 }
    value.groups = []
    value.totalGroupCount = value.displayedGroupCount = 0
    value.isTruncated = false
    const wrapper = mount(AiControlledMetricsCard, { props: { metrics: value } })
    expect(wrapper.text()).toContain('匹配用户总数：0 人')
    expect(wrapper.find('ul').exists()).toBe(false)
  })

  it('显示安全条件、数据时间限制与引用，转义所有数据文本', async () => {
    const value = result()
    value.metrics!.groups[0]!.key = '<img src=x onerror=alert(1)>'
    const wrapper = mount(AiStructuredResultCard, {
      props: { result: value },
      global: { plugins: [ElementPlus] },
    })
    expect(wrapper.text()).toContain('创建结束时间（不含）')
    expect(wrapper.text()).toContain('非历史人员快照')
    expect(wrapper.text()).toContain('<b>说明</b>')
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('b').exists()).toBe(false)
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('select')).toEqual([[{ runId: 'run', invocationId: 'call' }]])
  })

  it.each(['version', 'metadata', 'mixed'])('未知或不完整指标 %s 禁止追问', (kind) => {
    const value = result()
    if (kind === 'version') value.toolVersion = '1.0'
    if (kind === 'metadata') value.report = undefined
    if (kind === 'mixed') value.table = { totalCount: 0, displayedRowCount: 0, items: [] }
    const wrapper = mount(AiStructuredResultCard, {
      props: { result: value },
      global: { plugins: [ElementPlus] },
    })
    expect(wrapper.text()).toContain('暂不支持展示')
    expect(wrapper.find('button').exists()).toBe(false)
  })
})
