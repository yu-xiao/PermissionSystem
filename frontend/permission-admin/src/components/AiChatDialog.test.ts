import { flushPromises, mount } from '@vue/test-utils'
import ElementPlus, { ElSelect } from 'element-plus'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import AiChatDialog from './AiChatDialog.vue'
import {
  getAiConversation,
  getAiConversations,
  sendAiMessage,
  type AiConversationDetail,
  type AiRun,
  type AiStructuredResult,
} from '../api/ai'

vi.mock('../stores/auth', () => ({ useAuthStore: () => ({ hasPermission: () => true }) }))
vi.mock('../utils/signalr-lite', () => ({
  startAiRunConnection: vi.fn(async () => ({ stop: vi.fn() })),
}))
vi.mock('../api/ai', () => ({
  getAiConversations: vi.fn(),
  getAiConversation: vi.fn(),
  sendAiMessage: vi.fn(),
  createAiConversation: vi.fn(),
  deleteAiConversation: vi.fn(),
  cancelAiRun: vi.fn(),
  retryAiRun: vi.fn(),
  saveMyAiFeedback: vi.fn(),
}))

function detail(results = true): AiConversationDetail {
  const structured: AiStructuredResult = {
    runId: 'run-1',
    invocationId: 'call-1',
    type: 'table',
    version: 1,
    toolCode: 'permission.users.search',
    toolVersion: '1.0',
    queriedAt: '2026-10-06T08:00:00Z',
    evaluationBasis: '当前授权范围',
    isTruncated: false,
    limitations: [],
    context: { version: 1, parameters: { keyword: 'alice', limit: 20 } },
    table: { totalCount: 0, displayedRowCount: 0, items: [] },
    citation: {
      sourceSystem: 'PermissionSystem',
      toolCode: 'permission.users.search',
      toolVersion: '1.0',
      queryParametersDigest: 'digest',
      queriedAt: '2026-10-06T08:00:00Z',
      rowCount: 0,
    },
  }
  return {
    id: 'conversation-1',
    title: '测试会话',
    status: 1,
    lastMessageAt: '2026-10-06T08:00:00Z',
    agentCode: 'agent',
    agentVersion: '2.2',
    messages: [
      {
        id: 'message-1',
        role: 3,
        content: '旧文字继续可见',
        sequence: 1,
        modelGenerated: true,
        createdAt: '2026-10-06T08:00:00Z',
        runId: 'run-1',
      },
    ],
    documentDrafts: [],
    permissionDiagnostics: [],
    structuredResults: results ? [structured] : [],
    structuredResultsUnavailable: !results,
  }
}

function dialog() {
  return mount(AiChatDialog, {
    global: {
      plugins: [ElementPlus],
      stubs: { ElDialog: { template: '<div><slot /><slot name="footer" /></div>' } },
    },
  })
}

async function openDialog() {
  const wrapper = dialog()
  await (wrapper.vm as unknown as { open: () => Promise<void> }).open()
  await flushPromises()
  return wrapper
}

async function submit(wrapper: ReturnType<typeof dialog>, text: string) {
  await wrapper.find('textarea').setValue(text)
  await wrapper.find('textarea').trigger('keydown', { key: 'Enter', ctrlKey: true })
  await flushPromises()
}

describe('AiChatDialog 结构化追问', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getAiConversations).mockResolvedValue({
      items: [detail()],
      totalCount: 1,
      pageIndex: 1,
      pageSize: 50,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    } as Awaited<ReturnType<typeof getAiConversations>>)
    vi.mocked(getAiConversation).mockResolvedValue(detail())
    vi.mocked(sendAiMessage).mockResolvedValue({
      id: 'run-next',
      status: 3,
      citations: [],
    } as unknown as AiRun)
  })

  it('重载结果与旧文本，选择卡片后发送精确引用，撤权重载后清除引用', async () => {
    vi.mocked(getAiConversation).mockResolvedValueOnce(detail()).mockResolvedValue(detail(false))
    const wrapper = await openDialog()
    expect(wrapper.text()).toContain('旧文字继续可见')
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '继续追问此结果')!
      .trigger('click')
    expect(wrapper.text()).toContain('追问：用户查询')
    await submit(wrapper, '只看停用用户')
    expect(sendAiMessage).toHaveBeenLastCalledWith(
      'conversation-1',
      '只看停用用户',
      { runId: 'run-1', invocationId: 'call-1' },
      undefined,
    )
    expect(wrapper.text()).toContain('部分历史结果已过期或当前不可读取')
    expect(wrapper.find('[aria-label="当前追问对象"]').exists()).toBe(false)
    await submit(wrapper, '重新明确查询条件')
    expect(sendAiMessage).toHaveBeenLastCalledWith(
      'conversation-1',
      '重新明确查询条件',
      undefined,
      undefined,
    )
    wrapper.unmount()
  })

  it('清除选择后保持普通纯文本请求兼容', async () => {
    const wrapper = await openDialog()
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '继续追问此结果')!
      .trigger('click')
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '清除选择')!
      .trigger('click')
    await submit(wrapper, '新查询')
    expect(sendAiMessage).toHaveBeenLastCalledWith('conversation-1', '新查询', undefined, undefined)
    wrapper.unmount()
  })

  it('自然月时区没有默认值，用户明确选择后随请求发送', async () => {
    const wrapper = await openDialog()
    const timezone = wrapper.findComponent(ElSelect)
    expect(timezone.props('modelValue')).toBeUndefined()
    timezone.vm.$emit('update:modelValue', 480)
    await flushPromises()
    await submit(wrapper, '再看上个月')
    expect(sendAiMessage).toHaveBeenLastCalledWith('conversation-1', '再看上个月', undefined, 480)
    wrapper.unmount()
  })
})
