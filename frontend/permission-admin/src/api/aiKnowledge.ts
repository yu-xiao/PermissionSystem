import { request } from '../utils/request'
import type { ApiResult, PagedResult } from './types'

export interface AiKnowledgeReference {
  documentId: string
  versionId: string
  chunkId: string
  contentHash: string
}
export interface AiKnowledgeHit {
  reference: AiKnowledgeReference
  title: string
  versionNumber: number
  startLine: number
  endLine: number
  content: string
  validFrom: string
  validUntil: string
}
export interface AiKnowledgeSearchResult {
  items: AiKnowledgeHit[]
  queriedAt: string
  isTruncated: boolean
  limitation: string
}
export interface AiKnowledgeVersion {
  id: string
  versionNumber: number
  parseStatus: 'Pending' | 'Ready' | 'Failed'
  errorCode?: string
  validFrom: string
  validUntil: string
  publishedAt?: string
}
export interface AiKnowledgeDocument {
  id: string
  title: string
  owner: string
  license: string
  currentVersionId?: string
  accessVersion: number
  rowVersion: string
  roleIds: string[]
  versions: AiKnowledgeVersion[]
}
const base = '/api/ai/knowledge'
export function getKnowledgeDocuments(pageIndex: number, pageSize = 20) {
  return request
    .get<ApiResult<PagedResult<AiKnowledgeDocument>>>(`${base}/documents`, {
      params: { pageIndex, pageSize },
    })
    .then((r) => r.data.data)
}
export function createKnowledgeDocument(data: {
  title: string
  owner: string
  license: string
  synthetic: boolean
  roleIds: string[]
}) {
  return request
    .post<ApiResult<AiKnowledgeDocument>>(`${base}/documents`, data)
    .then((r) => r.data.data)
}
export function setKnowledgeAccess(doc: AiKnowledgeDocument, roleIds: string[]) {
  return request
    .put<ApiResult<AiKnowledgeDocument>>(`${base}/documents/${doc.id}/access`, {
      roleIds,
      rowVersion: doc.rowVersion,
    })
    .then((r) => r.data.data)
}
export function uploadKnowledgeVersion(
  doc: AiKnowledgeDocument,
  file: File,
  validFrom: string,
  validUntil: string,
) {
  const data = new FormData()
  data.append('file', file)
  data.append('validFrom', validFrom)
  data.append('validUntil', validUntil)
  data.append('rowVersion', doc.rowVersion)
  return request
    .post<ApiResult<AiKnowledgeDocument>>(`${base}/documents/${doc.id}/versions`, data)
    .then((r) => r.data.data)
}
export function publishKnowledgeVersion(doc: AiKnowledgeDocument, versionId: string) {
  return request.post(`${base}/documents/${doc.id}/versions/${versionId}/publish`, {
    rowVersion: doc.rowVersion,
  })
}
export function deleteKnowledgeDocument(doc: AiKnowledgeDocument) {
  return request.delete(`${base}/documents/${doc.id}`, { data: { rowVersion: doc.rowVersion } })
}
export function searchKnowledge(keyword: string) {
  return request
    .get<ApiResult<AiKnowledgeSearchResult>>(`${base}/search`, { params: { keyword, limit: 5 } })
    .then((r) => r.data.data)
}
export function previewKnowledgeVersion(
  documentId: string,
  versionId: string,
  startSequence: number,
) {
  return request
    .get<ApiResult<AiKnowledgeSearchResult>>(
      `${base}/documents/${documentId}/versions/${versionId}/preview`,
      {
        params: { startSequence },
      },
    )
    .then((r) => r.data.data)
}
export function getKnowledgeSource(ref: AiKnowledgeReference) {
  return request
    .get<ApiResult<AiKnowledgeHit>>(
      `${base}/documents/${ref.documentId}/versions/${ref.versionId}/chunks/${ref.chunkId}`,
      { params: { contentHash: ref.contentHash } },
    )
    .then((r) => r.data.data)
}
