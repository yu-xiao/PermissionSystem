<script setup lang="ts">
defineOptions({
  name: 'SystemNumberRule',
})

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import {
  createNumberRule,
  deleteNumberRule,
  disableNumberRule,
  enableNumberRule,
  generateNumber,
  getNumberRules,
  previewNumberRule,
  resetNumberSequence,
  updateNumberRule,
  type CreateOrUpdateNumberRuleRequest,
  type NumberRuleItem,
  type NumberRuleResetCycle,
} from '../../../api/numberRule'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const loading = ref(false)
const tableData = ref<NumberRuleItem[]>([])
const total = ref(0)
const dialogVisible = ref(false)
const formRef = ref<FormInstance>()
const editingRow = ref<NumberRuleItem>()
const previewResult = ref('')
const previewPattern = ref('')

const resetCycleOptions: Array<{ label: string; value: NumberRuleResetCycle }> = [
  { label: '不重置', value: 'None' },
  { label: '每日', value: 'Daily' },
  { label: '每月', value: 'Monthly' },
  { label: '每年', value: 'Yearly' },
]

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  businessType: '',
  isEnabled: undefined as boolean | undefined,
})

const form = reactive<CreateOrUpdateNumberRuleRequest>({
  ruleCode: '',
  ruleName: '',
  businessType: '',
  prefix: '',
  dateFormat: 'yyyyMMdd',
  sequenceLength: 4,
  resetCycle: 'Daily',
  separator: '',
  isEnabled: true,
  remark: '',
})

const dialogTitle = computed(() => (editingRow.value ? '编辑编号规则' : '新增编号规则'))

const rules: FormRules = {
  ruleCode: [{ required: true, message: '请输入规则编码', trigger: 'blur' }],
  ruleName: [{ required: true, message: '请输入规则名称', trigger: 'blur' }],
  businessType: [{ required: true, message: '请输入业务类型', trigger: 'blur' }],
  dateFormat: [{ required: true, message: '请输入日期格式', trigger: 'blur' }],
  sequenceLength: [{ required: true, message: '请输入流水位数', trigger: 'change' }],
  resetCycle: [{ required: true, message: '请选择重置周期', trigger: 'change' }],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getNumberRules(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    businessType: '',
    isEnabled: undefined,
  })
  loadData()
}

function openCreate() {
  editingRow.value = undefined
  clearResult()
  Object.assign(form, {
    ruleCode: '',
    ruleName: '',
    businessType: '',
    prefix: '',
    dateFormat: 'yyyyMMdd',
    sequenceLength: 4,
    resetCycle: 'Daily',
    separator: '',
    isEnabled: true,
    remark: '',
  })
  dialogVisible.value = true
}

