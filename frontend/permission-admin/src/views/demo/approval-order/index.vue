<script setup lang="ts">
defineOptions({ name: 'DemoApprovalOrder' })

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { getDepartmentTree, type DepartmentItem } from '../../../api/departments'
import {
  ApprovalStatus,
  cancelDemoApprovalOrder,
  createDemoApprovalOrder,
  deleteDemoApprovalOrder,
  getDemoApprovalOrders,
  submitDemoApprovalOrder,
  updateDemoApprovalOrder,
  withdrawDemoApprovalOrder,
  type ApprovalStatus as ApprovalStatusValue,
  type DemoApprovalOrderItem,
} from '../../../api/demoApprovalOrder'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'
import { useAuthStore } from '../../../stores/auth'

const router = useRouter()
const authStore = useAuthStore()
const tenantId = computed(() => authStore.currentUser?.tenantId ?? '')
const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const withdrawVisible = ref(false)
const editingId = ref('')
const current = ref<DemoApprovalOrderItem>()
const formRef = ref<FormInstance>()
const withdrawFormRef = ref<FormInstance>()
const tableData = ref<DemoApprovalOrderItem[]>([])
const departments = ref<DepartmentItem[]>([])
const total = ref(0)
const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  approvalStatus: undefined as ApprovalStatusValue | undefined,
})
const form = reactive({
  orderNo: '',
  title: '',
  amount: 0,
  departmentId: undefined as string | undefined,
})
const withdrawForm = reactive({ comment: '' })

const rules: FormRules = {
  title: [{ required: true, message: '请输入标题', trigger: 'blur' }],
  amount: [{ required: true, message: '请输入金额', trigger: 'change' }],
}

