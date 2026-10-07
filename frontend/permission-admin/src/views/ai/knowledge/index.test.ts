import { flushPromises, mount } from '@vue/test-utils'
import ElementPlus, { ElFormItem } from 'element-plus'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import KnowledgePage from './index.vue'
import {
  createKnowledgeDocument,
  getKnowledgeDocuments,
  searchKnowledge,
} from '../../../api/aiKnowledge'
import { getRoles } from '../../../api/roles'

const auth = vi.hoisted(() => ({
  effectiveTenantId: 'tenant',
  hasPermission: vi.fn<(code: string) => boolean>(),
}))
vi.mock('../../../stores/auth', () => ({ useAuthStore: () => auth }))
vi.mock('../../../api/roles', () => ({ getRoles: vi.fn() }))
vi.mock('../../../api/aiKnowledge', () => ({
  createKnowledgeDocument: vi.fn(),
  getKnowledgeDocuments: vi.fn(),
  searchKnowledge: vi.fn(),
  setKnowledgeAccess: vi.fn(),
  uploadKnowledgeVersion: vi.fn(),
  deleteKnowledgeDocument: vi.fn(),
  publishKnowledgeVersion: vi.fn(),
  previewKnowledgeVersion: vi.fn(),
  getKnowledgeSource: vi.fn(),
}))
describe('合成知识管理', () => {
  let wrapper: ReturnType<typeof mount>
  beforeEach(() => {
    vi.clearAllMocks()
    auth.hasPermission.mockImplementation((code) => code !== 'system:role:view')
    vi.mocked(getKnowledgeDocuments).mockResolvedValue({
      items: [],
      pageIndex: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false,
    })
  })
  afterEach(() => wrapper?.unmount())
  async function page() {
    wrapper = mount(KnowledgePage, {
      global: {
        plugins: [ElementPlus],
        stubs: {
          ElDialog: { template: '<div><slot /><slot name="footer" /></div>' },
        },
      },
    })
    await flushPromises()
  }
  async function click(text: string) {
    await wrapper
      .findAll('button')
      .find((b) => b.text() === text)!
      .trigger('click')
    await flushPromises()
  }
  it('管理权限不显示正文查询入口，也不隐式枚举角色', async () => {
    auth.hasPermission.mockImplementation((code) =>
      ['ai:knowledge:view', 'ai:knowledge:manage'].includes(code),
    )
    await page()
    expect(getKnowledgeDocuments).toHaveBeenCalledOnce()
    expect(wrapper.find('[aria-label="受控知识查询"]').exists()).toBe(false)
    await click('创建合成文档')
    expect(getRoles).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('角色枚举需要角色查看权限')
  })
  it('未填写测试标记不能创建；完成标记后仅提交空 ACL 合成草稿', async () => {
    await page()
    await click('创建合成文档')
    await click('保存')
    expect(createKnowledgeDocument).not.toHaveBeenCalled()
    for (const [label, value] of [
      ['文档标题', '合成制度'],
      ['测试 Owner', '测试所有者'],
      ['测试许可说明', '仅合成测试'],
    ]) {
      const field = wrapper.findAllComponents(ElFormItem).find((f) => f.props('label') === label)!
      await field.get('input').setValue(value)
    }
    await wrapper.get('input[type="checkbox"]').setValue(true)
    await click('保存')
    expect(createKnowledgeDocument).toHaveBeenCalledWith({
      title: '合成制度',
      owner: '测试所有者',
      license: '仅合成测试',
      synthetic: true,
      roleIds: [],
    })
  })
  it('查询失败时清除上次授权正文，不能复用缓存结果', async () => {
    vi.mocked(searchKnowledge)
      .mockResolvedValueOnce({
        items: [
          {
            reference: {
              documentId: 'doc',
              versionId: 'v1',
              chunkId: 'chunk',
              contentHash: 'hash',
            },
            title: '合成',
            versionNumber: 1,
            content: '之前的合成正文',
            startLine: 1,
            endLine: 1,
            validFrom: '2026-10-01',
            validUntil: '2026-11-01',
          },
        ],
        queriedAt: '2026-10-07',
        isTruncated: false,
        limitation: '仅直接匹配',
      })
      .mockRejectedValueOnce(new Error('unavailable'))
    await page()
    const field = wrapper
      .findAllComponents(ElFormItem)
      .find((f) => f.props('label') === '直接匹配关键词')!
    await field.get('input').setValue('合成')
    await field.element
      .closest('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
    await flushPromises()
    expect(wrapper.text()).toContain('之前的合成正文')
    await field.element
      .closest('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
    await flushPromises()
    expect(wrapper.text()).not.toContain('之前的合成正文')
    expect(wrapper.text()).toContain('查询不可用')
  })
})
