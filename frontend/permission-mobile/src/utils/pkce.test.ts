import { afterEach, describe, expect, it, vi } from 'vitest'
import { beginAuthorization } from '../api/auth'
import {
  createCodeChallenge,
  createPkcePair,
  validateAuthorizationIssuer,
  validateAuthorizationState,
  validateReturnPath,
} from './pkce'

describe('PKCE helpers', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('creates an RFC 7636 S256 challenge', async () => {
    const pair = await createPkcePair()
    expect(pair.codeVerifier.length).toBeGreaterThanOrEqual(43)
    expect(pair.codeChallenge).toMatch(/^[A-Za-z0-9_-]+$/)
    expect(await createCodeChallenge(pair.codeVerifier)).toBe(pair.codeChallenge)
  })

  it('accepts only same-origin internal return paths and matching state', () => {
    vi.stubGlobal('window', { location: { origin: 'https://mobile.example.test' } })
    expect(validateReturnPath('/tasks/todo?filter=pending#top')).toBe('/tasks/todo?filter=pending#top')
    expect(validateReturnPath('https://evil.example.test/steal')).toBe('/home')
    expect(validateReturnPath('//evil.example.test/steal')).toBe('/home')
    expect(() => validateAuthorizationState('expected', 'different')).toThrow('登录状态校验失败')
    expect(validateAuthorizationState('expected', 'expected')).toBe(true)
    expect(validateAuthorizationIssuer('https://api.example.test/', 'https://api.example.test')).toBe(true)
    expect(() => validateAuthorizationIssuer('https://api.example.test', 'https://evil.example.test')).toThrow('授权服务器校验失败')
  })

  it('keeps the selected tenant in the authorization request', async () => {
    const authorizationUrl = await beginAuthorization({
      issuer: 'https://api.example.test',
      clientId: 'permission-mobile',
      redirectUri: 'https://mobile.example.test/authorize/callback',
      tenant: 'default',
    })

    expect(new URL(authorizationUrl).searchParams.get('tenant')).toBe('default')
  })
})