const withdrawRules: FormRules = {
  comment: [{ required: true, message: '请输入撤回原因', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getDemoApprovalOrders({
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
    orderNo: '',
    title: '',
    amount: 0,
    departmentId: undefined,
  })
  dialogVisible.value = true
}

function openEdit(row: DemoApprovalOrderItem) {
  editingId.value = row.id
  Object.assign(form, {
    orderNo: row.orderNo,
    title: row.title,
    amount: row.amount,
    departmentId: row.departmentId,
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  saving.value = true
  try {
    if (editingId.value) {
      await updateDemoApprovalOrder(editingId.value, {
        title: form.title,
        amount: form.amount,
        departmentId: form.departmentId,
      })
    } else {
      await createDemoApprovalOrder({
        tenantId: tenantId.value,
        title: form.title,
        amount: form.amount,
        departmentId: form.departmentId,
      })
    }

    ElMessage.success('保存成功')
    dialogVisible.value = false
    await loadData()
  } finally {
    saving.value = false
  }
}

async function remove(row: DemoApprovalOrderItem) {
  await ElMessageBox.confirm(`确定删除 Demo 审批单“${row.orderNo}”吗？`, '确认删除')
  await deleteDemoApprovalOrder(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

async function submit(row: DemoApprovalOrderItem) {
  await ElMessageBox.confirm(`确定提交“${row.orderNo}”进入审批吗？`, '提交审批')
  await submitDemoApprovalOrder(row.id, { remark: '提交审批' })
  ElMessage.success('提交成功')
  await loadData()
}

async function cancel(row: DemoApprovalOrderItem) {
  await ElMessageBox.confirm(`确定取消 Demo 审批单“${row.orderNo}”吗？`, '确认取消')
  await cancelDemoApprovalOrder(row.id, '取消')
  ElMessage.success('取消成功')
  await loadData()
}

function openWithdraw(row: DemoApprovalOrderItem) {
  current.value = row
  withdrawForm.comment = ''
  withdrawVisible.value = true
}

async function withdraw() {
  await withdrawFormRef.value?.validate()
  if (!current.value) {
    return
  }

  await withdrawDemoApprovalOrder(current.value.id, withdrawForm.comment)
  ElMessage.success('撤回成功')
  withdrawVisible.value = false
  await loadData()
}

function view(row: DemoApprovalOrderItem) {
  void router.push(`/demo/approval-order/${row.id}`)
}

function canEdit(row: DemoApprovalOrderItem) {
  return row.approvalStatus === ApprovalStatus.Draft ||
    row.approvalStatus === ApprovalStatus.Rejected ||
    row.approvalStatus === ApprovalStatus.Withdrawn
}

function canDelete(row: DemoApprovalOrderItem) {
  return row.approvalStatus === ApprovalStatus.Draft
}

function canCancel(row: DemoApprovalOrderItem) {
  return row.approvalStatus === ApprovalStatus.Draft
}

function canSubmit(row: DemoApprovalOrderItem) {
  return canEdit(row)
}

function canWithdraw(row: DemoApprovalOrderItem) {
  return row.approvalStatus === ApprovalStatus.Pending
}

function statusText(status: ApprovalStatusValue) {
  const map: Record<ApprovalStatusValue, string> = {
    [ApprovalStatus.Draft]: '草稿',
    [ApprovalStatus.Pending]: '审批中',
    [ApprovalStatus.Approved]: '已通过',
    [ApprovalStatus.Rejected]: '已驳回',
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

loadDepartments()
loadData()
</script>

<template>
  <PageContainer title="Demo 审批单" description="用于验证业务单据接入审批流的轻量示例。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="单据编号 / 标题 / 申请人" @keyup.enter="resetPageAndLoad" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.approvalStatus" clearable placeholder="审批状态" style="width: 140px">
          <el-option label="草稿" :value="ApprovalStatus.Draft" />
          <el-option label="审批中" :value="ApprovalStatus.Pending" />
          <el-option label="已通过" :value="ApprovalStatus.Approved" />
          <el-option label="已驳回" :value="ApprovalStatus.Rejected" />
          <el-option label="已撤回" :value="ApprovalStatus.Withdrawn" />
          <el-option label="已取消" :value="ApprovalStatus.Cancelled" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'demo-approval-order:view'" type="primary" @click="resetPageAndLoad">查询</el-button>
        <el-button v-permission="'demo-approval-order:create'" @click="openCreate">新增</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="orderNo" label="单据编号" min-width="150" />
      <el-table-column prop="title" label="标题" min-width="180" show-overflow-tooltip />
      <el-table-column prop="amount" label="金额" width="120" />
      <el-table-column prop="applicantUserName" label="申请人" width="120" />
      <el-table-column label="审批状态" width="110">
        <template #default="{ row }">
          <el-tag :type="statusType(row.approvalStatus)">{{ statusText(row.approvalStatus) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="创建时间" width="180">
        <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="290" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'demo-approval-order:view'" link type="primary" @click="view(row)">查看</el-button>
          <el-button v-if="canEdit(row)" v-permission="'demo-approval-order:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button v-if="canDelete(row)" v-permission="'demo-approval-order:delete'" link type="danger" @click="remove(row)">删除</el-button>
          <el-button v-if="canCancel(row)" v-permission="'demo-approval-order:cancel'" link type="warning" @click="cancel(row)">取消</el-button>
          <el-button v-if="canSubmit(row)" v-permission="'demo-approval-order:submit'" link type="success" @click="submit(row)">提交审批</el-button>
          <el-button v-if="canWithdraw(row)" v-permission="'demo-approval-order:withdraw'" link type="warning" @click="openWithdraw(row)">撤回</el-button>
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

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑 Demo 审批单' : '新增 Demo 审批单'" width="620px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="100px">
        <el-form-item v-if="editingId" label="单据编号" prop="orderNo">
          <el-input v-model="form.orderNo" :disabled="Boolean(editingId)" />
        </el-form-item>
        <el-form-item label="标题" prop="title">
          <el-input v-model="form.title" />
        </el-form-item>
        <el-form-item label="金额" prop="amount">
          <el-input-number v-model="form.amount" :min="0" :precision="2" class="full-width" />
        </el-form-item>
        <el-form-item label="申请部门">
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

    <el-dialog v-model="withdrawVisible" title="撤回审批" width="520px">
      <el-form ref="withdrawFormRef" :model="withdrawForm" :rules="withdrawRules" label-width="90px">
        <el-form-item label="撤回原因" prop="comment">
          <el-input v-model="withdrawForm.comment" type="textarea" :rows="4" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="withdrawVisible = false">取消</el-button>
        <el-button type="primary" @click="withdraw">确定</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.full-width {
  width: 100%;
}
</style>
