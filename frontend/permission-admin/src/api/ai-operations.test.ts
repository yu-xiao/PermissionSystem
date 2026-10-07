import { describe, expect, it, vi } from 'vitest'
import {
  exportAiTechnicalMetadata,
  getAiScenarioOperations,
  getAiTechnicalExportReceipts,
  getAiTechnicalExportReceipt,
  getAiCostQuality,
  getAiCostQualityTrends,
} from './ai'
import { request } from '../utils/request'

vi.mock('../utils/request', () => ({ request: { get: vi.fn(), post: vi.fn() } }))

describe('估算质量只读契约', () => {
  it('仅 GET 独立调用窗口、币种分页和取消信号', async () => {
    vi.clearAllMocks()
    const data = { scope: 'CurrentTenantReadableRunInvocations' }
    vi.mocked(request.get).mockResolvedValue({ data: { data } })
    const params = {
      from: '2026-10-01T00:00:00Z',
      to: '2026-10-02T00:00:00Z',
      pageIndex: 2,
      pageSize: 20,
    }
    const signal = new AbortController().signal
    expect(await getAiCostQuality(params, signal)).toEqual(data)
    expect(request.get).toHaveBeenCalledWith('/api/ai/operations/cost-quality', { params, signal })
    expect(request.post).not.toHaveBeenCalled()
  })
})

describe('估算质量趋势只读契约', () => {
  it('只 GET 趋势窗口、币种分页和取消信号，不触发摘要或上传', async () => {
    vi.clearAllMocks()
    const data = { grouping: 'UsageCreatedAtUtcDay', bucketTimezone: 'UTC' }
    vi.mocked(request.get).mockResolvedValue({ data: { data } })
    const params = {
      from: '2026-10-01T00:00:00Z',
      to: '2026-10-02T00:00:00Z',
      pageIndex: 2,
      pageSize: 20,
    }
    const signal = new AbortController().signal
    expect(await getAiCostQualityTrends(params, signal)).toEqual(data)
    expect(request.get).toHaveBeenCalledWith('/api/ai/operations/cost-quality/trends', {
      params,
      signal,
    })
    expect(request.get).toHaveBeenCalledTimes(1)
    expect(request.post).not.toHaveBeenCalled()
  })
})

describe('本人导出凭据契约', () => {
  it('只使用两个受控 GET、独立时间窗口和取消信号，不上传文件或 Hash', async () => {
    vi.clearAllMocks()
    const data = { scope: 'CurrentCaller' }
    vi.mocked(request.get).mockResolvedValue({ data: { data } })
    const signal = new AbortController().signal
    const params = {
      from: '2026-10-01T00:00:00Z',
      to: '2026-10-02T00:00:00Z',
      pageIndex: 2,
      pageSize: 20,
    }
    expect(await getAiTechnicalExportReceipts(params, signal)).toEqual(data)
    expect(request.get).toHaveBeenCalledWith('/api/ai/operations/technical-export-receipts', {
      params,
      signal,
    })
    const window = { from: params.from, to: params.to }
    expect(await getAiTechnicalExportReceipt('export/id', window, signal)).toEqual(data)
    expect(request.get).toHaveBeenCalledWith(
      '/api/ai/operations/technical-export-receipts/export%2Fid',
      { params: window, signal },
    )
    expect(request.post).not.toHaveBeenCalled()
  })
})

describe('场景运营查询契约', () => {
  it('复用现有请求实例，只传日期和分页并读取 ApiResult', async () => {
    const response = { metricsVersion: 1, scenarios: { items: [], totalCount: 0 } }
    vi.mocked(request.get).mockResolvedValue({ data: { succeeded: true, data: response } })
    const params = {
      from: '2026-10-01T00:00:00Z',
      to: '2026-10-02T00:00:00Z',
      pageIndex: 2,
      pageSize: 10,
    }
    expect(await getAiScenarioOperations(params)).toEqual(response)
    expect(request.get).toHaveBeenCalledWith('/api/ai/operations/scenarios', { params })
  })
})

describe('技术元数据下载契约', () => {
  const fileName = 'ai-technical-0123456789abcdef0123456789abcdef.json'
  const body = new Blob(['{"manifest":{},"payload":{}}'], { type: 'application/json' })
  const signal = new AbortController().signal
  const data = { from: '2026-10-01T00:00:00Z', to: '2026-10-02T00:00:00Z' }
  function response() {
    return {
      status: 200,
      data: body,
      headers: {
        'content-type': 'application/json; charset=utf-8',
        'content-disposition': `attachment; filename=${fileName}; filename*=UTF-8''${fileName}`,
      },
    }
  }
  it('复用 Axios Blob 与取消信号，接受固定 JSON 文件名', async () => {
    vi.mocked(request.post).mockResolvedValue(response())
    expect(await exportAiTechnicalMetadata(data, signal)).toEqual({ content: body, fileName })
    expect(request.post).toHaveBeenCalledWith('/api/ai/operations/technical-export', data, {
      responseType: 'blob',
      signal,
    })
  })
  it.each(['error-json', 'wrong-type', 'path-name', 'empty', 'too-large', 'status'])(
    '拒绝 %s，不将错误响应当文件',
    async (kind) => {
      const result = response()
      if (kind === 'error-json') result.headers['content-disposition'] = ''
      if (kind === 'wrong-type') result.headers['content-type'] = 'text/html'
      if (kind === 'path-name')
        result.headers['content-disposition'] = 'attachment; filename="../../secret.json"'
      if (kind === 'empty') result.data = new Blob([])
      if (kind === 'too-large') result.data = new Blob([new Uint8Array(16 * 1024 * 1024 + 1)])
      if (kind === 'status') result.status = 202
      vi.mocked(request.post).mockResolvedValue(result)
      await expect(exportAiTechnicalMetadata(data, signal)).rejects.toThrow('响应无效')
    },
  )
})
