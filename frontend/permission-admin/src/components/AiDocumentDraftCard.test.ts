import ElementPlus from 'element-plus'
import { ElMessageBox } from 'element-plus'
import { flushPromises, mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import AiDocumentDraftCard from './AiDocumentDraftCard.vue'
import { confirmAiDocumentDraft, executeAiDocumentDraft } from '../api/ai'
import type { AiDocumentDraft } from '../api/ai'

vi.mock('../api/ai', async (importOriginal) => {
  const original = await importOriginal<typeof import('../api/ai')>()
  return {
    ...original,
    updateAiDocumentDraft: vi.fn(),
    cancelAiDocumentDraft: vi.fn(),
    confirmAiDocumentDraft: vi.fn(),
    executeAiDocumentDraft: vi.fn(),
  }
})

function createDraft(overrides: Partial<AiDocumentDraft> = {}): AiDocumentDraft {
  return {
    id: 'draft-1',
    conversationId: 'conversation-1',
    runId: 'run-1',
    businessType: 'DemoBusinessOrder',
    handlerVersion: '1.0',
    status: 3,
    draftVersion: 1,
    payload: {
      title: 'August order',
      customerName: 'Contoso',
      amount: 123.45,
      departmentCode: 'SALES',
      departmentName: 'Sales',
    },
    payloadHash: 'A'.repeat(64),
    validationErrors: [],
    expiresAt: '2026-08-28T08:30:00Z',
    lastValidatedAt: '2026-08-28T08:00:00Z',
    concurrencyToken: 'AQID',
    ...overrides,
  }
}

describe('AiDocumentDraftCard', () => {
  beforeEach(() => vi.clearAllMocks())

  it('renders normalized preview and hides execution without permission', () => {
    const wrapper = mount(AiDocumentDraftCard, {
      props: { draft: createDraft() },
      global: { plugins: [ElementPlus] },
    })

    expect(wrapper.text()).toContain('校验通过')
    expect(wrapper.text()).toContain('August order')
    expect(wrapper.text()).toContain('Contoso')
    expect(wrapper.text()).toContain('Sales')
    expect(wrapper.text()).not.toContain('创建正式单据')
    expect(wrapper.text()).not.toContain('确认提交')
  })

  it('shows the formal-order command only for a validated authorized draft', () => {
    const wrapper = mount(AiDocumentDraftCard, {
      props: { draft: createDraft(), canExecute: true },
      global: { plugins: [ElementPlus] },
    })

    expect(wrapper.text()).toContain('创建正式单据')
  })

  it('requires explicit and sensitive confirmation before executing the draft', async () => {
    vi.spyOn(ElMessageBox, 'confirm').mockResolvedValue({} as never)
    vi.mocked(confirmAiDocumentDraft).mockResolvedValue({
      id: 'confirmation-1',
      draftId: 'draft-1',
      draftVersion: 1,
      confirmationVersion: 1,
      payloadHash: 'A'.repeat(64),
      handlerVersion: '1.0',
      confirmedAt: '2026-08-28T08:00:00Z',
      expiresAt: '2026-08-28T08:02:00Z',
      concurrencyToken: 'BAUG',
    })
    vi.mocked(executeAiDocumentDraft).mockResolvedValue({
      executionId: 'execution-1',
      draftId: 'draft-1',
      runId: 'run-1',
      businessEntityId: 'order-1',
      businessNo: 'DBO-0001',
      businessStatus: 'Draft',
      linkUrl: '/demo/business-order?keyword=DBO-0001',
      traceId: 'trace-1',
      completedAt: '2026-08-28T08:01:00Z',
      draftStatus: 6,
      draftConcurrencyToken: 'Bw==',
    })
    const sensitiveDialog = defineComponent({
      name: 'SensitiveVerificationDialog',
      setup(_, { expose }) {
        expose({ open: vi.fn().mockResolvedValue('step-up-ticket') })
        return () => h('div')
      },
    })
    const wrapper = mount(AiDocumentDraftCard, {
      props: { draft: createDraft(), canExecute: true },
      global: {
        plugins: [ElementPlus],
        stubs: { SensitiveVerificationDialog: sensitiveDialog },
      },
    })

    const executeButton = wrapper
      .findAll('button')
      .find((button) => button.text().includes('创建正式单据'))
    await executeButton?.trigger('click')
    await flushPromises()

    expect(confirmAiDocumentDraft).toHaveBeenCalledWith('draft-1', 'AQID', 'step-up-ticket')
    expect(executeAiDocumentDraft).toHaveBeenCalledOnce()
    expect(wrapper.text()).toContain('DBO-0001')
    expect(wrapper.emitted('updated')?.[0]?.[0]).toMatchObject({ status: 6 })
  })

  it('shows field validation errors for an incomplete draft', () => {
    const wrapper = mount(AiDocumentDraftCard, {
      props: {
        draft: createDraft({
          status: 1,
          payload: {},
          validationErrors: [
            { field: 'Title', code: 'required', message: 'Title is required.', candidates: [] },
          ],
        }),
      },
      global: { plugins: [ElementPlus] },
    })

    expect(wrapper.text()).toContain('待补充')
    expect(wrapper.text()).toContain('Title is required.')
  })
})
