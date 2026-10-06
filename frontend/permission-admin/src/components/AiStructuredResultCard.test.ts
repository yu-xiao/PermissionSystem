import { mount } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { describe, expect, it } from 'vitest'
import type { AiStructuredResult } from '../api/ai'
import AiStructuredResultCard from './AiStructuredResultCard.vue'

function result(): AiStructuredResult {
  return {
    runId: 'run-1',
    invocationId: 'call-1',
    type: 'table',
    version: 1,
    toolCode: 'permission.users.search',
    toolVersion: '1.0',
    queriedAt: '2026-10-06T08:00:00Z',
    evaluationBasis: '当前授权范围',
    context: {
      version: 1,
      parameters: {
        keyword: '<script>instruction</script>',
        limit: 1,
        departmentScope: 'CurrentDepartment',
      },
    },
    citation: {
      sourceSystem: 'PermissionSystem',
      toolCode: 'permission.users.search',
      toolVersion: '1.0',
      queryParametersDigest: 'digest',
      queriedAt: '2026-10-06T08:00:00Z',
      rowCount: 1,
    },
    isTruncated: true,
    limitations: [],
    table: {
      totalCount: 100,
      displayedRowCount: 1,
      items: [
        {
          id: 'user-1',
          userName: '<img src=x onerror=alert(1)>',
          displayName: '用户',
          isEnabled: true,
          createdAt: '2026-10-06T08:00:00Z',
        },
      ],
    },
  }
}

describe('AiStructuredResultCard', () => {
  it('展示总量、展示行、实际条件、截断、历史时间和来源，并转义注入文本', () => {
    const wrapper = mount(AiStructuredResultCard, {
      props: { result: result() },
      global: { plugins: [ElementPlus] },
    })
    expect(wrapper.text()).toContain('匹配总量：100；当前展示：1 行')
    expect(wrapper.text()).toContain('本部门（不含下级，与授权范围取交集）')
    expect(wrapper.text()).toContain('历史快照')
    expect(wrapper.text()).toContain('结果已截断')
    expect(wrapper.text()).toContain('PermissionSystem')
    expect(wrapper.text()).toContain('<script>instruction</script>')
    expect(wrapper.find('script').exists()).toBe(false)
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('a').exists()).toBe(false)
  })

  it('只发送结果引用，选择后显示状态，忙碌期间禁用选择', async () => {
    const wrapper = mount(AiStructuredResultCard, {
      props: { result: result() },
      global: { plugins: [ElementPlus] },
    })
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('select')).toEqual([[{ runId: 'run-1', invocationId: 'call-1' }]])
    await wrapper.setProps({ selected: true, busy: true })
    expect(wrapper.text()).toContain('已选择追问对象')
    expect(wrapper.find('button').attributes('disabled')).toBeDefined()
  })

  it('统计摘要分开表达日志总量、总分组与展示分组', () => {
    const value = result()
    value.type = 'statistics-summary'
    value.table = undefined
    value.statistics = {
      totalCount: 800,
      groups: [
        {
          code: 'byModule',
          totalGroupCount: 25,
          displayedGroupCount: 20,
          isTruncated: true,
          items: [{ key: '<b>Inventory</b>', count: 100 }],
        },
      ],
    }
    const wrapper = mount(AiStructuredResultCard, {
      props: { result: value },
      global: { plugins: [ElementPlus] },
    })
    expect(wrapper.text()).toContain('匹配日志总量：800 条')
    expect(wrapper.text()).toContain('共 25 组，展示 20 组')
    expect(wrapper.text()).toContain('已截断')
    expect(wrapper.find('b').exists()).toBe(false)
  })

  it.each(['version', 'type', 'context'])('未知 %s 保留提示并禁止引用', (kind) => {
    const value = result()
    if (kind === 'version') value.version = 99
    if (kind === 'type') value.type = 'unknown'
    if (kind === 'context') value.context.version = 99
    const wrapper = mount(AiStructuredResultCard, {
      props: { result: value },
      global: { plugins: [ElementPlus] },
    })
    expect(wrapper.text()).toContain('暂不支持展示')
    expect(wrapper.find('button').exists()).toBe(false)
    expect(wrapper.find('table').exists()).toBe(false)
  })
})
