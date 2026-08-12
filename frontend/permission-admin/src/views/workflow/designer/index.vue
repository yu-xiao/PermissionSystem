<script setup lang="ts">
defineOptions({
  name: 'WorkflowDesigner',
})

import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getDepartmentTree, type DepartmentItem } from '../../../api/departments'
import { getRoles, type RoleItem } from '../../../api/roles'
import { getUsers, type UserItem } from '../../../api/users'
import {
  getWorkflowDefinition,
  getWorkflowDesigner,
  publishWorkflowDefinition,
  saveWorkflowDesigner,
  type WorkflowDesigner,
  type WorkflowDesignerCondition,
  type WorkflowDesignerEdge,
  type WorkflowDesignerNode,
} from '../../../api/workflowDefinition'
import { useAuthStore } from '../../../stores/auth'

const WorkflowNodeType = {
  Start: 0,
  Approver: 1,
  Cc: 2,
  Condition: 3,
  End: 4,
} as const

const WorkflowApproverType = {
  Users: 0,
  Roles: 1,
  DepartmentManager: 2,
  Initiator: 4,
  InitiatorDirectLeader: 5,
  InitiatorDepartmentManager: 6,
  FormFieldUser: 7,
} as const

const WorkflowApprovalMode = {
  Single: 0,
  Countersign: 1,
  OrSign: 2,
  Sequential: 3,
} as const

type WorkflowNodeTypeValue = (typeof WorkflowNodeType)[keyof typeof WorkflowNodeType]
type WorkflowApproverTypeValue = (typeof WorkflowApproverType)[keyof typeof WorkflowApproverType]
type WorkflowApprovalModeValue = (typeof WorkflowApprovalMode)[keyof typeof WorkflowApprovalMode]
type ConditionLogic = 'AND' | 'OR'

interface DesignerFlowNode {
  localId: string
  id?: string
  nodeKey: string
  nodeName: string
  nodeType: WorkflowNodeTypeValue
  approverType?: WorkflowApproverTypeValue
  approverIds: string[]
  formField: string
  approvalMode?: WorkflowApprovalModeValue
  timeoutHours?: number
  configJson?: string
  positionX: number
  positionY: number
  sort: number
  branches: ConditionBranch[]
}

interface ConditionBranch {
  localId: string
  conditionId: string
  name: string
  isDefault: boolean
  logic: ConditionLogic
  field: string
  operator: string
  value: string
  children: DesignerFlowNode[]
}

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const definitionId = computed(() => String(route.params.id))
const tenantId = computed(() => authStore.effectiveTenantId)
const loading = ref(false)
const saving = ref(false)
const publishing = ref(false)
const drawerVisible = ref(false)
const previewVisible = ref(false)
const definitionName = ref('')
const definitionCode = ref('')
const definitionVersion = ref(1)
const definitionConcurrencyToken = ref('')
const flowNodes = ref<DesignerFlowNode[]>([])
const selectedNode = ref<DesignerFlowNode>()
const selectedBranch = ref<ConditionBranch>()
const users = ref<UserItem[]>([])
const roles = ref<RoleItem[]>([])
const departments = ref<DepartmentItem[]>([])
const previewJson = ref('')

const drawerTitle = computed(() => {
  if (selectedBranch.value) {
    return '条件配置'
  }

  return selectedNode.value ? `${nodeTypeText(selectedNode.value.nodeType)}配置` : '节点配置'
})

const selectedApproverOptions = computed(() => {
  if (!selectedNode.value) {
    return []
  }

  if (selectedNode.value.approverType === WorkflowApproverType.Roles) {
    return roles.value.map((role) => ({ label: `${role.name}（${role.code}）`, value: role.id }))
  }

  return users.value.map((user) => ({ label: `${user.displayName}（${user.userName}）`, value: user.id }))
})

const conditionOperators = [
  { label: '=', value: '=' },
  { label: '!=', value: '!=' },
  { label: '>', value: '>' },
  { label: '>=', value: '>=' },
  { label: '<', value: '<' },
  { label: '<=', value: '<=' },
  { label: 'contains', value: 'contains' },
  { label: 'in', value: 'in' },
]

