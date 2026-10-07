import { mount } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { describe, expect, it } from 'vitest'
import type { AiStructuredResult } from '../api/ai'
import AiStructuredResultCard from './AiStructuredResultCard.vue'

function result(): AiStructuredResult {
  return {
    runId: 'run-1',
    invocationId: 'call-1',
    type: 'demo-business-orders',
    version: 1,
    toolCode: 'business.demo_business_order.query',
    toolVersion: '1.0',
    queriedAt: '2026-10-07T00:00:00Z',
    evaluationBasis: '当前授权范围，关键词仅匹配单号和标题',
    context: { version: 1, parameters: { approvalStatus: 'Pending', limit: 1 } },
    citation: {
      sourceSystem: 'PermissionSystem',
      datasetCode: 'demo-business-orders-readonly',
      datasetVersion: '1.0',
      toolCode: 'business.demo_business_order.query',
      toolVersion: '1.0',
      queryParametersDigest: 'digest',
      queriedAt: '2026-10-07T00:00:00Z',
      rowCount: 1,
    },
    isTruncated: true,
    limitations: ['Demo 验证数据，非真实 ERP／WMS 业务'],
    demoOrders: {
      totalCount: 500,
      displayedRowCount: 1,
      items: [
        {
          id: 'order-1',
          orderNo: 'DEMO-001',
          title: '<img src=x onerror=alert(1)>',
          approvalStatus: 'Pending',
          departmentId: null,
          createdAt: '2026-10-07T00:00:00Z',
        },
      ],
    },
  }
}

describe('Demo 只读单据卡片', () => {
  it('分开表达完整单数与展示行，沿用状态文案并转义业务文本', () => {
    const wrapper = mount(AiStructuredResultCard, {
      props: { result: result() },
      global: { plugins: [ElementPlus] },
    })
    expect(wrapper.text()).toContain('Demo 业务单据查询')
    expect(wrapper.text()).toContain('匹配总量：500 单；当前展示：1 行')
    expect(wrapper.text()).toContain('审批中')
    expect(wrapper.text()).toContain('历史查询结果，非历史状态快照')
    expect(wrapper.text()).toContain('非真实 ERP／WMS 业务')
    expect(wrapper.text()).toContain('<img src=x onerror=alert(1)>')
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('客户名称')
    expect(wrapper.text()).not.toContain('金额')
    expect(wrapper.find('a').exists()).toBe(false)
  })

  it('只提交实际引用且忙碌时禁止重复选择', async () => {
    const wrapper = mount(AiStructuredResultCard, {
      props: { result: result() },
      global: { plugins: [ElementPlus] },
    })
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('select')).toEqual([[{ runId: 'run-1', invocationId: 'call-1' }]])
    await wrapper.setProps({ busy: true })
    expect(wrapper.find('button').attributes('disabled')).toBeDefined()
  })

  it('空数据明确显示零单', () => {
    const value = result()
    value.demoOrders = { totalCount: 0, displayedRowCount: 0, items: [] }
    value.isTruncated = false
    const wrapper = mount(AiStructuredResultCard, {
      props: { result: value },
      global: { plugins: [ElementPlus] },
    })
    expect(wrapper.text()).toContain('匹配总量：0 单')
    expect(wrapper.text()).toContain('当前没有展示行')
  })

  it.each(['toolVersion', 'toolCode', 'payload'])('未知 %s 禁止展示和追问', (kind) => {
    const value = result()
    if (kind === 'toolVersion') value.toolVersion = '99'
    if (kind === 'toolCode') value.toolCode = 'unknown'
    if (kind === 'payload') value.demoOrders = undefined
    const wrapper = mount(AiStructuredResultCard, {
      props: { result: value },
      global: { plugins: [ElementPlus] },
    })
    expect(wrapper.text()).toContain('暂不支持展示')
    expect(wrapper.find('button').exists()).toBe(false)
    expect(wrapper.find('table').exists()).toBe(false)
  })
})
