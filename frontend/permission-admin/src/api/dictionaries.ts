import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export type DictionaryStatus = 'Enabled' | 'Disabled'

export interface DictionaryTypeQuery extends PageQuery {
  status?: DictionaryStatus
}

export interface DictionaryTypeItem {
  id: string
  tenantId: string
  code: string
  name: string
  description?: string
  status: DictionaryStatus
  sort: number
  createdAt: string
}

export interface CreateDictionaryTypeRequest {
  tenantId: string
  code: string
  name: string
  description?: string
  status: DictionaryStatus
  sort: number
}

export type UpdateDictionaryTypeRequest = Omit<CreateDictionaryTypeRequest, 'tenantId' | 'code'>

export interface DictionaryItemQuery extends PageQuery {
  typeCode?: string
  status?: DictionaryStatus
}

export interface DictionaryItem {
  id: string
  tenantId: string
  typeCode: string
  label: string
  value: string
  color?: string
  cssClass?: string
  isDefault: boolean
  status: DictionaryStatus
  sort: number
  remark?: string
  createdAt: string
}

export interface CreateDictionaryItemRequest {
  tenantId: string
  typeCode: string
  label: string
  value: string
  color?: string
  cssClass?: string
  isDefault: boolean
  status: DictionaryStatus
  sort: number
  remark?: string
}

export type UpdateDictionaryItemRequest = Omit<CreateDictionaryItemRequest, 'tenantId' | 'typeCode'>

export function getDictionaryTypes(params: DictionaryTypeQuery) {
  return request
    .get<ApiResult<PagedResult<DictionaryTypeItem>>>('/api/dictionaries/types', { params })
    .then((res) => res.data.data)
}

export function createDictionaryType(data: CreateDictionaryTypeRequest) {
  return request.post<ApiResult<DictionaryTypeItem>>('/api/dictionaries/types', data).then((res) => res.data.data)
}

export function updateDictionaryType(id: string, data: UpdateDictionaryTypeRequest) {
  return request.put<ApiResult<DictionaryTypeItem>>(`/api/dictionaries/types/${id}`, data).then((res) => res.data.data)
}

export function deleteDictionaryType(id: string) {
  return request.delete<ApiResult<void>>(`/api/dictionaries/types/${id}`)
}

export function getDictionaryItems(params: DictionaryItemQuery) {
  return request
    .get<ApiResult<PagedResult<DictionaryItem>>>('/api/dictionaries/items', { params })
    .then((res) => res.data.data)
}

export function createDictionaryItem(data: CreateDictionaryItemRequest) {
  return request.post<ApiResult<DictionaryItem>>('/api/dictionaries/items', data).then((res) => res.data.data)
}

export function updateDictionaryItem(id: string, data: UpdateDictionaryItemRequest) {
  return request.put<ApiResult<DictionaryItem>>(`/api/dictionaries/items/${id}`, data).then((res) => res.data.data)
}

export function deleteDictionaryItem(id: string) {
  return request.delete<ApiResult<void>>(`/api/dictionaries/items/${id}`)
}

export function getEnabledDictionaryItems(typeCode: string) {
  return request
    .get<ApiResult<DictionaryItem[]>>(`/api/dictionaries/types/${typeCode}/items/enabled`)
    .then((res) => res.data.data)
}
