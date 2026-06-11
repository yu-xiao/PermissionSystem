<script setup lang="ts">
defineOptions({ name: 'DemoBusinessOrder' })

import { ElMessage, ElMessageBox, type FormInstance, type FormRules, type UploadRequestOptions } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import { getDepartmentTree, type DepartmentItem } from '../../../api/departments'
import {
  ApprovalStatus,
  cancelDemoBusinessOrder,
  createDemoBusinessOrder,
  deleteDemoBusinessOrder,
  downloadDemoBusinessOrderImportTemplate,
  exportDemoBusinessOrders,
  getDemoBusinessOrderAttachments,
  getDemoBusinessOrderChangeHistories,
  getDemoBusinessOrderOperationLogs,
  getDemoBusinessOrderPrintTemplates,
  getDemoBusinessOrders,
  importDemoBusinessOrders,
  notifyDemoBusinessOrderOwner,
  printDemoBusinessOrder,
  submitDemoBusinessOrder,
  updateDemoBusinessOrder,
  uploadDemoBusinessOrderAttachment,
  withdrawDemoBusinessOrder,
  type ApprovalStatus as ApprovalStatusValue,
  type DemoBusinessOrderChangeHistoryItem,
  type DemoBusinessOrderImportResult,
  type DemoBusinessOrderItem,
} from '../../../api/demoBusinessOrder'
import type { FileResourceItem } from '../../../api/files'
import type { OperationLogItem } from '../../../api/operation-logs'
import type { PrintTemplateItem } from '../../../api/printTemplate'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.currentUser?.tenantId ?? '')
const loading = ref(false)
const saving = ref(false)
const detailLoading = ref(false)
const dialogVisible = ref(false)
const detailVisible = ref(false)
const printVisible = ref(false)
const importVisible = ref(false)
const editingId = ref('')
const current = ref<DemoBusinessOrderItem>()
const formRef = ref<FormInstance>()
const tableData = ref<DemoBusinessOrderItem[]>([])
const departments = ref<DepartmentItem[]>([])
const attachments = ref<FileResourceItem[]>([])
const histories = ref<DemoBusinessOrderChangeHistoryItem[]>([])
const operationLogs = ref<OperationLogItem[]>([])
const printTemplates = ref<PrintTemplateItem[]>([])
const selectedTemplateId = ref('')
const printHtml = ref('')
const importResult = ref<DemoBusinessOrderImportResult>()
const total = ref(0)

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  approvalStatus: undefined as ApprovalStatusValue | undefined,
  departmentId: undefined as string | undefined,
})

const form = reactive({
  title: '',
  customerName: '',
  amount: 0,
  departmentId: undefined as string | undefined,
})

