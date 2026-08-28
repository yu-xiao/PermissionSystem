import ElementPlus from 'element-plus'
import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import AiDocumentDraftCard from './AiDocumentDraftCard.vue'
import type { AiDocumentDraft } from '../api/ai'

vi.mock('../api/ai', async (importOriginal) => {
  const original = await importOriginal<typeof import('../api/ai')>()
  return {
    ...original,
    updateAiDocumentDraft: vi.fn(),
    cancelAiDocumentDraft: vi.fn(),
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
  it('renders normalized preview without a formal-order execution command', () => {
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
