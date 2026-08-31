import type { AxiosResponse } from 'axios'

export type ApiErrorKind =
  | 'unauthorized'
  | 'forbidden'
  | 'conflict'
  | 'validation'
  | 'rate-limited'
  | 'server'
  | 'network'

export interface ApiErrorOptions {
  status?: number
  code?: number
  traceId?: string
  kind?: ApiErrorKind
  retryable?: boolean
  cause?: unknown
}

export class ApiError extends Error {
  readonly status?: number
  readonly code?: number
  readonly traceId?: string
  readonly kind: ApiErrorKind
  readonly retryable: boolean

  constructor(message: string, options: ApiErrorOptions = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = options.status
    this.code = options.code
    this.traceId = options.traceId
    this.kind = options.kind || 'network'
    this.retryable = options.retryable ?? false
    if (options.cause !== undefined) {
      this.cause = options.cause
    }
  }
}

/** Envelope returned by PermissionSystem.Api for JSON endpoints. */
export interface ApiResult<T = void> {
  succeeded: boolean
  code: number
  message: string
  data: T | null
  traceId?: string
}

export interface PagedResult<T> {
  items: T[]
  pageIndex: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export interface PageQuery {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  sortBy?: string
  descending?: boolean
}

export type ApiResponse<T> = AxiosResponse<ApiResult<T>>

/**
 * Converts a successful API envelope to its payload and keeps server errors
 * consistent with transport errors raised by the request interceptor.
 */
export function unwrapApiResult<T>(response: ApiResponse<T>): T {
  const result = response.data
  if (!result?.succeeded) {
    throw new Error(result?.message || '请求失败')
  }

  return result.data as T
}

export function unwrapVoid(response: ApiResponse<void>): void {
  unwrapApiResult(response)
}
