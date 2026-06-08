import { request } from '../utils/request'
import type { TokenResponse } from './auth'
import type { ApiResult } from './types'

export interface OidcChallengeResponse {
  redirectUrl: string
}

export function challengeOidc(providerCode: string, returnUrl?: string) {
  return request
    .get<ApiResult<OidcChallengeResponse>>(`/api/sso/oidc/${encodeURIComponent(providerCode)}/challenge`, {
      params: { returnUrl },
    })
    .then((res) => res.data.data)
}

export async function exchangeSsoLoginCode(loginCode: string) {
  const form = new URLSearchParams()
  form.set('grant_type', 'sso_oidc')
  form.set('login_code', loginCode)
  form.set('client_id', import.meta.env.VITE_OAUTH_CLIENT_ID)
  form.set('client_secret', import.meta.env.VITE_OAUTH_CLIENT_SECRET)
  form.set('scope', 'permission-system-api offline_access')

  const { data } = await request.post<TokenResponse>('/api/sso/oidc/exchange', form)
  return data
}
