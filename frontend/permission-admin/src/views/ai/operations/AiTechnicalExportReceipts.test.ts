import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import ElementPlus, { ElDatePicker } from 'element-plus'
import { defineComponent, h, KeepAlive, reactive, ref } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import Receipts from './AiTechnicalExportReceipts.vue'
import {
  getAiTechnicalExportReceipt,
  getAiTechnicalExportReceipts,
  type AiTechnicalExportReceiptDetail,
  type AiTechnicalExportReceiptPage,
} from '../../../api/ai'
import {
  hashTechnicalExportFile,
  supportsLocalExportIntegrity,
} from '../../../utils/aiTechnicalExportIntegrity'

let auth: {
  effectiveTenantId: string
  currentUser: { userId: string; permissionCodes: string[] }
  hasPermission: (code: string) => boolean
}
vi.mock('../../../stores/auth', () => ({ useAuthStore: () => auth }))
vi.mock('../../../api/ai', () => ({
  getAiTechnicalExportReceipt: vi.fn(),
  getAiTechnicalExportReceipts: vi.fn(),
}))
vi.mock('../../../utils/aiTechnicalExportIntegrity', () => ({
  hashTechnicalExportFile: vi.fn(),
  supportsLocalExportIntegrity: vi.fn(),
  maxTechnicalExportBytes: 16 * 1024 * 1024,
}))
const window = {
  tenantId: 'tenant-1',
  scope: 'CurrentCaller' as const,
  receiptFrom: '2026-10-01T00:00:00Z',
  receiptTo: '2026-10-02T00:00:00Z',
  observedFrom: '2026-10-02T00:00:00Z',
  observedTo: '2026-10-02T00:00:01Z',
  matchedRecordCount: 2,
  unreadableRecordCount: 0,
  windowInterpretable: true,
}
function detail(): AiTechnicalExportReceiptDetail {
  return {
    ...window,
    export: {
      exportId: 'export-1',
      firstRecordedAt: window.receiptFrom,
      lastRecordedAt: window.receiptFrom,
      requestedCount: 1,
      preparedCount: 1,
      failedCount: 1,
      canVerify: true,
      verificationReason: 'Ready',
    },
    receipts: [
      {
        receiptId: 'prepared-1',
        recordedAt: window.receiptFrom,
        schemaVersion: 1,
        outcome: 'Prepared',
        from: window.receiptFrom,
        to: window.receiptTo,
        observedFrom: window.observedFrom,
        observedTo: window.observedTo,
        runCount: 1,
        usageCount: 2,
        bytes: 100,
        fileSha256: 'A'.repeat(64),
        failureCode: null,
      },
      {
        receiptId: 'failed-1',
        recordedAt: window.receiptFrom,
        schemaVersion: 1,
        outcome: 'Failed',
        from: window.receiptFrom,
        to: window.receiptTo,
        observedFrom: window.observedFrom,
        observedTo: null,
        runCount: null,
        usageCount: null,
        bytes: null,
        fileSha256: null,
        failureCode: 'Cancelled',
      },
    ],
  }
}
function page(): AiTechnicalExportReceiptPage {
  return {
    ...window,
    exports: {
      items: [detail().export],
      pageIndex: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    },
  }
}
function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((done) => {
    resolve = done
  })
  return { promise, resolve }
}
let wrapper: VueWrapper
async function open() {
  wrapper = mount(Receipts, { props: { modelValue: true }, global: { plugins: [ElementPlus] } })
  await flushPromises()
}
async function button(text: string) {
  await wrapper
    .findAll('button')
    .find((b) => b.text() === text)!
    .trigger('click')
  await flushPromises()
}
async function select() {
  await button('查看凭据')
}
async function file(size = 100, name = 'untrusted-export-other.json') {
  const input = wrapper.get('input[type=file]')
  Object.defineProperty(input.element, 'files', {
    configurable: true,
    value: [new File([new Uint8Array(size)], name)],
  })
  await input.trigger('change')
  await flushPromises()
}
describe('本人凭据与本地核对', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    auth = reactive({
      effectiveTenantId: 'tenant-1',
      currentUser: {
        userId: 'actor',
        permissionCodes: ['ai:operations:view', 'ai:operations:export'],
      },
      hasPermission(code: string) {
        return this.currentUser.permissionCodes.includes(code)
      },
    })
    vi.mocked(supportsLocalExportIntegrity).mockReturnValue(true)
    vi.mocked(getAiTechnicalExportReceipts).mockResolvedValue(page())
    vi.mocked(getAiTechnicalExportReceipt).mockResolvedValue(detail())
    vi.mocked(hashTechnicalExportFile).mockResolvedValue({ bytes: 100, sha256: 'A'.repeat(64) })
  })
  afterEach(() => {
    wrapper?.unmount()
    vi.restoreAllMocks()
  })
  it('关闭或只有查看权限时不读取凭据', async () => {
    wrapper = mount(Receipts, { props: { modelValue: false }, global: { plugins: [ElementPlus] } })
    await flushPromises()
    expect(getAiTechnicalExportReceipts).not.toHaveBeenCalled()
    auth.currentUser.permissionCodes = ['ai:operations:view']
    await wrapper.setProps({ modelValue: true })
    await flushPromises()
    expect(getAiTechnicalExportReceipts).not.toHaveBeenCalled()
  })
  it('核对本地原字节后重读同一凭据，不上传且不覆盖失败事实', async () => {
    await open()
    expect(wrapper.text()).toContain('凭据记录时间')
    expect(wrapper.text()).toContain('非完整历史')
    await select()
    expect(getAiTechnicalExportReceipt).toHaveBeenLastCalledWith(
      'export-1',
      { from: window.receiptFrom, to: window.receiptTo },
      expect.any(AbortSignal),
    )
    await file()
    await button('核对本地文件')
    expect(hashTechnicalExportFile).toHaveBeenCalledTimes(1)
    expect(getAiTechnicalExportReceipt).toHaveBeenCalledTimes(2)
    expect(wrapper.text()).toContain('与当前可见准备凭据一致')
    expect(wrapper.text()).toContain('已记录失败')
    expect(wrapper.text()).not.toContain('untrusted-export-other')
    expect(wrapper.text()).not.toContain('下载成功')
  })
  it('不同 Hash 或长度显示不一致', async () => {
    vi.mocked(hashTechnicalExportFile).mockResolvedValue({ bytes: 99, sha256: 'B'.repeat(64) })
    await open()
    await select()
    await file()
    await button('核对本地文件')
    expect(wrapper.text()).toContain('所选文件与准备凭据不一致')
  })
  it.each(['hash', 'deleted', 'window', 'failed-fact'])(
    'Hash 计算后 %s 变化清理文件和结果',
    async (change) => {
      await open()
      await select()
      await file()
      const fresh = detail()
      if (change === 'hash') fresh.receipts[0]!.fileSha256 = 'B'.repeat(64)
      if (change === 'deleted') fresh.export.canVerify = false
      if (change === 'window') fresh.windowInterpretable = false
      if (change === 'failed-fact') fresh.export.failedCount++
      vi.mocked(getAiTechnicalExportReceipt).mockResolvedValueOnce(fresh)
      await button('核对本地文件')
      expect(wrapper.text()).toContain('凭据已变化')
      expect(wrapper.text()).not.toContain('与当前可见准备凭据一致')
      expect(wrapper.find('input[type=file]').exists()).toBe(false)
    },
  )
  it.each(['tenant', 'permission', 'date', 'file', 'close', 'unmount'])(
    '%s 变化丢弃迟到 Hash 并取消请求',
    async (change) => {
      const pending = deferred<{ bytes: number; sha256: string }>()
      vi.mocked(hashTechnicalExportFile).mockReturnValueOnce(pending.promise)
      await open()
      await select()
      await file()
      await button('核对本地文件')
      if (change === 'tenant') auth.effectiveTenantId = 'tenant-2'
      if (change === 'permission') auth.currentUser.permissionCodes = ['ai:operations:view']
      if (change === 'date') wrapper.findComponent(ElDatePicker).vm.$emit('update:modelValue', null)
      if (change === 'file') await file(100, 'replacement.json')
      if (change === 'close') await button('关闭')
      if (change === 'unmount') wrapper.unmount()
      await flushPromises()
      pending.resolve({ bytes: 100, sha256: 'A'.repeat(64) })
      await flushPromises()
      expect(getAiTechnicalExportReceipt).toHaveBeenCalledTimes(1)
      expect(wrapper.text()).not.toContain('与当前可见准备凭据一致')
    },
  )
  it('关闭窗口清除并取消迟到列表，不自动重放核对', async () => {
    const pending = deferred<AiTechnicalExportReceiptPage>()
    vi.mocked(getAiTechnicalExportReceipts).mockReturnValueOnce(pending.promise)
    await open()
    const signal = vi.mocked(getAiTechnicalExportReceipts).mock.calls[0]![1]
    await wrapper.setProps({ modelValue: false })
    await flushPromises()
    expect(signal.aborted).toBe(true)
    pending.resolve(page())
    await flushPromises()
    expect(getAiTechnicalExportReceipt).not.toHaveBeenCalled()
    expect(hashTechnicalExportFile).not.toHaveBeenCalled()
  })
  it('损坏窗口和缺 Web Crypto 不启用核对', async () => {
    const response = detail()
    response.export.canVerify = false
    response.export.verificationReason = 'WindowUnreadable'
    response.windowInterpretable = false
    vi.mocked(getAiTechnicalExportReceipt).mockResolvedValueOnce(response)
    await open()
    await select()
    expect(wrapper.find('input[type=file]').exists()).toBe(false)
    vi.mocked(supportsLocalExportIntegrity).mockReturnValue(false)
    wrapper.unmount()
    await open()
    await select()
    expect(wrapper.text()).toContain('HTTPS 安全环境')
    expect(wrapper.get('input[type=file]').attributes('disabled')).toBeDefined()
  })
  it('空文件或超限在读取前拒绝', async () => {
    await open()
    await select()
    await file(0)
    expect(wrapper.text()).toContain('非空且不超过 16 MiB')
    expect(hashTechnicalExportFile).not.toHaveBeenCalled()
  })
  it('读取失败允许重试并拒绝其他租户的响应', async () => {
    vi.mocked(getAiTechnicalExportReceipts).mockRejectedValueOnce(new Error('synthetic'))
    await open()
    expect(wrapper.text()).toContain('凭据读取失败')
    await button('查询凭据')
    expect(wrapper.text()).toContain('export-1')
    vi.mocked(getAiTechnicalExportReceipt).mockResolvedValueOnce({
      ...detail(),
      tenantId: 'tenant-2',
    })
    await select()
    expect(wrapper.text()).toContain('凭据详情不可用')
    expect(wrapper.find('input[type=file]').exists()).toBe(false)
  })
  it('KeepAlive 停用清理并丢弃迟到 Hash', async () => {
    const visible = ref(true)
    const opened = ref(true)
    wrapper = mount(
      defineComponent({
        setup: () => () =>
          h(KeepAlive, () =>
            visible.value
              ? h(Receipts, {
                  modelValue: opened.value,
                  'onUpdate:modelValue': (value: boolean) => {
                    opened.value = value
                  },
                })
              : null,
          ),
      }),
      { global: { plugins: [ElementPlus] } },
    )
    await flushPromises()
    await select()
    await file()
    const pending = deferred<{ bytes: number; sha256: string }>()
    vi.mocked(hashTechnicalExportFile).mockReturnValueOnce(pending.promise)
    await button('核对本地文件')
    visible.value = false
    await flushPromises()
    pending.resolve({ bytes: 100, sha256: 'A'.repeat(64) })
    visible.value = true
    await flushPromises()
    expect(opened.value).toBe(false)
    expect(getAiTechnicalExportReceipt).toHaveBeenCalledTimes(1)
  })
})
