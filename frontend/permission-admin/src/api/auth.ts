import { request } from '../utils/request'

export interface TokenResponse {
  access_token: string
  refresh_token?: string
  expires_in?: number
  token_type: string
}

export interface LoginRequest {
  username: string
  password: string
}

export async function login(payload: LoginRequest) {
  const form = new URLSearchParams()
  form.set('grant_type', 'password')
  form.set('username', payload.username)
  form.set('password', payload.password)
  form.set('client_id', import.meta.env.VITE_OAUTH_CLIENT_ID)
  form.set('client_secret', import.meta.env.VITE_OAUTH_CLIENT_SECRET)
  form.set('scope', 'permission-system-api offline_access')

  const { data } = await request.post<TokenResponse>('/connect/token', form)
  return data
}

export async function refreshToken(refreshToken: string) {
  const form = new URLSearchParams()
  form.set('grant_type', 'refresh_token')
  form.set('refresh_token', refreshToken)
  form.set('client_id', import.meta.env.VITE_OAUTH_CLIENT_ID)
  form.set('client_secret', import.meta.env.VITE_OAUTH_CLIENT_SECRET)

  const { data } = await request.post<TokenResponse>('/connect/token', form)
  return data
}