function openEdit(row: NumberRuleItem) {
  editingRow.value = row
  clearResult()
  Object.assign(form, {
    ruleCode: row.ruleCode,
    ruleName: row.ruleName,
    businessType: row.businessType,
    prefix: row.prefix,
    dateFormat: row.dateFormat,
    sequenceLength: row.sequenceLength,
    resetCycle: row.resetCycle,
    separator: row.separator,
    isEnabled: row.isEnabled,
    remark: row.remark ?? '',
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  const payload = buildPayload()

  if (editingRow.value) {
    await updateNumberRule(editingRow.value.id, {
      ...payload,
      concurrencyToken: editingRow.value.concurrencyToken,
    })
  } else {
    await createNumberRule(payload)
  }

  ElMessage.success('保存成功')
  dialogVisible.value = false
  await loadData()
}

async function previewCurrentRule() {
  await formRef.value?.validate()
  const result = await previewNumberRule(buildPayload())
  previewResult.value = result.number
  previewPattern.value = result.pattern
}

async function remove(row: NumberRuleItem) {
  await ElMessageBox.confirm(`确认删除编号规则 ${row.ruleCode}？`, '确认删除')
  await deleteNumberRule(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

async function toggleStatus(row: NumberRuleItem) {
  if (row.isEnabled) {
    await disableNumberRule(row.id)
    ElMessage.success('已禁用')
  } else {
    await enableNumberRule(row.id)
    ElMessage.success('已启用')
  }
  await loadData()
}

async function testGenerate(row: NumberRuleItem) {
  const result = await generateNumber(row.ruleCode)
  ElMessageBox.alert(result.number, `测试生成：${row.ruleCode}`, {
    confirmButtonText: '知道了',
  })
}

async function resetSequence(row: NumberRuleItem) {
  await ElMessageBox.confirm(`确认重置编号规则 ${row.ruleCode} 的流水号？`, '确认重置', {
    type: 'warning',
  })
  await resetNumberSequence(row.ruleCode)
  ElMessage.success('流水号已重置')
}

function buildPayload(): CreateOrUpdateNumberRuleRequest {
  return {
    ruleCode: form.ruleCode.trim(),
    ruleName: form.ruleName.trim(),
    businessType: form.businessType.trim(),
    prefix: form.prefix.trim(),
    dateFormat: form.dateFormat.trim(),
    sequenceLength: form.sequenceLength,
    resetCycle: form.resetCycle,
    separator: form.separator,
    isEnabled: form.isEnabled,
    remark: form.remark?.trim(),
  }
}

function clearResult() {
  previewResult.value = ''
  previewPattern.value = ''
}

function resetCycleLabel(value: NumberRuleResetCycle) {
  return resetCycleOptions.find((item) => item.value === value)?.label ?? value
}

loadData()
</script>

<template>
  <PageContainer title="编号规则" description="维护平台通用业务编号规则、重置周期和流水号生成策略。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="规则编码 / 名称 / 前缀" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.businessType" clearable placeholder="业务类型" style="width: 160px" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isEnabled" clearable placeholder="状态" style="width: 120px">
          <el-option label="启用" :value="true" />
          <el-option label="禁用" :value="false" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:number-rule:view'" type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
        <el-button v-permission="'system:number-rule:create'" @click="openCreate">新增</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="ruleCode" label="规则编码" min-width="150" show-overflow-tooltip />
      <el-table-column prop="ruleName" label="规则名称" min-width="160" show-overflow-tooltip />
      <el-table-column prop="businessType" label="业务类型" min-width="150" show-overflow-tooltip />
      <el-table-column prop="prefix" label="前缀" width="100" />
      <el-table-column prop="dateFormat" label="日期格式" width="120" />
      <el-table-column prop="sequenceLength" label="流水位数" width="96" />
      <el-table-column prop="resetCycle" label="重置周期" width="100">
        <template #default="{ row }">
          {{ resetCycleLabel(row.resetCycle) }}
        </template>
      </el-table-column>
      <el-table-column prop="separator" label="分隔符" width="88">
        <template #default="{ row }">
          {{ row.separator || '-' }}
        </template>
      </el-table-column>
      <el-table-column prop="isEnabled" label="状态" width="92">
        <template #default="{ row }">
          <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="remark" label="备注" min-width="180" show-overflow-tooltip />
      <el-table-column label="操作" width="300" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:number-rule:update'" link type="primary" @click="openEdit(row)">
            编辑
          </el-button>
          <el-button
            v-permission="row.isEnabled ? 'system:number-rule:disable' : 'system:number-rule:enable'"
            link
            type="primary"
            @click="toggleStatus(row)"
          >
            {{ row.isEnabled ? '禁用' : '启用' }}
          </el-button>
          <el-button v-permission="'system:number-rule:generate'" link type="primary" @click="testGenerate(row)">
            测试生成
          </el-button>
          <el-button v-permission="'system:number-rule:reset'" link type="warning" @click="resetSequence(row)">
            重置流水
          </el-button>
          <el-button v-permission="'system:number-rule:delete'" link type="danger" @click="remove(row)">
            删除
          </el-button>
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

    <el-dialog v-model="dialogVisible" :title="dialogTitle" width="720px" @closed="clearResult">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="规则编码" prop="ruleCode">
              <el-input v-model="form.ruleCode" :disabled="Boolean(editingRow)" placeholder="PurchaseOrder" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="规则名称" prop="ruleName">
              <el-input v-model="form.ruleName" placeholder="采购订单编号" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="业务类型" prop="businessType">
              <el-input v-model="form.businessType" placeholder="PurchaseOrder" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="前缀">
              <el-input v-model="form.prefix" placeholder="PO" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="日期格式" prop="dateFormat">
              <el-input v-model="form.dateFormat" placeholder="yyyyMMdd" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="流水位数" prop="sequenceLength">
              <el-input-number v-model="form.sequenceLength" :min="1" :max="18" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="重置周期" prop="resetCycle">
              <el-select v-model="form.resetCycle">
                <el-option
                  v-for="item in resetCycleOptions"
                  :key="item.value"
                  :label="item.label"
                  :value="item.value"
                />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="分隔符">
              <el-input v-model="form.separator" maxlength="8" placeholder="可留空" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="是否启用">
          <el-switch v-model="form.isEnabled" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.remark" type="textarea" :rows="3" />
        </el-form-item>
        <el-alert v-if="previewResult" class="preview-alert" type="success" :closable="false">
          <template #title>
            <span>预览编号：{{ previewResult }}</span>
            <span class="preview-pattern">规则：{{ previewPattern }}</span>
          </template>
        </el-alert>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button v-permission="'system:number-rule:preview'" @click="previewCurrentRule">预览</el-button>
        <el-button type="primary" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.preview-alert {
  margin-top: 8px;
}

.preview-pattern {
  margin-left: 16px;
  color: var(--el-text-color-secondary);
}
</style>