function createId(prefix: string) {
  const id = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`
  return `${prefix}_${id.replace(/-/g, '')}`
}

function createGuid() {
  return globalThis.crypto?.randomUUID?.() ?? '00000000-0000-0000-0000-000000000000'
}

function createFlowNode(type: WorkflowNodeTypeValue, name?: string): DesignerFlowNode {
  const localId = createId('node')
  const defaults: Record<WorkflowNodeTypeValue, string> = {
    [WorkflowNodeType.Start]: '发起人',
    [WorkflowNodeType.Approver]: '审批人',
    [WorkflowNodeType.Cc]: '抄送人',
    [WorkflowNodeType.Condition]: '条件分支',
    [WorkflowNodeType.End]: '结束',
  }

  return {
    localId,
    nodeKey: localId,
    nodeName: name ?? defaults[type],
    nodeType: type,
    approverType:
      type === WorkflowNodeType.Approver || type === WorkflowNodeType.Cc
        ? WorkflowApproverType.Users
        : undefined,
    approverIds: [],
    formField: '',
    approvalMode: type === WorkflowNodeType.Approver ? WorkflowApprovalMode.Single : undefined,
    timeoutHours: undefined,
    configJson: undefined,
    positionX: 0,
    positionY: 0,
    sort: 0,
    branches: type === WorkflowNodeType.Condition ? createDefaultBranches() : [],
  }
}

function createDefaultBranches(): ConditionBranch[] {
  return [
    {
      localId: createId('branch'),
      conditionId: createGuid(),
      name: '条件1',
      isDefault: false,
      logic: 'AND',
      field: '',
      operator: '=',
      value: '',
      children: [],
    },
    {
      localId: createId('branch'),
      conditionId: createGuid(),
      name: '默认条件',
      isDefault: true,
      logic: 'AND',
      field: '',
      operator: '=',
      value: '',
      children: [],
    },
  ]
}

function initEmptyDesigner() {
  flowNodes.value = [
    createFlowNode(WorkflowNodeType.Start, '发起人'),
    createFlowNode(WorkflowNodeType.End, '结束'),
  ]
}

async function loadDesigner() {
  loading.value = true
  try {
    const [definition, designer] = await Promise.all([
      getWorkflowDefinition(definitionId.value),
      getWorkflowDesigner(definitionId.value),
    ])

    definitionName.value = definition.name
    definitionCode.value = definition.code
    definitionVersion.value = definition.version
    definitionConcurrencyToken.value = definition.concurrencyToken
    hydrateDesigner(designer)
  } finally {
    loading.value = false
  }
}

async function loadOptions() {
  const [userResult, roleResult, departmentTree] = await Promise.all([
    getUsers({ pageIndex: 1, pageSize: 200, keyword: '', isEnabled: true }),
    getRoles({ pageIndex: 1, pageSize: 200, keyword: '', isEnabled: true }),
    getDepartmentTree(tenantId.value),
  ])

  users.value = userResult.items
  roles.value = roleResult.items
  departments.value = departmentTree
}

function hydrateDesigner(designer: WorkflowDesigner) {
  if (!designer.nodes.length) {
    initEmptyDesigner()
    return
  }

  const nodes = [...designer.nodes].sort((a, b) => a.sort - b.sort).map(toFlowNode)
  const nodeMap = new Map(nodes.map((node) => [node.nodeKey, node]))
  const conditionMap = new Map(designer.conditions.map((condition) => [condition.id, condition]))
  const edgesByFrom = new Map<string, WorkflowDesignerEdge[]>()

  for (const edge of designer.edges) {
    const edges = edgesByFrom.get(edge.fromNodeKey) ?? []
    edges.push(edge)
    edgesByFrom.set(edge.fromNodeKey, edges)
  }

  for (const edges of edgesByFrom.values()) {
    edges.sort((a, b) => a.sort - b.sort)
  }

  function sortedOutgoing(nodeKey: string) {
    return edgesByFrom.get(nodeKey) ?? []
  }

  function collectReachableDistances(startKey: string) {
    const distances = new Map<string, number>()
    const queue: Array<{ key: string; distance: number }> = [{ key: startKey, distance: 0 }]

    while (queue.length) {
      const current = queue.shift()!
      const previousDistance = distances.get(current.key)
      if (previousDistance !== undefined && previousDistance <= current.distance) {
        continue
      }

      distances.set(current.key, current.distance)
      for (const edge of sortedOutgoing(current.key)) {
        queue.push({ key: edge.toNodeKey, distance: current.distance + 1 })
      }
    }

    return distances
  }

  function resolveMergeNodeKey(targetKeys: string[]) {
    const validTargets = targetKeys.filter((key) => nodeMap.has(key))
    if (validTargets.length < 2) {
      return undefined
    }

    const distanceMaps = validTargets.map(collectReachableDistances)
    const commonKeys = [...distanceMaps[0].keys()].filter((key) =>
      distanceMaps.every((distances) => distances.has(key)),
    )

    return commonKeys
      .map((key) => ({
        key,
        distance: Math.max(...distanceMaps.map((distances) => distances.get(key) ?? Number.MAX_SAFE_INTEGER)),
      }))
      .sort((a, b) => a.distance - b.distance)[0]?.key
  }

  function buildBranches(node: DesignerFlowNode, visited: Set<string>) {
    const outgoing = sortedOutgoing(node.nodeKey)
    const mergeNodeKey = resolveMergeNodeKey(outgoing.map((edge) => edge.toNodeKey))
    node.branches = outgoing.map((edge, index) => {
      const condition = edge.conditionId ? conditionMap.get(edge.conditionId) : undefined
      return {
        localId: createId('branch'),
        conditionId: edge.conditionId ?? createGuid(),
        name: edge.isDefault ? '默认条件' : condition?.conditionName || `条件${index + 1}`,
        isDefault: edge.isDefault,
        logic: readConditionLogic(condition?.expressionJson),
        field: readConditionField(condition?.expressionJson),
        operator: readConditionOperator(condition?.expressionJson),
        value: readConditionValue(condition?.expressionJson),
        children: edge.toNodeKey === mergeNodeKey ? [] : buildSequence(edge.toNodeKey, mergeNodeKey, new Set(visited)),
      }
    })

    if (!node.branches.some((branch) => branch.isDefault)) {
      node.branches.push(createDefaultBranches()[1])
    }

    return mergeNodeKey
  }

  function buildSequence(startNodeKey: string | undefined, stopNodeKey?: string, visited = new Set<string>()) {
    const result: DesignerFlowNode[] = []
    let currentNodeKey = startNodeKey

    while (currentNodeKey && currentNodeKey !== stopNodeKey && !visited.has(currentNodeKey)) {
      const node = nodeMap.get(currentNodeKey)
      if (!node) {
        break
      }

      visited.add(currentNodeKey)
      result.push(node)

      if (node.nodeType === WorkflowNodeType.Condition) {
        currentNodeKey = buildBranches(node, visited)
        continue
      }

      currentNodeKey = sortedOutgoing(currentNodeKey)[0]?.toNodeKey
    }

    return result
  }

  const startNode = nodes.find((node) => node.nodeType === WorkflowNodeType.Start) ?? nodes[0]
  const rebuiltNodes = buildSequence(startNode?.nodeKey)
  flowNodes.value = rebuiltNodes.length ? rebuiltNodes : nodes
}

function toFlowNode(node: WorkflowDesignerNode): DesignerFlowNode {
  const approverIds = parseApproverIds(node.approverIds, node.approverType as WorkflowApproverTypeValue)
  return {
    localId: node.nodeKey || createId('node'),
    id: node.id,
    nodeKey: node.nodeKey || createId('node'),
    nodeName: node.nodeName,
    nodeType: node.nodeType as WorkflowNodeTypeValue,
    approverType: node.approverType as WorkflowApproverTypeValue | undefined,
    approverIds,
    formField:
      node.approverType === WorkflowApproverType.FormFieldUser ? node.approverIds ?? '' : '',
    approvalMode: node.approvalMode as WorkflowApprovalModeValue | undefined,
    timeoutHours: readConfigNumber(node.configJson, 'timeoutHours'),
    configJson: node.configJson,
    positionX: node.positionX,
    positionY: node.positionY,
    sort: node.sort,
    branches: [],
  }
}

function parseApproverIds(value?: string, approverType?: WorkflowApproverTypeValue) {
  if (!value || approverType === WorkflowApproverType.FormFieldUser) {
    return []
  }

  try {
    const parsed = JSON.parse(value)
    return Array.isArray(parsed) ? parsed.filter((item) => typeof item === 'string') : []
  } catch {
    return value.split(',').map((item) => item.trim()).filter(Boolean)
  }
}

function insertNodeAfter(index: number, type: WorkflowNodeTypeValue) {
  flowNodes.value.splice(index + 1, 0, createFlowNode(type))
}

function insertBranchNode(branch: ConditionBranch, index: number, type: WorkflowNodeTypeValue) {
  branch.children.splice(index + 1, 0, createFlowNode(type))
}

function editNode(node: DesignerFlowNode) {
  selectedNode.value = node
  selectedBranch.value = undefined
  drawerVisible.value = true
}

function editBranch(branch: ConditionBranch) {
  selectedBranch.value = branch
  selectedNode.value = undefined
  drawerVisible.value = true
}

function removeNode(list: DesignerFlowNode[], node: DesignerFlowNode) {
  if (node.nodeType === WorkflowNodeType.Start) {
    return
  }

  if (node.nodeType === WorkflowNodeType.End && list.length === 1) {
    return
  }

  const index = list.findIndex((item) => item.localId === node.localId)
  if (index >= 0) {
    list.splice(index, 1)
  }
}

function addBranch(node: DesignerFlowNode) {
  node.branches.splice(Math.max(node.branches.length - 1, 0), 0, {
    localId: createId('branch'),
    conditionId: createGuid(),
    name: `条件${node.branches.length}`,
    isDefault: false,
    logic: 'AND',
    field: '',
    operator: '=',
    value: '',
    children: [],
  })
}

function removeBranch(node: DesignerFlowNode, branch: ConditionBranch) {
  if (branch.isDefault) {
    ElMessage.warning('默认条件不可删除')
    return
  }

  node.branches = node.branches.filter((item) => item.localId !== branch.localId)
}

function buildDesignerPayload(): WorkflowDesigner {
  const nodes: WorkflowDesignerNode[] = []
  const edges: WorkflowDesignerEdge[] = []
  const conditions: WorkflowDesignerCondition[] = []
  const nodeKeys = new Set<string>()
  let nodeSort = 1
  let edgeSort = 1
  let conditionSort = 1

  function addNode(node: DesignerFlowNode) {
    if (nodeKeys.has(node.nodeKey)) {
      return
    }

    nodeKeys.add(node.nodeKey)
    nodes.push({
      id: node.id,
      nodeKey: node.nodeKey,
      nodeName: node.nodeName,
      nodeType: node.nodeType,
      approverType: node.approverType,
      approverIds: buildApproverIds(node),
      approvalMode: node.approvalMode,
      configJson: JSON.stringify({ timeoutHours: node.timeoutHours ?? null }),
      positionX: node.positionX,
      positionY: node.positionY,
      sort: nodeSort++,
    })
  }

  function addEdge(fromNodeKey: string, toNodeKey: string, branch?: ConditionBranch) {
    const conditionId = branch && !branch.isDefault ? branch.conditionId : undefined
    edges.push({
      fromNodeKey,
      toNodeKey,
      conditionId,
      isDefault: branch?.isDefault === true,
      sort: edgeSort++,
    })

    if (branch && !branch.isDefault) {
      conditions.push({
        id: branch.conditionId,
        nodeKey: fromNodeKey,
        conditionName: branch.name,
        expressionJson: buildConditionExpression(branch),
        sort: conditionSort++,
      })
    }
  }

  function processList(list: DesignerFlowNode[], nextKey?: string): string | undefined {
    let currentNextKey = nextKey
    for (let index = list.length - 1; index >= 0; index -= 1) {
      const node = list[index]
      addNode(node)

      if (node.nodeType === WorkflowNodeType.Condition) {
        for (const branch of node.branches) {
          const branchFirstKey = processList(branch.children, currentNextKey)
          const targetKey = branchFirstKey ?? currentNextKey
          if (targetKey) {
            addEdge(node.nodeKey, targetKey, branch)
          }
        }
      } else if (node.nodeType !== WorkflowNodeType.End && currentNextKey) {
        addEdge(node.nodeKey, currentNextKey)
      }

      currentNextKey = node.nodeKey
    }

    return list[0]?.nodeKey ?? nextKey
  }

  processList(flowNodes.value)
  return { nodes, edges, conditions }
}

function validateBeforeSave() {
  const allNodes = collectAllNodes(flowNodes.value)
  if (!allNodes.some((node) => node.nodeType === WorkflowNodeType.Start)) {
    throw new Error('必须有发起人节点')
  }

  if (!allNodes.some((node) => node.nodeType === WorkflowNodeType.Approver || node.nodeType === WorkflowNodeType.End)) {
    throw new Error('必须至少有一个审批节点或结束节点')
  }

  for (const node of allNodes) {
    if (node.nodeType === WorkflowNodeType.Approver && requiresApprover(node) && !hasApproverValue(node)) {
      throw new Error(`审批节点“${node.nodeName}”必须配置审批人`)
    }

    if (node.nodeType === WorkflowNodeType.Condition) {
      if (!node.branches.some((branch) => branch.isDefault)) {
        throw new Error(`条件分支“${node.nodeName}”必须有默认条件`)
      }

      for (const branch of node.branches.filter((item) => !item.isDefault)) {
        if (!branch.field || !branch.operator || !branch.value) {
          throw new Error(`条件“${branch.name}”必须配置字段、操作符和值`)
        }
      }
    }
  }
}

async function saveDesigner() {
  try {
    validateBeforeSave()
    saving.value = true
    await saveWorkflowDesigner(definitionId.value, {
      ...buildDesignerPayload(),
      concurrencyToken: definitionConcurrencyToken.value,
    })
    ElMessage.success('保存成功')
    await loadDesigner()
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '保存失败')
  } finally {
    saving.value = false
  }
}

async function publishDefinition() {
  await ElMessageBox.confirm('发布前请确认流程结构已保存，确定继续发布吗？', '确认发布')
  publishing.value = true
  try {
    await publishWorkflowDefinition(definitionId.value)
    ElMessage.success('发布成功')
    await loadDesigner()
  } finally {
    publishing.value = false
  }
}

function showPreview() {
  try {
    validateBeforeSave()
    previewJson.value = JSON.stringify(buildDesignerPayload(), null, 2)
    previewVisible.value = true
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '预览失败')
  }
}

function goBack() {
  void router.push('/workflow/definition')
}

function collectAllNodes(list: DesignerFlowNode[]): DesignerFlowNode[] {
  return list.flatMap((node) => [
    node,
    ...node.branches.flatMap((branch) => collectAllNodes(branch.children)),
  ])
}

function requiresApprover(node: DesignerFlowNode) {
  return node.approverType !== WorkflowApproverType.Initiator &&
    node.approverType !== WorkflowApproverType.InitiatorDirectLeader &&
    node.approverType !== WorkflowApproverType.InitiatorDepartmentManager
}

function hasApproverValue(node: DesignerFlowNode) {
  return node.approverType === WorkflowApproverType.FormFieldUser
    ? Boolean(node.formField)
    : node.approverIds.length > 0
}

function buildApproverIds(node: DesignerFlowNode) {
  if (node.nodeType !== WorkflowNodeType.Approver && node.nodeType !== WorkflowNodeType.Cc) {
    return undefined
  }

  if (node.approverType === WorkflowApproverType.FormFieldUser) {
    return node.formField || undefined
  }

  return node.approverIds.length ? JSON.stringify(node.approverIds) : undefined
}

function buildConditionExpression(branch: ConditionBranch) {
  return JSON.stringify({
    logic: branch.logic,
    children: [
      {
        field: branch.field,
        operator: branch.operator,
        value: normalizeConditionValue(branch.value, branch.operator),
      },
    ],
  })
}

function normalizeConditionValue(value: string, operator: string) {
  if (operator === 'in') {
    return value.split(',').map((item) => item.trim()).filter(Boolean)
  }

  const numberValue = Number(value)
  return Number.isFinite(numberValue) && value.trim() !== '' ? numberValue : value
}

function nodeTypeText(type: WorkflowNodeTypeValue) {
  const map: Record<WorkflowNodeTypeValue, string> = {
    [WorkflowNodeType.Start]: '发起人',
    [WorkflowNodeType.Approver]: '审批人',
    [WorkflowNodeType.Cc]: '抄送人',
    [WorkflowNodeType.Condition]: '条件分支',
    [WorkflowNodeType.End]: '结束',
  }

  return map[type]
}

function nodeClass(type: WorkflowNodeTypeValue) {
  return {
    'node-card--start': type === WorkflowNodeType.Start,
    'node-card--approver': type === WorkflowNodeType.Approver,
    'node-card--cc': type === WorkflowNodeType.Cc,
    'node-card--condition': type === WorkflowNodeType.Condition,
    'node-card--end': type === WorkflowNodeType.End,
  }
}

function nodeSummary(node: DesignerFlowNode) {
  if (node.nodeType === WorkflowNodeType.Start) {
    return '提交人 / 发起人本人'
  }

  if (node.nodeType === WorkflowNodeType.End) {
    return '流程完成'
  }

  if (node.nodeType === WorkflowNodeType.Condition) {
    return `${node.branches.length} 个分支，含默认条件`
  }

  const prefix = node.nodeType === WorkflowNodeType.Cc ? '抄送给' : '审批人'
  return `${prefix}：${approverSummary(node)}`
}

function approverSummary(node: DesignerFlowNode) {
  if (node.approverType === WorkflowApproverType.Initiator) {
    return '发起人本人'
  }

  if (node.approverType === WorkflowApproverType.InitiatorDirectLeader) {
    return '发起人直属上级'
  }

  if (node.approverType === WorkflowApproverType.InitiatorDepartmentManager) {
    return '发起人部门负责人'
  }

  if (node.approverType === WorkflowApproverType.FormFieldUser) {
    return node.formField || '表单字段未配置'
  }

  if (!node.approverIds.length) {
    return '未配置'
  }

  if (node.approverType === WorkflowApproverType.Roles) {
    return node.approverIds.map((id) => roleName(id)).join('、')
  }

  if (node.approverType === WorkflowApproverType.DepartmentManager) {
    return node.approverIds.map((id) => departmentName(id)).join('、')
  }

  return node.approverIds.map((id) => userName(id)).join('、')
}

function userName(id: string) {
  const user = users.value.find((item) => item.id === id)
  return user ? user.displayName : id
}

function roleName(id: string) {
  const role = roles.value.find((item) => item.id === id)
  return role ? role.name : id
}

function departmentName(id: string) {
  const department = findDepartment(departments.value, id)
  return department ? department.name : id
}

function findDepartment(items: DepartmentItem[], id: string): DepartmentItem | undefined {
  for (const item of items) {
    if (item.id === id) {
      return item
    }

    const child = findDepartment(item.children ?? [], id)
    if (child) {
      return child
    }
  }

  return undefined
}

function readConfigNumber(configJson: string | undefined, key: string) {
  if (!configJson) {
    return undefined
  }

  try {
    const config = JSON.parse(configJson) as Record<string, unknown>
    const value = config[key]
    return typeof value === 'number' ? value : undefined
  } catch {
    return undefined
  }
}

function readConditionLogic(expressionJson?: string): ConditionLogic {
  return readCondition(expressionJson).logic
}

function readConditionField(expressionJson?: string) {
  return readCondition(expressionJson).field
}

function readConditionOperator(expressionJson?: string) {
  return readCondition(expressionJson).operator
}

function readConditionValue(expressionJson?: string) {
  const value = readCondition(expressionJson).value
  return Array.isArray(value) ? value.join(',') : String(value ?? '')
}

function readCondition(expressionJson?: string) {
  try {
    const parsed = JSON.parse(expressionJson || '{}') as {
      logic?: ConditionLogic
      children?: Array<{ field?: string; operator?: string; value?: unknown }>
    }
    const first = parsed.children?.[0]
    const logic: ConditionLogic = parsed.logic === 'OR' ? 'OR' : 'AND'
    return {
      logic,
      field: first?.field ?? '',
      operator: first?.operator ?? '=',
      value: first?.value ?? '',
    }
  } catch {
    return { logic: 'AND' as ConditionLogic, field: '', operator: '=', value: '' }
  }
}

onMounted(async () => {
  await Promise.all([loadOptions(), loadDesigner()])
})
</script>

<template>
  <section class="workflow-designer">
    <header class="designer-header">
      <div class="designer-header__left">
        <el-button @click="goBack">返回</el-button>
        <div>
          <h1>{{ definitionName || '流程设计器' }}</h1>
          <p>{{ definitionCode }} v{{ definitionVersion }}</p>
        </div>
      </div>
      <div class="designer-header__actions">
        <el-button @click="showPreview">预览 JSON</el-button>
        <el-button type="primary" :loading="saving" @click="saveDesigner">保存</el-button>
        <el-button type="success" :loading="publishing" @click="publishDefinition">发布</el-button>
      </div>
    </header>

    <main v-loading="loading" class="designer-canvas">
      <div class="flow-root">
        <template v-for="(node, index) in flowNodes" :key="node.localId">
          <article class="node-card" :class="nodeClass(node.nodeType)" @click="editNode(node)">
            <div class="node-card__title">{{ node.nodeName }}</div>
            <div class="node-card__body">{{ nodeSummary(node) }}</div>
            <button
              v-if="node.nodeType !== WorkflowNodeType.Start"
              class="node-card__remove"
              type="button"
              @click.stop="removeNode(flowNodes, node)"
            >
              ×
            </button>
          </article>

          <div v-if="node.nodeType === WorkflowNodeType.Condition" class="condition-branches">
            <div
              v-for="branch in node.branches"
              :key="branch.localId"
              class="condition-branch"
              :class="{ 'is-default': branch.isDefault }"
            >
              <button class="branch-title" type="button" @click="editBranch(branch)">
                {{ branch.name }}
                <span v-if="branch.isDefault">默认</span>
              </button>

              <div class="branch-lane">
                <div class="flow-plus flow-plus--small">
                  <el-dropdown trigger="click">
                    <button class="plus-button" type="button">+</button>
                    <template #dropdown>
                      <el-dropdown-menu>
                        <el-dropdown-item @click="insertBranchNode(branch, -1, WorkflowNodeType.Approver)">审批人</el-dropdown-item>
                        <el-dropdown-item @click="insertBranchNode(branch, -1, WorkflowNodeType.Cc)">抄送人</el-dropdown-item>
                        <el-dropdown-item @click="insertBranchNode(branch, -1, WorkflowNodeType.End)">结束节点</el-dropdown-item>
                      </el-dropdown-menu>
                    </template>
                  </el-dropdown>
                </div>

                <template v-for="(child, childIndex) in branch.children" :key="child.localId">
                  <article class="node-card node-card--branch" :class="nodeClass(child.nodeType)" @click="editNode(child)">
                    <div class="node-card__title">{{ child.nodeName }}</div>
                    <div class="node-card__body">{{ nodeSummary(child) }}</div>
                    <button class="node-card__remove" type="button" @click.stop="removeNode(branch.children, child)">×</button>
                  </article>
                  <div class="flow-plus flow-plus--small">
                    <el-dropdown trigger="click">
                      <button class="plus-button" type="button">+</button>
                      <template #dropdown>
                        <el-dropdown-menu>
                          <el-dropdown-item @click="insertBranchNode(branch, childIndex, WorkflowNodeType.Approver)">审批人</el-dropdown-item>
                          <el-dropdown-item @click="insertBranchNode(branch, childIndex, WorkflowNodeType.Cc)">抄送人</el-dropdown-item>
                          <el-dropdown-item @click="insertBranchNode(branch, childIndex, WorkflowNodeType.End)">结束节点</el-dropdown-item>
                        </el-dropdown-menu>
                      </template>
                    </el-dropdown>
                  </div>
                </template>
              </div>

              <el-button v-if="!branch.isDefault" class="branch-remove" link type="danger" @click="removeBranch(node, branch)">删除条件</el-button>
            </div>
            <el-button class="branch-add" type="primary" link @click="addBranch(node)">新增条件</el-button>
          </div>

          <div v-if="index < flowNodes.length - 1" class="flow-plus">
            <el-dropdown trigger="click">
              <button class="plus-button" type="button">+</button>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item @click="insertNodeAfter(index, WorkflowNodeType.Approver)">审批人</el-dropdown-item>
                  <el-dropdown-item @click="insertNodeAfter(index, WorkflowNodeType.Cc)">抄送人</el-dropdown-item>
                  <el-dropdown-item @click="insertNodeAfter(index, WorkflowNodeType.Condition)">条件分支</el-dropdown-item>
                  <el-dropdown-item @click="insertNodeAfter(index, WorkflowNodeType.End)">结束节点</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </div>
        </template>
      </div>
    </main>

    <el-drawer v-model="drawerVisible" :title="drawerTitle" size="420px">
      <el-form v-if="selectedNode" label-width="120px">
        <el-form-item label="节点名称">
          <el-input v-model="selectedNode.nodeName" :disabled="selectedNode.nodeType === WorkflowNodeType.Start" />
        </el-form-item>

        <template v-if="selectedNode.nodeType === WorkflowNodeType.Approver || selectedNode.nodeType === WorkflowNodeType.Cc">
          <el-form-item :label="selectedNode.nodeType === WorkflowNodeType.Cc ? '抄送人类型' : '审批人类型'">
            <el-select v-model="selectedNode.approverType" class="full-width">
              <el-option label="指定用户" :value="WorkflowApproverType.Users" />
              <el-option label="指定角色" :value="WorkflowApproverType.Roles" />
              <el-option label="部门负责人" :value="WorkflowApproverType.DepartmentManager" />
              <el-option label="发起人直属上级" :value="WorkflowApproverType.InitiatorDirectLeader" />
              <el-option label="发起人部门负责人" :value="WorkflowApproverType.InitiatorDepartmentManager" />
              <el-option label="表单字段指定人" :value="WorkflowApproverType.FormFieldUser" />
            </el-select>
          </el-form-item>

          <el-form-item v-if="selectedNode.approverType === WorkflowApproverType.Users || selectedNode.approverType === WorkflowApproverType.Roles" label="选择人员">
            <el-select v-model="selectedNode.approverIds" class="full-width" multiple filterable>
              <el-option
                v-for="option in selectedApproverOptions"
                :key="option.value"
                :label="option.label"
                :value="option.value"
              />
            </el-select>
          </el-form-item>

          <el-form-item v-else-if="selectedNode.approverType === WorkflowApproverType.DepartmentManager" label="选择部门">
            <el-tree-select
              v-model="selectedNode.approverIds"
              class="full-width"
              :data="departments"
              multiple
              show-checkbox
              node-key="id"
              :props="{ label: 'name', children: 'children' }"
            />
          </el-form-item>

          <el-form-item v-else-if="selectedNode.approverType === WorkflowApproverType.FormFieldUser" label="表单字段">
            <el-input v-model="selectedNode.formField" placeholder="例如：managerUserId" />
          </el-form-item>

          <el-form-item v-if="selectedNode.nodeType === WorkflowNodeType.Approver" label="审批方式">
            <el-radio-group v-model="selectedNode.approvalMode">
              <el-radio :value="WorkflowApprovalMode.Single">单人</el-radio>
              <el-radio :value="WorkflowApprovalMode.Countersign">会签</el-radio>
              <el-radio :value="WorkflowApprovalMode.OrSign">或签</el-radio>
              <el-radio :value="WorkflowApprovalMode.Sequential">依次审批</el-radio>
            </el-radio-group>
          </el-form-item>

          <el-form-item v-if="selectedNode.nodeType === WorkflowNodeType.Approver" label="超时时间">
            <el-input-number v-model="selectedNode.timeoutHours" :min="0" :precision="0" />
            <span class="form-hint">小时，0 或空表示不限制</span>
          </el-form-item>
        </template>

        <template v-if="selectedNode.nodeType === WorkflowNodeType.Condition">
          <el-form-item label="分支数量">
            <span>{{ selectedNode.branches.length }}</span>
          </el-form-item>
        </template>
      </el-form>

      <el-form v-else-if="selectedBranch" label-width="110px">
        <el-form-item label="条件名称">
          <el-input v-model="selectedBranch.name" :disabled="selectedBranch.isDefault" />
        </el-form-item>
        <el-form-item label="默认条件">
          <el-switch v-model="selectedBranch.isDefault" disabled />
        </el-form-item>
        <template v-if="!selectedBranch.isDefault">
          <el-form-item label="组合关系">
            <el-radio-group v-model="selectedBranch.logic">
              <el-radio value="AND">AND</el-radio>
              <el-radio value="OR">OR</el-radio>
            </el-radio-group>
          </el-form-item>
          <el-form-item label="字段">
            <el-input v-model="selectedBranch.field" placeholder="例如：amount" />
          </el-form-item>
          <el-form-item label="操作符">
            <el-select v-model="selectedBranch.operator" class="full-width">
              <el-option v-for="item in conditionOperators" :key="item.value" :label="item.label" :value="item.value" />
            </el-select>
          </el-form-item>
          <el-form-item label="值">
            <el-input v-model="selectedBranch.value" placeholder="in 操作符可用逗号分隔" />
          </el-form-item>
        </template>
      </el-form>
    </el-drawer>

    <el-dialog v-model="previewVisible" title="保存数据预览" width="820px">
      <pre class="json-preview">{{ previewJson }}</pre>
    </el-dialog>
  </section>
</template>

<style scoped>
.workflow-designer {
  min-height: calc(100vh - var(--app-header-height) - var(--app-tabs-height) - var(--app-content-padding) * 2);
  border: 1px solid var(--app-border-soft);
  border-radius: 8px;
  background: var(--app-surface);
  box-shadow: var(--app-shadow-soft);
  overflow: hidden;
}

.designer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 16px 18px;
  background: var(--app-surface);
  border-bottom: 1px solid var(--app-border-soft);
}

.designer-header__left {
  display: flex;
  align-items: center;
  gap: 14px;
}

.designer-header h1 {
  margin: 0;
  font-size: 18px;
  font-weight: 650;
  color: var(--app-text);
}

.designer-header p {
  margin: 4px 0 0;
  font-size: 12px;
  color: var(--app-text-secondary);
}

.designer-header__actions {
  display: flex;
  gap: 8px;
}

.designer-canvas {
  height: calc(100vh - var(--app-header-height) - var(--app-tabs-height) - var(--app-content-padding) * 2 - 70px);
  overflow: auto;
  padding: 28px;
  background:
    linear-gradient(var(--app-border-soft) 1px, transparent 1px),
    linear-gradient(90deg, var(--app-border-soft) 1px, transparent 1px),
    var(--app-bg);
  background-size: 24px 24px;
}

.flow-root {
  min-width: 820px;
  width: max-content;
  margin: 0 auto;
}

.node-card {
  position: relative;
  width: 260px;
  margin: 0 auto;
  overflow: hidden;
  cursor: pointer;
  background: var(--app-surface);
  border: 1px solid var(--app-border-soft);
  border-radius: 8px;
  box-shadow: 0 8px 22px rgb(15 23 42 / 10%);
}

.node-card__title {
  padding: 9px 14px;
  font-size: 14px;
  font-weight: 650;
  color: #fff;
}

.node-card__body {
  min-height: 54px;
  padding: 12px 14px;
  font-size: 13px;
  line-height: 1.5;
  color: var(--app-text-secondary);
  word-break: break-word;
}

.node-card__remove {
  position: absolute;
  top: 6px;
  right: 8px;
  width: 20px;
  height: 20px;
  padding: 0;
  cursor: pointer;
  color: #fff;
  background: transparent;
  border: 0;
  font-size: 18px;
}

.node-card--start .node-card__title {
  background: #2f80ed;
}

.node-card--approver .node-card__title {
  background: #f59e0b;
}

.node-card--cc .node-card__title {
  background: #06b6d4;
}

.node-card--condition .node-card__title {
  background: #f97316;
}

.node-card--end .node-card__title {
  background: #22c55e;
}

.node-card--branch {
  width: 220px;
}

.flow-plus {
  position: relative;
  display: flex;
  justify-content: center;
  padding: 30px 0;
}

.flow-plus::before {
  position: absolute;
  top: 0;
  bottom: 0;
  left: 50%;
  width: 2px;
  content: '';
  background: var(--app-border-color);
  transform: translateX(-50%);
}

.flow-plus--small {
  padding: 18px 0;
}

.plus-button {
  position: relative;
  z-index: 1;
  width: 32px;
  height: 32px;
  cursor: pointer;
  color: #fff;
  background: #2563eb;
  border: 3px solid #fff;
  border-radius: 50%;
  box-shadow: 0 5px 14px rgb(37 99 235 / 32%);
  font-size: 22px;
  line-height: 24px;
}

.condition-branches {
  position: relative;
  display: flex;
  align-items: stretch;
  gap: 22px;
  justify-content: center;
  min-width: 760px;
  margin: 26px 0;
  padding: 20px 16px 26px;
  border-top: 2px solid var(--app-border-color);
  border-bottom: 2px solid var(--app-border-color);
}

.condition-branch {
  position: relative;
  min-width: 240px;
  padding: 0 10px 8px;
}

.condition-branch::before {
  position: absolute;
  top: -22px;
  left: 50%;
  width: 2px;
  height: 22px;
  content: '';
  background: var(--app-border-color);
}

.branch-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  height: 34px;
  padding: 0 12px;
  cursor: pointer;
  color: #9a3412;
  background: #fff7ed;
  border: 1px solid #fed7aa;
  border-radius: 6px;
  font-weight: 600;
}

.branch-title span {
  color: var(--app-text-secondary);
  font-size: 12px;
}

.condition-branch.is-default .branch-title {
  color: var(--app-text);
  background: var(--app-surface-soft);
  border-color: var(--app-border-color);
}

.branch-lane {
  min-height: 92px;
  padding-top: 4px;
}

.branch-remove,
.branch-add {
  display: block;
  margin: 8px auto 0;
}

.full-width {
  width: 100%;
}

.form-hint {
  margin-left: 8px;
  color: #909399;
  font-size: 12px;
}

.json-preview {
  max-height: 560px;
  padding: 14px;
  overflow: auto;
  margin: 0;
  color: var(--app-text);
  background: var(--app-surface-soft);
  border: 1px solid var(--app-border-soft);
  border-radius: 6px;
  font-size: 12px;
}
</style>
