import { request } from '../utils/request'
import type { ApiResult, PageQuery, PagedResult } from './types'
import { unwrapApiResult } from './types'

export interface FileResourceQuery extends PageQuery {
  businessType?: string
  businessId?: string
  storageProvider?: string
  extension?: string
}

export interface FileResourceItem {
  id: string
  tenantId: string
  originalName: string
  fileName: string
  extension: string
  contentType: string
  size: number
  storageProvider: string
  bucketName: string
  objectKey: string
  url?: string
  md5: string
  sha256?: string
  businessType?: string
  businessId?: string
  createdBy?: string
  createdAt: string
  fileStatus?: number
  scanStatus?: number
  scanMessage?: string
}

export async function getFiles(params: FileResourceQuery = {}) {
  return unwrapApiResult(
    await request.get<ApiResult<PagedResult<FileResourceItem>>>('/api/v1/files', { params }),
  )
}

export async function getFilesByBusiness(businessType: string, businessId: string) {
  return unwrapApiResult(
    await request.get<ApiResult<FileResourceItem[]>>(
      `/api/v1/files/business/${encodeURIComponent(businessType)}/${businessId}`,
    ),
  )
}

export async function uploadFile(file: File, businessType?: string, businessId?: string) {
  const form = new FormData()
  form.append('file', file)
  if (businessType) form.append('businessType', businessType)
  if (businessId) form.append('businessId', businessId)

  return unwrapApiResult(
    await request.post<ApiResult<FileResourceItem>>('/api/v1/files', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 60000,
    }),
  )
}

export async function downloadFile(id: string) {
  return request.get<Blob>(`/api/v1/files/${id}/download`, {
    responseType: 'blob',
    timeout: 60000,
  })
}

export async function deleteFile(id: string) {
  return unwrapApiResult(await request.delete<ApiResult<void>>(`/api/v1/files/${id}`))
}