const rules: FormRules = {
  title: [{ required: true, message: '请输入标题', trigger: 'blur' }],
  customerName: [{ required: true, message: '请输入客户名称', trigger: 'blur' }],
  amount: [{ required: true, message: '请输入金额', trigger: 'change' }],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getDemoBusinessOrders({
      ...query,
      keyword: query.keyword || undefined,
    })
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function loadDepartments() {
  departments.value = await getDepartmentTree(tenantId.value)
}

function resetPageAndLoad() {
  query.pageIndex = 1
  void loadData()
}

function openCreate() {
  editingId.value = ''
  Object.assign(form, {
    title: '',
    customerName: '',
    amount: 0,
    departmentId: undefined,
  })
  dialogVisible.value = true
}

function openEdit(row: DemoBusinessOrderItem) {
  editingId.value = row.id
  Object.assign(form, {
    title: row.title,
    customerName: row.customerName,
    amount: row.amount,
    departmentId: row.departmentId,
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  saving.value = true
  try {
    const payload = {
      tenantId: tenantId.value,
      title: form.title,
      customerName: form.customerName,
      amount: form.amount,
      departmentId: form.departmentId,
    }
    if (editingId.value) {
      await updateDemoBusinessOrder(editingId.value, payload)
    } else {
      await createDemoBusinessOrder(payload)
    }

    ElMessage.success('保存成功')
    dialogVisible.value = false
    await loadData()
  } finally {
    saving.value = false
  }
}

async function remove(row: DemoBusinessOrderItem) {
  await ElMessageBox.confirm(`确定删除 ${row.orderNo} 吗？`, '确认删除')
  await deleteDemoBusinessOrder(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

async function submit(row: DemoBusinessOrderItem) {
  await ElMessageBox.confirm(`确定提交 ${row.orderNo} 进入审批吗？`, '提交审批')
  await submitDemoBusinessOrder(row.id, '提交审批')
  ElMessage.success('提交成功')
  await loadData()
}

async function withdraw(row: DemoBusinessOrderItem) {
  await ElMessageBox.confirm(`确定撤回 ${row.orderNo} 吗？`, '撤回审批')
  await withdrawDemoBusinessOrder(row.id, '撤回审批')
  ElMessage.success('撤回成功')
  await loadData()
}

async function cancel(row: DemoBusinessOrderItem) {
  await ElMessageBox.confirm(`确定取消 ${row.orderNo} 吗？`, '取消单据')
  await cancelDemoBusinessOrder(row.id, '取消单据')
  ElMessage.success('取消成功')
  await loadData()
}

async function exportOrders() {
  const response = await exportDemoBusinessOrders({
    ...query,
    keyword: query.keyword || undefined,
  })
  downloadBlob(response.data, `demo-business-orders-${Date.now()}.xlsx`)
}

async function downloadTemplate() {
  const response = await downloadDemoBusinessOrderImportTemplate()
  downloadBlob(response.data, 'demo-business-order-import-template.xlsx')
}

async function importFile(options: UploadRequestOptions) {
  try {
    importResult.value = await importDemoBusinessOrders(options.file)
    importVisible.value = true
    options.onSuccess(importResult.value)
  } catch (error) {
    ;(options.onError as (error: unknown) => void)(error)
  }
}

async function openDetail(row: DemoBusinessOrderItem) {
  current.value = row
  detailVisible.value = true
  printHtml.value = ''
  selectedTemplateId.value = ''
  detailLoading.value = true
  try {
    const [attachmentRows, historyRows, logRows, templateRows] = await Promise.all([
      getDemoBusinessOrderAttachments(row.id),
      getDemoBusinessOrderChangeHistories(row.id),
      getDemoBusinessOrderOperationLogs(row.id, { pageIndex: 1, pageSize: 10 }),
      getDemoBusinessOrderPrintTemplates(),
    ])
    attachments.value = attachmentRows
    histories.value = historyRows
    operationLogs.value = logRows.items
    printTemplates.value = templateRows
    selectedTemplateId.value = templateRows[0]?.id ?? ''
  } finally {
    detailLoading.value = false
  }
}

async function uploadAttachment(options: UploadRequestOptions) {
  if (!current.value) {
    return
  }

  try {
    await uploadDemoBusinessOrderAttachment(current.value.id, options.file)
    attachments.value = await getDemoBusinessOrderAttachments(current.value.id)
    histories.value = await getDemoBusinessOrderChangeHistories(current.value.id)
    ElMessage.success('上传成功')
    options.onSuccess(true)
  } catch (error) {
    ;(options.onError as (error: unknown) => void)(error)
  }
}

async function printCurrent() {
  if (!current.value || !selectedTemplateId.value) {
    ElMessage.warning('请先配置并选择打印模板')
    return
  }

  const result = await printDemoBusinessOrder(current.value.id, selectedTemplateId.value)
  printHtml.value = result.html
  printVisible.value = true
  histories.value = await getDemoBusinessOrderChangeHistories(current.value.id)
}

async function notifyCurrent() {
  if (!current.value) {
    return
  }

  await notifyDemoBusinessOrderOwner(current.value.id)
  histories.value = await getDemoBusinessOrderChangeHistories(current.value.id)
  ElMessage.success('通知已发送')
}

function canEdit(row: DemoBusinessOrderItem) {
  return row.approvalStatus === ApprovalStatus.Draft ||
    row.approvalStatus === ApprovalStatus.Rejected ||
    row.approvalStatus === ApprovalStatus.Withdrawn
}

function canDelete(row: DemoBusinessOrderItem) {
  return row.approvalStatus === ApprovalStatus.Draft
}

function canCancel(row: DemoBusinessOrderItem) {
  return row.approvalStatus === ApprovalStatus.Draft
}

function canSubmit(row: DemoBusinessOrderItem) {
  return canEdit(row)
}

function canWithdraw(row: DemoBusinessOrderItem) {
  return row.approvalStatus === ApprovalStatus.Pending
}

function statusText(status: ApprovalStatusValue) {
  const map: Record<ApprovalStatusValue, string> = {
    [ApprovalStatus.Draft]: '草稿',
    [ApprovalStatus.Pending]: '审批中',
    [ApprovalStatus.Approved]: '已通过',
    [ApprovalStatus.Rejected]: '已拒绝',
    [ApprovalStatus.Withdrawn]: '已撤回',
    [ApprovalStatus.Cancelled]: '已取消',
  }
  return map[status] ?? '未知'
}

function statusType(status: ApprovalStatusValue) {
  if (status === ApprovalStatus.Pending) {
    return 'warning'
  }
  if (status === ApprovalStatus.Approved) {
    return 'success'
  }
  if (status === ApprovalStatus.Rejected) {
    return 'danger'
  }
  return 'info'
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}

loadDepartments()
loadData()
</script>

<template>
  <PageContainer title="Demo 业务单据" description="业务模块接入模板示例，不代表正式 WMS / ERP 单据。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input
          v-model="query.keyword"
          clearable
          placeholder="单号 / 标题 / 客户 / 负责人"
          @keyup.enter="resetPageAndLoad"
        />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.approvalStatus" clearable placeholder="审批状态" style="width: 140px">
          <el-option label="草稿" :value="ApprovalStatus.Draft" />
          <el-option label="审批中" :value="ApprovalStatus.Pending" />
          <el-option label="已通过" :value="ApprovalStatus.Approved" />
          <el-option label="已拒绝" :value="ApprovalStatus.Rejected" />
          <el-option label="已撤回" :value="ApprovalStatus.Withdrawn" />
          <el-option label="已取消" :value="ApprovalStatus.Cancelled" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-tree-select
          v-model="query.departmentId"
          :data="departments"
          clearable
          check-strictly
          node-key="id"
          placeholder="部门"
          :props="{ label: 'name', children: 'children' }"
          style="width: 180px"
        />
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'demo-business-order:view'" type="primary" @click="resetPageAndLoad">查询</el-button>
        <el-button v-permission="'demo-business-order:create'" @click="openCreate">新增</el-button>
        <el-button v-permission="'demo-business-order:export'" @click="exportOrders">导出</el-button>
        <el-button v-permission="'demo-business-order:import'" @click="downloadTemplate">导入模板</el-button>
        <el-upload
          v-permission="'demo-business-order:import'"
          :http-request="importFile"
          :show-file-list="false"
          class="inline-upload"
        >
          <el-button>导入预览</el-button>
        </el-upload>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="orderNo" label="单据编号" min-width="160" />
      <el-table-column prop="title" label="标题" min-width="180" show-overflow-tooltip />
      <el-table-column prop="customerName" label="客户" min-width="140" show-overflow-tooltip />
      <el-table-column prop="amount" label="金额" width="120" />
      <el-table-column prop="ownerUserName" label="负责人" width="120" />
      <el-table-column label="状态" width="110">
        <template #default="{ row }">
          <el-tag :type="statusType(row.approvalStatus)">{{ statusText(row.approvalStatus) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="创建时间" width="180">
        <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="360" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'demo-business-order:view'" link type="primary" @click="openDetail(row)">接入点</el-button>
          <el-button v-if="canEdit(row)" v-permission="'demo-business-order:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button v-if="canDelete(row)" v-permission="'demo-business-order:delete'" link type="danger" @click="remove(row)">删除</el-button>
          <el-button v-if="canCancel(row)" v-permission="'demo-business-order:cancel'" link type="warning" @click="cancel(row)">取消</el-button>
          <el-button v-if="canSubmit(row)" v-permission="'demo-business-order:submit'" link type="success" @click="submit(row)">提交</el-button>
          <el-button v-if="canWithdraw(row)" v-permission="'demo-business-order:withdraw'" link type="warning" @click="withdraw(row)">撤回</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-model:current-page="query.pageIndex"
      v-model:page-size="query.pageSize"
      class="pager"
      background
      layout="total, sizes, prev, pager, next"
      :total="total"
      @change="loadData"
    />

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑 Demo 业务单据' : '新增 Demo 业务单据'" width="620px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="100px">
        <el-form-item label="标题" prop="title">
          <el-input v-model="form.title" />
        </el-form-item>
        <el-form-item label="客户" prop="customerName">
          <el-input v-model="form.customerName" />
        </el-form-item>
        <el-form-item label="金额" prop="amount">
          <el-input-number v-model="form.amount" :min="0" :precision="2" class="full-width" />
        </el-form-item>
        <el-form-item label="归属部门">
          <el-tree-select
            v-model="form.departmentId"
            :data="departments"
            clearable
            check-strictly
            node-key="id"
            :props="{ label: 'name', children: 'children' }"
            class="full-width"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">保存</el-button>
      </template>
    </el-dialog>

    <el-drawer v-model="detailVisible" size="720px" :title="current?.orderNo || '接入点'">
      <div v-loading="detailLoading" class="detail-panel">
        <el-descriptions v-if="current" :column="2" border>
          <el-descriptions-item label="标题">{{ current.title }}</el-descriptions-item>
          <el-descriptions-item label="客户">{{ current.customerName }}</el-descriptions-item>
          <el-descriptions-item label="金额">{{ current.amount }}</el-descriptions-item>
          <el-descriptions-item label="状态">{{ statusText(current.approvalStatus) }}</el-descriptions-item>
        </el-descriptions>

        <el-tabs>
          <el-tab-pane label="附件">
            <el-upload
              v-permission="'demo-business-order:attachment:upload'"
              :http-request="uploadAttachment"
              :show-file-list="false"
            >
              <el-button type="primary">上传附件</el-button>
            </el-upload>
            <el-table :data="attachments" border class="section-table">
              <el-table-column prop="originalName" label="文件名" min-width="220" />
              <el-table-column prop="size" label="大小" width="120" />
              <el-table-column label="上传时间" width="180">
                <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
              </el-table-column>
            </el-table>
          </el-tab-pane>

          <el-tab-pane label="打印">
            <div class="print-actions">
              <el-select v-model="selectedTemplateId" placeholder="选择打印模板" class="print-select">
                <el-option
                  v-for="item in printTemplates"
                  :key="item.id"
                  :label="item.templateName"
                  :value="item.id"
                />
              </el-select>
              <el-button v-permission="'demo-business-order:print'" type="primary" @click="printCurrent">打印预览</el-button>
            </div>
          </el-tab-pane>

          <el-tab-pane label="变更历史">
            <el-timeline>
              <el-timeline-item v-for="item in histories" :key="`${item.action}-${item.changedAt}`" :timestamp="formatTime(item.changedAt)">
                <strong>{{ item.action }}</strong>
                <span class="history-user">{{ item.changedByName || '-' }}</span>
                <div>{{ item.description }}</div>
              </el-timeline-item>
            </el-timeline>
          </el-tab-pane>

          <el-tab-pane label="操作日志">
            <el-table :data="operationLogs" border>
              <el-table-column prop="action" label="动作" width="160" />
              <el-table-column prop="requestMethod" label="方法" width="90" />
              <el-table-column prop="requestPath" label="路径" min-width="240" show-overflow-tooltip />
              <el-table-column label="时间" width="180">
                <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
              </el-table-column>
            </el-table>
          </el-tab-pane>

          <el-tab-pane label="通知">
            <el-button v-permission="'demo-business-order:notify'" type="primary" @click="notifyCurrent">发送通知</el-button>
          </el-tab-pane>
        </el-tabs>
      </div>
    </el-drawer>

    <el-dialog v-model="printVisible" title="打印预览" width="760px">
      <iframe class="print-frame" :srcdoc="printHtml" />
    </el-dialog>

    <el-dialog v-model="importVisible" title="导入预览" width="680px">
      <el-alert
        v-if="importResult"
        :title="`总行数 ${importResult.totalRows}，成功 ${importResult.successRows}，失败 ${importResult.failedRows}`"
        type="info"
        show-icon
      />
      <el-table v-if="importResult?.errors.length" :data="importResult.errors" border class="section-table">
        <el-table-column prop="rowNumber" label="行号" width="90" />
        <el-table-column prop="columnName" label="列" width="140" />
        <el-table-column prop="message" label="错误" min-width="220" />
      </el-table>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.full-width {
  width: 100%;
}

.inline-upload {
  display: inline-flex;
}

.detail-panel {
  min-height: 360px;
}

.section-table {
  margin-top: 12px;
}

.print-actions {
  display: flex;
  gap: 12px;
  align-items: center;
}

.print-select {
  width: 260px;
}

.history-user {
  margin-left: 8px;
  color: var(--el-text-color-secondary);
}

.print-frame {
  width: 100%;
  height: 520px;
  border: 1px solid var(--el-border-color);
}
</style>
