import { flushPromises, mount } from '@vue/test-utils'
import ElementPlus, { ElMessageBox, ElSelect } from 'element-plus'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import AiChatDialog from './AiChatDialog.vue'
import { getAiScenarioOptions } from '../api/aiScenario'
import {
  createAiConversation,
  getAiConversation,
  getAiConversations,
  getAiRun,
  cancelAiRun,
  getAiSubmission,
  sendAiMessage,
  type AiConversationDetail,
  type AiRun,
  type AiStructuredResult,
} from '../api/ai'
import { startAiRunConnection } from '../utils/signalr-lite'

const auth = vi.hoisted(() => ({ hasPermission: vi.fn(() => true) }))
vi.mock('../stores/auth', () => ({ useAuthStore: () => auth }))
vi.mock('../utils/signalr-lite', () => ({
  startAiRunConnection: vi.fn(async () => ({ stop: vi.fn() })),
}))
vi.mock('../api/aiScenario', () => ({ getAiScenarioOptions: vi.fn(async () => []) }))
vi.mock('../api/ai', () => ({
  getAiConversations: vi.fn(),
  getAiConversation: vi.fn(),
  getAiRun: vi.fn(),
  getAiSubmission: vi.fn(),
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
      stubs: {
        ElDialog: { template: '<div><slot name="header" /><slot /><slot name="footer" /></div>' },
      },
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
    auth.hasPermission.mockImplementation(() => true)
    vi.mocked(getAiScenarioOptions).mockResolvedValue([])
    vi.mocked(createAiConversation).mockResolvedValue(detail(false))
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
      expect.any(String),
    )
    expect(wrapper.text()).toContain('部分历史结果已过期或当前不可读取')
    expect(wrapper.find('[aria-label="当前追问对象"]').exists()).toBe(false)
    await submit(wrapper, '重新明确查询条件')
    expect(sendAiMessage).toHaveBeenLastCalledWith(
      'conversation-1',
      '重新明确查询条件',
      undefined,
      undefined,
      expect.any(String),
    )
    wrapper.unmount()
  })

  it('按服务端逐草稿能力展示动作，不使用聊天层的 Demo 全局权限', async () => {
    auth.hasPermission.mockImplementation(
      (permission?: string) => permission !== 'demo-business-order:create',
    )
    const conversation = detail()
    conversation.documentDrafts = [
      {
        id: 'draft',
        conversationId: conversation.id,
        runId: 'run-1',
        businessType: 'DemoBusinessOrder',
        handlerVersion: '1.0',
        status: 3,
        draftVersion: 1,
        payload: { title: '已授权草稿' },
        payloadHash: 'A'.repeat(64),
        validationErrors: [],
        expiresAt: '2026-10-07T10:00:00Z',
        concurrencyToken: 'AQID',
        canEdit: true,
        canCancel: true,
        canConfirm: true,
        canExecute: true,
      },
    ]
    vi.mocked(getAiConversation).mockResolvedValue(conversation)
    const wrapper = await openDialog()
    expect(wrapper.text()).toContain('已授权草稿')
    expect(wrapper.text()).toContain('创建正式单据')
    expect(auth.hasPermission).not.toHaveBeenCalledWith('demo-business-order:create')
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
    expect(sendAiMessage).toHaveBeenLastCalledWith(
      'conversation-1',
      '新查询',
      undefined,
      undefined,
      expect.any(String),
    )
    wrapper.unmount()
  })

  it('新会话显式携带场景，旧请求保留兼容路径', async () => {
    vi.mocked(getAiScenarioOptions).mockResolvedValue([
      {
        id: 'scene-1',
        code: 'permission-assistant',
        name: '权限助手',
        versionId: 'v1',
        versionNumber: 1,
      },
    ])
    const wrapper = await openDialog()
    const scene = wrapper
      .findAllComponents(ElSelect)
      .find((el) => el.props('ariaLabel') === '新会话场景')!
    expect(scene.props('modelValue')).toBe('')
    scene.vm.$emit('update:modelValue', 'scene-1')
    await flushPromises()
    await wrapper.find('button[aria-label="新建会话"]').trigger('click')
    await flushPromises()
    expect(createAiConversation).toHaveBeenCalledWith(undefined, 'scene-1')
    wrapper.unmount()
  })

  it('自然月时区没有默认值，用户明确选择后随请求发送', async () => {
    const wrapper = await openDialog()
    const timezone = wrapper
      .findAllComponents(ElSelect)
      .find((el) => el.props('ariaLabel') === '自然月查询时区')!
    expect(timezone.props('modelValue')).toBeUndefined()
    timezone.vm.$emit('update:modelValue', 480)
    await flushPromises()
    await submit(wrapper, '再看上个月')
    expect(sendAiMessage).toHaveBeenLastCalledWith(
      'conversation-1',
      '再看上个月',
      undefined,
      480,
      expect.any(String),
    )
    wrapper.unmount()
  })

  it('升级明确新建当前版本会话，清除旧结果引用和查询时区', async () => {
    const pinned = {
      ...detail(),
      scenarioId: 'scene-1',
      scenarioVersionId: 'v1',
      agentVersion: '1',
    }
    vi.mocked(getAiConversation).mockResolvedValue(pinned)
    vi.mocked(getAiScenarioOptions).mockResolvedValue([
      {
        id: 'scene-1',
        code: 'permission-assistant',
        name: '权限助手',
        versionId: 'v2',
        versionNumber: 2,
      },
    ])
    vi.mocked(createAiConversation).mockResolvedValue({
      ...detail(false),
      id: 'new-conversation',
      scenarioId: 'scene-1',
      scenarioVersionId: 'v2',
      agentVersion: '2',
      messages: [],
    })
    const confirmation = vi
      .spyOn(ElMessageBox, 'confirm')
      .mockResolvedValue('confirm' as Awaited<ReturnType<typeof ElMessageBox.confirm>>)
    const wrapper = await openDialog()
    await wrapper
      .findAll('button')
      .find((b) => b.text() === '继续追问此结果')!
      .trigger('click')
    const timezone = wrapper
      .findAllComponents(ElSelect)
      .find((el) => el.props('ariaLabel') === '自然月查询时区')!
    timezone.vm.$emit('update:modelValue', 480)
    await flushPromises()
    await wrapper
      .findAll('button')
      .find((b) => b.text() === '以当前发布版本新建会话')!
      .trigger('click')
    await flushPromises()
    expect(confirmation).toHaveBeenCalled()
    expect(createAiConversation).toHaveBeenCalledWith(undefined, 'scene-1')
    expect(timezone.props('modelValue')).toBeUndefined()
    expect(wrapper.find('[aria-label="当前追问对象"]').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('旧文字继续可见')
    await submit(wrapper, '新会话明确查询')
    expect(sendAiMessage).toHaveBeenLastCalledWith(
      'new-conversation',
      '新会话明确查询',
      undefined,
      undefined,
      expect.any(String),
    )
    wrapper.unmount()
    confirmation.mockRestore()
  })

  it('打开已有后台 Run 恢复状态并禁止第二次发送，终态消息刷新且旧事件不能回退状态', async () => {
    const run = {
      id: 'background',
      conversationId: 'conversation-1',
      status: 2,
      progressVersion: 2,
      citations: [],
      toolProgress: [],
    } as unknown as AiRun
    vi.mocked(getAiConversation).mockResolvedValue({ ...detail(), latestRun: run })
    const wrapper = await openDialog()
    expect(wrapper.text()).toContain('正在查询')
    await submit(wrapper, '不能重复提交')
    expect(sendAiMessage).not.toHaveBeenCalled()
    const finished = { ...run, status: 3, progressVersion: 4 } as AiRun
    vi.mocked(getAiRun).mockResolvedValue(finished)
    const updated = detail()
    updated.messages.push({
      id: 'final-answer',
      role: 3,
      content: '后台最终回答',
      sequence: 2,
      modelGenerated: true,
      createdAt: '2026-10-07T01:00:00Z',
    })
    vi.mocked(getAiConversation).mockResolvedValue({ ...updated, latestRun: finished })
    const onEvent = vi.mocked(startAiRunConnection).mock.calls[0]![0]
    onEvent({
      runId: 'background',
      conversationId: 'conversation-1',
      status: 3,
      progressVersion: 4,
    })
    await flushPromises()
    onEvent({
      runId: 'background',
      conversationId: 'conversation-1',
      status: 2,
      progressVersion: 2,
    })
    expect(wrapper.text()).toContain('后台最终回答')
    expect(wrapper.text()).toContain('已完成')
    expect(wrapper.text()).not.toContain('正在查询')
    wrapper.unmount()
  })

  it('终态详情暂时失败后继续轮询加载最终回答', async () => {
    const run = {
      id: 'background',
      conversationId: 'conversation-1',
      status: 2,
      progressVersion: 2,
      citations: [],
    } as unknown as AiRun
    vi.mocked(getAiConversation).mockResolvedValue({ ...detail(), latestRun: run })
    const wrapper = await openDialog()
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    try {
      const finished = { ...run, status: 3, progressVersion: 4 } as AiRun
      vi.mocked(getAiRun).mockResolvedValue(finished)
      const updated = detail()
      updated.messages[0]!.content = '恢复后的最终回答'
      vi.mocked(getAiConversation)
        .mockRejectedValueOnce(new TypeError('network unavailable'))
        .mockResolvedValue({ ...updated, latestRun: finished })
      vi.mocked(startAiRunConnection).mock.calls[0]![0]({
        runId: run.id,
        conversationId: run.conversationId,
        status: 3,
        progressVersion: 4,
      })
      await flushPromises()
      expect(wrapper.text()).not.toContain('恢复后的最终回答')
      await vi.advanceTimersByTimeAsync(6000)
      await flushPromises()
      expect(wrapper.text()).toContain('恢复后的最终回答')
    } finally {
      wrapper.unmount()
      vi.useRealTimers()
    }
  })

  it('取消请求等待服务端终态，不把请求成功伪装成已取消', async () => {
    const run = {
      id: 'background',
      conversationId: 'conversation-1',
      status: 2,
      progressVersion: 2,
      citations: [],
    } as unknown as AiRun
    vi.mocked(getAiConversation).mockResolvedValue({ ...detail(), latestRun: run })
    vi.mocked(getAiRun).mockResolvedValue({
      ...run,
      progressVersion: 3,
      cancellationRequestedAt: '2026-10-07T01:00:00Z',
    })
    const wrapper = await openDialog()
    await wrapper
      .findAll('button')
      .find((b) => b.text() === '取消')!
      .trigger('click')
    await flushPromises()
    expect(cancelAiRun).toHaveBeenCalledWith('background')
    expect(wrapper.text()).toContain('取消请求已提交')
    expect(wrapper.text()).not.toContain('已取消')
    wrapper.unmount()
  })

  it('提交响应丢失先读取提交引用，人工恢复时复用相同键和参数', async () => {
    vi.mocked(sendAiMessage)
      .mockRejectedValueOnce(new TypeError('network unavailable'))
      .mockResolvedValue({
        id: 'recovered',
        conversationId: 'conversation-1',
        status: 1,
        progressVersion: 1,
        citations: [],
      } as unknown as AiRun)
    vi.mocked(getAiSubmission).mockRejectedValue({ response: { status: 404 } })
    const wrapper = await openDialog()
    await submit(wrapper, '保留同一提交')
    const key = vi.mocked(sendAiMessage).mock.calls[0]![4]
    expect(sendAiMessage).toHaveBeenCalledTimes(1)
    await wrapper
      .findAll('button')
      .find((b) => b.text() === '恢复提交状态')!
      .trigger('click')
    await flushPromises()
    expect(getAiSubmission).toHaveBeenCalledWith('conversation-1', key)
    expect(sendAiMessage).toHaveBeenLastCalledWith(
      'conversation-1',
      '保留同一提交',
      undefined,
      undefined,
      key,
    )
    expect(wrapper.text()).toContain('等待执行')
    wrapper.unmount()
  })
})
