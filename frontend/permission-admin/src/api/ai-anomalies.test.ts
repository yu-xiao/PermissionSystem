import { describe, expect, it } from 'vitest'
import { anomalyEventLink } from './ai-anomalies'

describe('受控提醒链接', () => {
  it('只接受固定站内路由和完整事件 UUID', () => {
    const link = '/system/ai-anomalies?eventId=11111111-1111-1111-1111-111111111111'
    expect(anomalyEventLink(link)).toBe(link)
    for (const value of [
      undefined,
      '',
      'https://example.com',
      '//example.com',
      'javascript:alert(1)',
      `${link}&tenantId=fake`,
      '/system/ai-anomalies?eventId=fake',
      `/../${link}`,
      `${link}#redirect`,
    ])
      expect(anomalyEventLink(value)).toBeUndefined()
  })
})
