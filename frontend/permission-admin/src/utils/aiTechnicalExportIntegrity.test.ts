// @vitest-environment node

import { afterAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { hashTechnicalExportFile, maxTechnicalExportBytes } from './aiTechnicalExportIntegrity'

const originalCrypto = globalThis.crypto
// 公共 setup 清理浏览器存储；此 Node 字节测试不使用存储。
vi.stubGlobal('localStorage', { clear: vi.fn() })

describe('本地文件完整性', () => {
  beforeEach(() => {
    vi.stubGlobal('isSecureContext', true)
    vi.stubGlobal('crypto', originalCrypto)
  })
  afterAll(() => vi.unstubAllGlobals())
  function file(text: string) {
    const data = new TextEncoder().encode(text)
    return { size: data.byteLength, arrayBuffer: async () => data.buffer }
  }
  it('使用标准 SHA-256 向量核对原字节，不解析或重新序列化内容', async () => {
    expect(await hashTechnicalExportFile(file('abc'))).toEqual({
      bytes: 3,
      sha256: 'BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD',
    })
    const compact = await hashTechnicalExportFile(file('{"a":1}'))
    const spaced = await hashTechnicalExportFile(file('{ "a":1 }'))
    expect(compact.sha256).not.toBe(spaced.sha256)
    await expect(
      hashTechnicalExportFile(file('<script>unparsed text</script>')),
    ).resolves.toHaveProperty('bytes', 30)
  })
  it.each([0, maxTechnicalExportBytes + 1])('拒绝 %s 字节，读取之前检查上限', async (size) => {
    const arrayBuffer = vi.fn()
    await expect(hashTechnicalExportFile({ size, arrayBuffer })).rejects.toThrow('16 MiB')
    expect(arrayBuffer).not.toHaveBeenCalled()
  })
  it('接受精确 16 MiB 上限并检测读取长度变化', async () => {
    await expect(
      hashTechnicalExportFile({
        size: maxTechnicalExportBytes,
        arrayBuffer: async () => new ArrayBuffer(maxTechnicalExportBytes),
      }),
    ).resolves.toHaveProperty('bytes', maxTechnicalExportBytes)
    await expect(
      hashTechnicalExportFile({ size: 3, arrayBuffer: async () => new ArrayBuffer(4) }),
    ).rejects.toThrow('字节数已变化')
  })
  it.each(['insecure', 'unavailable'])('缺少 %s 能力时无降级计算', async (kind) => {
    if (kind === 'insecure') vi.stubGlobal('isSecureContext', false)
    else vi.stubGlobal('crypto', {})
    const arrayBuffer = vi.fn()
    await expect(hashTechnicalExportFile({ size: 3, arrayBuffer })).rejects.toThrow('HTTPS')
    expect(arrayBuffer).not.toHaveBeenCalled()
  })
})
