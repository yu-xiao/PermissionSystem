import ElementPlus, { ElFormItem } from 'element-plus'
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import ProviderPage from './index.vue'
import {
  getAiProvider,
  getAiProviders,
  updateAiProvider,
  type AiProviderDetail,
} from '../../../api/ai'

vi.mock('../../../api/ai', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../../api/ai')>()),
  getAiProvider: vi.fn(),
  getAiProviders: vi.fn(),
  updateAiProvider: vi.fn(),
}))
vi.mock('../../../stores/auth', () => ({
  useAuthStore: () => ({ effectiveTenantId: 'tenant-1', hasPermission: () => true }),
}))

const provider: AiProviderDetail = {
  id: 'provider-1',
  tenantId: 'tenant-1',
  providerCode: 'primary',
  providerName: 'Primary',
  providerType: 1,
  baseUrl: 'https://api.example.test',
  modelName: 'test-model',
  isDefault: true,
  isEnabled: true,
  supportsTools: true,
  supportsJsonSchema: false,
  complianceConfirmedAt: '2026-09-24T00:00:00Z',
  createdAt: '2026-09-24T00:00:00Z',
  concurrencyToken: 'AQID',
  chatCompletionsPath: 'v1/chat/completions',
  apiKey: '********',
  hasApiKey: true,
  timeoutSeconds: 30,
  allowInsecureHttp: false,
  allowPrivateNetwork: false,
  allowedHosts: ['api.example.test'],
}

describe('AI provider endpoint changes', () => {
  let wrapper: ReturnType<typeof mount>

  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getAiProviders).mockResolvedValue({
      items: [provider],
      totalCount: 1,
      pageIndex: 1,
      pageSize: 10,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    })
    vi.mocked(getAiProvider).mockResolvedValue(provider)
    vi.mocked(updateAiProvider).mockResolvedValue(provider)
  })
  afterEach(() => wrapper?.unmount())

  async function openEditor() {
    wrapper = mount(ProviderPage, {
      global: {
        plugins: [ElementPlus],
        directives: { permission: () => undefined },
        stubs: {
          ElDialog: { template: '<div><slot /><slot name="footer" /></div>' },
          ElDropdown: { template: '<div><slot /></div>' },
        },
      },
    })
    await flushPromises()
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '详情')!
      .trigger('click')
    await flushPromises()
  }

  function field(label: string) {
    return wrapper
      .findAllComponents(ElFormItem)
      .find((item) => item.props('label') === label)!
      .find('input')
  }

  async function save() {
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '保存')!
      .trigger('click')
    await flushPromises()
  }

  it('keeps the key blank and permits ordinary edits without replacing it', async () => {
    await openEditor()
    expect((field('API Key').element as HTMLInputElement).value).toBe('')
    await field('名称').setValue('Updated')
    await save()
    expect(updateAiProvider).toHaveBeenCalledWith(
      'provider-1',
      expect.objectContaining({
        providerName: 'Updated',
        apiKey: '',
        concurrencyToken: 'AQID',
      }),
    )
  })

  it.each([
    ['BaseUrl', 'https://replacement.example.test'],
    ['接口路径', 'v2/chat/completions'],
  ])('requires an explicit key when %s changes', async (label, value) => {
    await openEditor()
    await field(label).setValue(value)
    expect(wrapper.text()).toContain('保存后原合规确认将失效')
    await save()
    expect(updateAiProvider).not.toHaveBeenCalled()
    await field('API Key').setValue('********')
    await save()
    expect(updateAiProvider).not.toHaveBeenCalled()
    await field('API Key').setValue('replacement-test-key')
    await save()
    expect(updateAiProvider).toHaveBeenCalledOnce()
    expect(updateAiProvider).toHaveBeenCalledWith(
      'provider-1',
      expect.objectContaining({
        apiKey: 'replacement-test-key',
      }),
    )
  })
})
