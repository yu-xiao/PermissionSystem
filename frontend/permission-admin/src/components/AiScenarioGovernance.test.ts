import { flushPromises, mount } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import AiScenarioGovernance from './AiScenarioGovernance.vue'
import {
  getAiScenarioDetail,
  getAiScenarioEvaluationReport,
  listAiScenarios,
  reviewAiScenarioEvaluation,
  type AiScenarioDetail,
} from '../api/aiScenario'

const state = vi.hoisted(() => ({ manage: true }))
vi.mock('../stores/auth', () => ({ useAuthStore: () => ({ hasPermission: () => state.manage }) }))
vi.mock('../api/aiScenario', () => ({
  listAiScenarios: vi.fn(),
  getAiScenarioDetail: vi.fn(),
  saveAiScenario: vi.fn(),
  freezeAiScenario: vi.fn(),
  copyAiScenarioVersion: vi.fn(),
  exportAiScenarioSnapshot: vi.fn(),
  importAiScenarioEvaluation: vi.fn(),
  getAiScenarioEvaluationReport: vi.fn(),
  reviewAiScenarioEvaluation: vi.fn(),
  changeAiScenarioVersion: vi.fn(),
  revokeAiScenarioEvaluation: vi.fn(),
}))
const detail = (): AiScenarioDetail => ({
  scenario: {
    id: 's1',
    code: 'permission-assistant',
    name: '权限助手',
    description: '',
    isEnabled: true,
    revision: 1,
    concurrencyToken: 'token-1',
    configuration: {
      supplementPrompt: '',
      toolCodes: ['permission.diagnose'],
      maxModelRounds: 6,
      maxToolCalls: 10,
      maxHistoryMessages: 20,
      maxRunSeconds: 90,
      temperature: 0,
      maxTokens: 2048,
    },
  },
  versions: [
    {
      id: 'v1',
      versionNumber: 1,
      contentHash: 'hash-1',
      buildIdentity: 'build-1',
      createdAt: '',
      published: false,
      stopped: false,
      compatible: true,
    },
  ],
  evaluations: [
    {
      id: 'e1',
      versionId: 'v1',
      reportHash: 'report-1',
      mode: 'Offline',
      modelFingerprint: 'offline',
      automaticPassed: true,
      createdAt: '',
    },
  ],
  events: [],
})
const render = () =>
  mount(AiScenarioGovernance, {
    global: {
      plugins: [ElementPlus],
      stubs: {
        ElDialog: {
          props: ['modelValue'],
          template: '<div v-if="modelValue"><slot /><slot name="footer" /></div>',
        },
      },
    },
  })

describe('AiScenarioGovernance', () => {
  beforeEach(() => {
    state.manage = true
    vi.clearAllMocks()
    vi.mocked(listAiScenarios).mockResolvedValue([detail().scenario])
    vi.mocked(getAiScenarioDetail).mockResolvedValue(detail())
    vi.mocked(getAiScenarioEvaluationReport).mockResolvedValue({
      mode: 'Offline',
      results: [
        {
          key: 'case-1',
          caseHash: 'case-hash',
          status: 'Passed',
          safetyCritical: true,
          steps: [
            {
              input: '合成请求',
              output: '<script>untrusted()</script>',
              checks: [{ code: 'scope', passed: true }],
            },
          ],
        },
      ],
    })
  })
  it('只读管理员看不到变更、发布、审核提交或撤销入口', async () => {
    state.manage = false
    const wrapper = render()
    await flushPromises()
    expect(wrapper.text()).toContain('查看报告')
    for (const action of ['编辑草稿', '冻结候选版本', '复制为草稿', '导入评测', '撤销资格'])
      expect(wrapper.text()).not.toContain(action)
    await wrapper
      .findAll('button')
      .find((b) => b.text() === '查看报告')!
      .trigger('click')
    await flushPromises()
    expect(wrapper.text()).not.toContain('保存人工审核')
    expect(wrapper.find('script').exists()).toBe(false)
    wrapper.unmount()
  })
  it('自动通过不会自动勾选事实或黄金审核，缺少依据时不提交', async () => {
    const wrapper = render()
    await flushPromises()
    await wrapper
      .findAll('button')
      .find((b) => b.text() === '逐案例审核')!
      .trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('<script>untrusted()</script>')
    expect(wrapper.find('script').exists()).toBe(false)
    expect(
      wrapper
        .findAll('input[type="checkbox"]')
        .every((e) => !(e.element as HTMLInputElement).checked),
    ).toBe(true)
    await wrapper
      .findAll('button')
      .find((b) => b.text() === '保存人工审核')!
      .trigger('click')
    await flushPromises()
    expect(reviewAiScenarioEvaluation).not.toHaveBeenCalled()
    wrapper.unmount()
  })
  it('代码不兼容候选禁用导出、评测导入和发布，仍允许复制为草稿', async () => {
    const incompatible = detail()
    incompatible.versions[0]!.compatible = false
    vi.mocked(getAiScenarioDetail).mockResolvedValue(incompatible)
    const wrapper = render()
    await flushPromises()
    for (const action of ['导出快照', '导入评测', '发布'])
      expect(
        wrapper
          .findAll('button')
          .find((b) => b.text() === action)!
          .attributes('disabled'),
      ).toBeDefined()
    expect(
      wrapper
        .findAll('button')
        .find((b) => b.text() === '复制为草稿')!
        .attributes('disabled'),
    ).toBeUndefined()
    wrapper.unmount()
  })
})
