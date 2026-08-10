<script setup lang="ts">
defineOptions({
  name: 'SecurityIpRule',
})

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { reactive, ref } from 'vue'
import {
  createIpAccessRule,
  deleteIpAccessRule,
  getIpAccessRules,
  updateIpAccessRule,
  type IpAccessRuleItem,
  type SaveIpAccessRuleRequest,
} from '../../../api/security'
import PageContainer from '../../../components/PageContainer/index.vue'
import SensitiveVerificationDialog from '../../../components/SensitiveVerificationDialog/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const editingId = ref('')
const editingRule = ref<IpAccessRuleItem | null>(null)
const formRef = ref<FormInstance>()
const sensitiveVerificationRef = ref<InstanceType<typeof SensitiveVerificationDialog>>()
const tableData = ref<IpAccessRuleItem[]>([])
const total = ref(0)

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  ruleType: '',
  isEnabled: undefined as boolean | undefined,
})

const form = reactive<SaveIpAccessRuleRequest>({
  ruleType: 'Blacklist',
  ipPattern: '',
  description: '',
  isEnabled: true,
})

const rules: FormRules = {
  ruleType: [{ required: true, message: '请选择规则类型', trigger: 'change' }],
  ipPattern: [{ required: true, message: '请输入 IP 规则', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getIpAccessRules(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = ''
  editingRule.value = null
  Object.assign(form, {
    ruleType: 'Blacklist',
    ipPattern: '',
    description: '',
    isEnabled: true,
  })
  dialogVisible.value = true
}

function openEdit(row: IpAccessRuleItem) {
  editingId.value = row.id
  editingRule.value = row
  Object.assign(form, {
    ruleType: row.ruleType,
    ipPattern: row.ipPattern,
    description: row.description ?? '',
    isEnabled: row.isEnabled,
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  const stepUpTicket = await requestSensitiveVerification(
    editingId.value ? 'security:ip-rule:update' : 'security:ip-rule:create',
  )
  saving.value = true
  try {
    if (editingId.value) {
      await updateIpAccessRule(editingId.value, {
        ...form,
        concurrencyToken: editingRule.value?.concurrencyToken,
      }, stepUpTicket)
    } else {
      await createIpAccessRule(form, stepUpTicket)
    }

    ElMessage.success('保存成功')
    dialogVisible.value = false
    await loadData()
  } finally {
    saving.value = false
  }
}

async function remove(row: IpAccessRuleItem) {
  await ElMessageBox.confirm(`确认删除 IP 规则 ${row.ipPattern}？`, '确认删除')
  const stepUpTicket = await requestSensitiveVerification('security:ip-rule:delete')
  await deleteIpAccessRule(row.id, stepUpTicket)
  ElMessage.success('删除成功')
  await loadData()
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    ruleType: '',
    isEnabled: undefined,
  })
  loadData()
}

async function requestSensitiveVerification(operationCode: string) {
  const code = await sensitiveVerificationRef.value?.open(operationCode)
  if (!code) {
    throw new Error('Sensitive operation verification was cancelled.')
  }

  return code
}

loadData()
</script>

<template>
  <PageContainer title="IP 黑白名单" description="维护请求入口的 IP 白名单和黑名单规则。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="IP / 描述" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.ruleType" clearable placeholder="类型" style="width: 130px">
          <el-option label="白名单" value="Whitelist" />
          <el-option label="黑名单" value="Blacklist" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isEnabled" clearable placeholder="状态" style="width: 130px">
          <el-option label="启用" :value="true" />
          <el-option label="禁用" :value="false" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
        <el-button v-permission="'security:ip-rule:create'" @click="openCreate">新增</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="ruleType" label="类型" width="120">
        <template #default="{ row }">
          <el-tag :type="row.ruleType === 'Whitelist' ? 'success' : 'danger'">
            {{ row.ruleType === 'Whitelist' ? '白名单' : '黑名单' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="ipPattern" label="IP 规则" min-width="180" />
      <el-table-column prop="description" label="描述" min-width="220" show-overflow-tooltip />
      <el-table-column prop="isEnabled" label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" min-width="180" />
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'security:ip-rule:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button v-permission="'security:ip-rule:delete'" link type="danger" @click="remove(row)">删除</el-button>
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

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑 IP 规则' : '新增 IP 规则'" width="520px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
        <el-form-item label="规则类型" prop="ruleType">
          <el-radio-group v-model="form.ruleType">
            <el-radio-button label="Whitelist">白名单</el-radio-button>
            <el-radio-button label="Blacklist">黑名单</el-radio-button>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="IP 规则" prop="ipPattern">
          <el-input v-model="form.ipPattern" placeholder="192.168.1.*" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="form.description" type="textarea" :rows="3" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="form.isEnabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">保存</el-button>
      </template>
    </el-dialog>

    <SensitiveVerificationDialog ref="sensitiveVerificationRef" />
  </PageContainer>
</template>
