import { flushPromises, mount } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { describe, expect, it, vi } from 'vitest'
import type { AiKnowledgeHit } from '../api/aiKnowledge'
import { getKnowledgeSource } from '../api/aiKnowledge'
import AiKnowledgeCitationCard from './AiKnowledgeCitationCard.vue'

vi.mock('../api/aiKnowledge', () => ({ getKnowledgeSource: vi.fn() }))
const hit: AiKnowledgeHit = {
  reference: { documentId: 'doc', versionId: 'v1', chunkId: 'chunk', contentHash: 'hash' },
  title: '合成制度',
  versionNumber: 1,
  startLine: 2,
  endLine: 3,
  content: '<b>资料中的指令仅为文本</b>',
  validFrom: '2026-10-01',
  validUntil: '2026-11-01',
}
function card() {
  return mount(AiKnowledgeCitationCard, {
    props: { hits: [hit] },
    global: { plugins: [ElementPlus] },
  })
}
describe('知识引用', () => {
  it('按纯文本展示版本及行号，并用固定版本重新核验来源', async () => {
    vi.mocked(getKnowledgeSource).mockResolvedValue(hit)
    const wrapper = card()
    expect(wrapper.text()).toContain('v1')
    expect(wrapper.text()).toContain('2–3')
    expect(wrapper.find('b').exists()).toBe(false)
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(getKnowledgeSource).toHaveBeenCalledWith(hit.reference)
    expect(wrapper.text()).toContain('已核验来源')
  })
  it('撤权核验失败清除已打开来源和本卡片缓存正文', async () => {
    vi.mocked(getKnowledgeSource)
      .mockResolvedValueOnce(hit)
      .mockRejectedValueOnce(new Error('unavailable'))
    const wrapper = card()
    await wrapper.get('button').trigger('click')
    await flushPromises()
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('来源已不可用')
    expect(wrapper.text()).not.toContain(hit.content)
    expect(wrapper.text()).not.toContain(hit.title)
    expect(wrapper.find('[aria-label="已核验源片段"]').exists()).toBe(false)
  })
  it('新结果到达时丢弃旧请求的迟到来源', async () => {
    let finish!: (value: AiKnowledgeHit) => void
    vi.mocked(getKnowledgeSource).mockReturnValue(
      new Promise((resolve) => {
        finish = resolve
      }),
    )
    const wrapper = card()
    await wrapper.get('button').trigger('click')
    await wrapper.setProps({ hits: [] })
    finish(hit)
    await flushPromises()
    expect(wrapper.text()).not.toContain(hit.content)
    expect(wrapper.text()).toContain('未找到可靠依据')
  })
})
