import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

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
  businessType?: string
  businessId?: string
  createdBy?: string
  createdAt: string
}

export function getFiles(params: FileResourceQuery) {
  return request.get<ApiResult<PagedResult<FileResourceItem>>>('/api/files', { params }).then((res) => res.data.data)
}

export function getFilesByBusiness(businessType: string, businessId: string) {
  return request
    .get<ApiResult<FileResourceItem[]>>(`/api/files/business/${businessType}/${businessId}`)
    .then((res) => res.data.data)
}

export function uploadFile(file: File, businessType?: string, businessId?: string) {
  const form = new FormData()
  form.append('file', file)

  if (businessType) {
    form.append('businessType', businessType)
  }

  if (businessId) {
    form.append('businessId', businessId)
  }

  return request
    .post<ApiResult<FileResourceItem>>('/api/files', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 60000,
    })
    .then((res) => res.data.data)
}

export function downloadFile(id: string) {
  return request.get<Blob>(`/api/files/${id}/download`, {
    responseType: 'blob',
    timeout: 60000,
  })
}

export function deleteFile(id: string) {
  return request.delete<ApiResult<void>>(`/api/files/${id}`)
}
