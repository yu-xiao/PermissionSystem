import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface StateMachineQuery extends PageQuery {
  businessType?: string
  isEnabled?: boolean
}

export interface StateMachineItem {
  id: string
  tenantId: string
  businessType: string
  name: string
  description?: string
  isEnabled: boolean
  createdAt: string
}

export interface CreateStateMachineRequest {
  businessType: string
  name: string
  description?: string
  isEnabled: boolean
}

export interface UpdateStateMachineRequest {
  name: string
  description?: string
  isEnabled: boolean
}

export interface StateDefinitionItem {
  id: string
  machineId: string
  stateCode: string
  stateName: string
  stateType: string
  color?: string
  sort: number
  isInitial: boolean
  isFinal: boolean
}

export interface CreateOrUpdateStateRequest {
  stateCode: string
  stateName: string
  stateType: string
  color?: string
  sort: number
  isInitial: boolean
  isFinal: boolean
}

export interface StateTransitionItem {
  id: string
  machineId: string
  fromState: string
  toState: string
  actionCode: string
  actionName: string
  requiredPermission?: string
  conditionJson?: string
  isEnabled: boolean
  sort: number
}

export interface CreateOrUpdateTransitionRequest {
  fromState: string
  toState: string
  actionCode: string
  actionName: string
  requiredPermission?: string
  conditionJson?: string
  isEnabled: boolean
  sort: number
}

export interface StateTransitionLogQuery extends PageQuery {
  businessType?: string
  businessId?: string
  actionCode?: string
}

export interface StateTransitionLogItem {
  id: string
  tenantId: string
  businessType: string
  businessId: string
  fromState: string
  toState: string
  actionCode: string
  actionName: string
  operatorUserId?: string
  operatorUserName?: string
  comment?: string
  createdAt: string
}

export interface ExecuteStateTransitionRequest {
  businessType: string
  businessId: string
  actionCode: string
  comment?: string
}

const baseUrl = '/api/system/state-machines'

export function getStateMachines(params: StateMachineQuery) {
  return request.get<ApiResult<PagedResult<StateMachineItem>>>(baseUrl, { params }).then((res) => res.data.data)
}

export function createStateMachine(data: CreateStateMachineRequest) {
  return request.post<ApiResult<StateMachineItem>>(baseUrl, data).then((res) => res.data.data)
}

export function updateStateMachine(id: string, data: UpdateStateMachineRequest) {
  return request.put<ApiResult<StateMachineItem>>(`${baseUrl}/${id}`, data).then((res) => res.data.data)
}

export function deleteStateMachine(id: string) {
  return request.delete<ApiResult<void>>(`${baseUrl}/${id}`)
}

export function getStates(machineId: string) {
  return request.get<ApiResult<StateDefinitionItem[]>>(`${baseUrl}/${machineId}/states`).then((res) => res.data.data)
}

export function createState(machineId: string, data: CreateOrUpdateStateRequest) {
  return request.post<ApiResult<StateDefinitionItem>>(`${baseUrl}/${machineId}/states`, data).then((res) => res.data.data)
}

export function updateState(machineId: string, stateId: string, data: CreateOrUpdateStateRequest) {
  return request
    .put<ApiResult<StateDefinitionItem>>(`${baseUrl}/${machineId}/states/${stateId}`, data)
    .then((res) => res.data.data)
}

export function deleteState(machineId: string, stateId: string) {
  return request.delete<ApiResult<void>>(`${baseUrl}/${machineId}/states/${stateId}`)
}

export function getTransitions(machineId: string) {
  return request.get<ApiResult<StateTransitionItem[]>>(`${baseUrl}/${machineId}/transitions`).then((res) => res.data.data)
}

export function createTransition(machineId: string, data: CreateOrUpdateTransitionRequest) {
  return request
    .post<ApiResult<StateTransitionItem>>(`${baseUrl}/${machineId}/transitions`, data)
    .then((res) => res.data.data)
}

export function updateTransition(machineId: string, transitionId: string, data: CreateOrUpdateTransitionRequest) {
  return request
    .put<ApiResult<StateTransitionItem>>(`${baseUrl}/${machineId}/transitions/${transitionId}`, data)
    .then((res) => res.data.data)
}

export function deleteTransition(machineId: string, transitionId: string) {
  return request.delete<ApiResult<void>>(`${baseUrl}/${machineId}/transitions/${transitionId}`)
}

export function executeStateTransition(data: ExecuteStateTransitionRequest) {
  return request.post<ApiResult<void>>(`${baseUrl}/transition`, data)
}

export function getStateTransitionLogs(params: StateTransitionLogQuery) {
  return request.get<ApiResult<PagedResult<StateTransitionLogItem>>>(`${baseUrl}/logs`, { params }).then((res) => res.data.data)
}
