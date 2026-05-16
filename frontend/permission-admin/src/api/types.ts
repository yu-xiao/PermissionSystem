export interface ApiResult<T> {
  succeeded: boolean
  code: number
  message: string
  data: T
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
  pageIndex: number
  pageSize: number
  keyword?: string
}
