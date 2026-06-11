<script setup lang="ts">
defineOptions({
  name: 'SystemPrintTemplate',
})

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { MoreFilled } from '@element-plus/icons-vue'
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  createPrintTemplate,
  deletePrintTemplate,
  getPrintRecords,
  getPrintTemplates,
  previewPrintTemplate,
  renderPrintTemplate,
  setDefaultPrintTemplate,
  updatePrintTemplate,
  type PrintRecordItem,
  type PrintTemplateItem,
} from '../../../api/printTemplate'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'
import { useAuthStore } from '../../../stores/auth'

const router = useRouter()
const authStore = useAuthStore()
const activeTab = ref('templates')
const loading = ref(false)
const recordLoading = ref(false)
const dialogVisible = ref(false)
const previewDialogVisible = ref(false)
const editingRow = ref<PrintTemplateItem>()
const formRef = ref<FormInstance>()
const tableData = ref<PrintTemplateItem[]>([])
const total = ref(0)
const records = ref<PrintRecordItem[]>([])
const recordTotal = ref(0)
const previewTitle = ref('模板预览')
const previewHtml = ref('')

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  businessType: '',
  templateType: '',
  isEnabled: undefined as boolean | undefined,
})

const recordQuery = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  businessType: '',
  businessId: '',
  templateId: '',
})

const form = reactive({
  templateCode: '',
  templateName: '',
  businessType: '',
  templateType: 'Document',
  contentHtml: '',
  contentJson: '',
  paperSize: 'A4',
  orientation: 'Portrait',
  isDefault: false,
  isEnabled: true,
  version: 1,
  remark: '',
})

const rules: FormRules = {
  templateCode: [{ required: true, message: '请输入模板编码', trigger: 'blur' }],
  templateName: [{ required: true, message: '请输入模板名称', trigger: 'blur' }],
  businessType: [{ required: true, message: '请输入业务类型', trigger: 'blur' }],
  templateType: [{ required: true, message: '请输入模板类型', trigger: 'blur' }],
  contentHtml: [{ required: true, message: '请输入模板 HTML', trigger: 'blur' }],
  paperSize: [{ required: true, message: '请选择纸张', trigger: 'change' }],
  orientation: [{ required: true, message: '请选择方向', trigger: 'change' }],
}

const sampleData = {
  OrderNo: 'PO202605260001',
  CreatedAt: '2026-05-26 10:30:00',
  ApplicantName: 'Admin',
  Amount: 1234.56,
  items: [
    { Name: 'Sample Item A', Qty: 2, Price: 100 },
    { Name: 'Sample Item B', Qty: 3, Price: 88.5 },
  ],
}

const defaultHtml = `<h1>{{OrderNo}}</h1>
<p>申请人：{{ApplicantName}}</p>
<p>创建时间：{{CreatedAt}}</p>
<p>金额：{{Amount}}</p>
<table border="1" cellspacing="0" cellpadding="6" style="width:100%;border-collapse:collapse;">
  <thead>
    <tr><th>名称</th><th>数量</th><th>单价</th></tr>
  </thead>
  <tbody>
    {{#items}}
    <tr><td>{{Name}}</td><td>{{Qty}}</td><td>{{Price}}</td></tr>
    {{/items}}
  </tbody>
</table>`

async function loadTemplates() {
  loading.value = true
  try {
    const result = await getPrintTemplates({
      ...query,
      keyword: query.keyword || undefined,
      businessType: query.businessType || undefined,
      templateType: query.templateType || undefined,
    })
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function loadRecords() {
  recordLoading.value = true
  try {
    const result = await getPrintRecords({
      ...recordQuery,
      keyword: recordQuery.keyword || undefined,
      businessType: recordQuery.businessType || undefined,
      businessId: recordQuery.businessId || undefined,
      templateId: recordQuery.templateId || undefined,
    })
    records.value = result.items
    recordTotal.value = result.totalCount
  } finally {
    recordLoading.value = false
  }
}

function openCreate() {
  editingRow.value = undefined
  Object.assign(form, {
    templateCode: '',
    templateName: '',
    businessType: '',
    templateType: 'Document',
    contentHtml: defaultHtml,
    contentJson: '',
    paperSize: 'A4',
    orientation: 'Portrait',
    isDefault: false,
    isEnabled: true,
    version: 1,
    remark: '',
  })
  dialogVisible.value = true
}

function openEdit(row: PrintTemplateItem) {
  editingRow.value = row
  Object.assign(form, {
    templateCode: row.templateCode,
    templateName: row.templateName,
    businessType: row.businessType,
    templateType: row.templateType,
    contentHtml: row.contentHtml,
    contentJson: row.contentJson ?? '',
    paperSize: row.paperSize,
    orientation: row.orientation,
    isDefault: row.isDefault,
    isEnabled: row.isEnabled,
    version: row.version,
    remark: row.remark ?? '',
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  const payload = {
    templateName: form.templateName.trim(),
    businessType: form.businessType.trim(),
    templateType: form.templateType.trim(),
    contentHtml: form.contentHtml.trim(),
    contentJson: form.contentJson.trim() || undefined,
    paperSize: form.paperSize,
    orientation: form.orientation,
    isDefault: form.isDefault,
    isEnabled: form.isEnabled,
    version: form.version,
    remark: form.remark.trim() || undefined,
  }

  if (editingRow.value) {
    await updatePrintTemplate(editingRow.value.id, payload)
  } else {
    await createPrintTemplate({
      templateCode: form.templateCode.trim(),
      ...payload,
    })
  }

  ElMessage.success('保存成功')
  dialogVisible.value = false
  await loadTemplates()
}

async function remove(row: PrintTemplateItem) {
  await ElMessageBox.confirm(`确认删除打印模板 ${row.templateName}？`, '确认删除')
  await deletePrintTemplate(row.id)
  ElMessage.success('删除成功')
  await loadTemplates()
}

async function setDefault(row: PrintTemplateItem) {
  await setDefaultPrintTemplate(row.id)
  ElMessage.success('默认模板已更新')
  await loadTemplates()
}

async function preview(row: PrintTemplateItem) {
  const result = await previewPrintTemplate(row.id, {
    businessId: 'preview',
    data: sampleData,
  })
  previewTitle.value = `${row.templateName} - 预览`
  previewHtml.value = result.html
  previewDialogVisible.value = true
}

async function renderTest(row: PrintTemplateItem) {
  const result = await renderPrintTemplate(row.id, {
    businessId: 'manual-preview',
    data: sampleData,
  })
  previewTitle.value = `${row.templateName} - 测试渲染`
  previewHtml.value = result.html
  previewDialogVisible.value = true
  ElMessage.success('测试渲染成功，已记录打印日志')
  await loadRecords()
}

function openDesigner(row: PrintTemplateItem) {
  router.push({
    path: `/system/print-templates/${row.id}/designer`,
  })
}

function hasMoreTemplateActions() {
  return (
    authStore.hasPermission('system:print-template:update') ||
    authStore.hasPermission('system:print-template:preview') ||
    authStore.hasPermission('system:print-template:print') ||
    authStore.hasPermission('system:print-template:delete')
  )
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    businessType: '',
    templateType: '',
    isEnabled: undefined,
  })
  loadTemplates()
}

function resetRecordQuery() {
  Object.assign(recordQuery, {
    pageIndex: 1,
    keyword: '',
    businessType: '',
    businessId: '',
    templateId: '',
  })
  loadRecords()
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

onMounted(() => {
  loadTemplates()
  loadRecords()
})
</script>

<template>
  <PageContainer title="打印模板" description="维护通用打印模板、模板预览和打印记录。">
    <template #actions>
      <TableToolbar @refresh="activeTab === 'templates' ? loadTemplates() : loadRecords()" />
    </template>

    <el-tabs v-model="activeTab">
      <el-tab-pane label="模板配置" name="templates">
        <el-form class="toolbar" inline @submit.prevent>
          <el-form-item>
            <el-input v-model="query.keyword" clearable placeholder="编码 / 名称 / 业务类型" @keyup.enter="loadTemplates" />
          </el-form-item>
          <el-form-item>
            <el-input v-model="query.businessType" clearable placeholder="业务类型" style="width: 160px" />
          </el-form-item>
          <el-form-item>
            <el-input v-model="query.templateType" clearable placeholder="模板类型" style="width: 140px" />
          </el-form-item>
          <el-form-item>
            <el-select v-model="query.isEnabled" clearable placeholder="状态" style="width: 120px">
              <el-option label="启用" :value="true" />
              <el-option label="禁用" :value="false" />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-button v-permission="'system:print-template:view'" type="primary" @click="loadTemplates">查询</el-button>
            <el-button @click="resetQuery">重置</el-button>
            <el-button v-permission="'system:print-template:create'" @click="openCreate">新增</el-button>
          </el-form-item>
        </el-form>

        <el-table v-loading="loading" :data="tableData" border>
          <el-table-column prop="templateCode" label="模板编码" min-width="150" show-overflow-tooltip />
          <el-table-column prop="templateName" label="模板名称" min-width="170" show-overflow-tooltip />
          <el-table-column prop="businessType" label="业务类型" min-width="160" show-overflow-tooltip />
          <el-table-column prop="templateType" label="模板类型" width="110" />
          <el-table-column label="纸张" width="120">
            <template #default="{ row }">{{ row.paperSize }} / {{ row.orientation === 'Landscape' ? '横向' : '纵向' }}</template>
          </el-table-column>
          <el-table-column prop="version" label="版本" width="80" />
          <el-table-column label="默认" width="80">
            <template #default="{ row }">
              <el-tag v-if="row.isDefault" type="success">默认</el-tag>
              <span v-else>-</span>
            </template>
          </el-table-column>
          <el-table-column label="状态" width="90">
            <template #default="{ row }">
              <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="创建时间" width="180">
            <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
          </el-table-column>
          <el-table-column label="操作" width="170" fixed="right">
            <template #default="{ row }">
              <div class="table-actions">
                <el-button v-permission="'system:print-template:design'" link type="primary" @click="openDesigner(row)">
                  设计
                </el-button>
                <el-dropdown v-if="hasMoreTemplateActions()" trigger="click">
                  <el-button link type="primary" :icon="MoreFilled">更多</el-button>
                  <template #dropdown>
                    <el-dropdown-menu>
                      <el-dropdown-item v-permission="'system:print-template:update'" @click="openEdit(row)">编辑</el-dropdown-item>
                      <el-dropdown-item v-permission="'system:print-template:update'" @click="setDefault(row)">设默认</el-dropdown-item>
                      <el-dropdown-item v-permission="'system:print-template:preview'" @click="preview(row)">预览</el-dropdown-item>
                      <el-dropdown-item v-permission="'system:print-template:print'" @click="renderTest(row)">测试</el-dropdown-item>
                      <el-dropdown-item v-permission="'system:print-template:delete'" divided @click="remove(row)">删除</el-dropdown-item>
                    </el-dropdown-menu>
                  </template>
                </el-dropdown>
              </div>
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
          @change="loadTemplates"
        />
      </el-tab-pane>

      <el-tab-pane label="打印记录" name="records">
        <el-form class="toolbar" inline @submit.prevent>
          <el-form-item>
            <el-input v-model="recordQuery.businessType" clearable placeholder="业务类型" />
          </el-form-item>
          <el-form-item>
            <el-input v-model="recordQuery.businessId" clearable placeholder="业务ID" />
          </el-form-item>
          <el-form-item>
            <el-input v-model="recordQuery.templateId" clearable placeholder="模板ID" />
          </el-form-item>
          <el-form-item>
            <el-button v-permission="'system:print-record:view'" type="primary" @click="loadRecords">查询</el-button>
            <el-button @click="resetRecordQuery">重置</el-button>
          </el-form-item>
        </el-form>

        <el-table v-loading="recordLoading" :data="records" border>
          <el-table-column prop="businessType" label="业务类型" min-width="150" />
          <el-table-column prop="businessId" label="业务ID" min-width="170" show-overflow-tooltip />
          <el-table-column prop="templateId" label="模板ID" min-width="220" show-overflow-tooltip />
          <el-table-column prop="printUserName" label="打印人" width="130" />
          <el-table-column prop="printCount" label="次数" width="80" />
          <el-table-column label="打印时间" width="180">
            <template #default="{ row }">{{ formatTime(row.printedAt) }}</template>
          </el-table-column>
        </el-table>

        <el-pagination
          v-model:current-page="recordQuery.pageIndex"
          v-model:page-size="recordQuery.pageSize"
          class="pager"
          background
          layout="total, sizes, prev, pager, next"
          :total="recordTotal"
          @change="loadRecords"
        />
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="dialogVisible" :title="editingRow ? '编辑打印模板' : '新增打印模板'" width="760px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
        <el-row :gutter="16">
          <el-col :xs="24" :md="12">
            <el-form-item label="模板编码" prop="templateCode">
              <el-input v-model="form.templateCode" :disabled="Boolean(editingRow)" placeholder="DemoApprovalOrderPrint" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :md="12">
            <el-form-item label="模板名称" prop="templateName">
              <el-input v-model="form.templateName" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :md="12">
            <el-form-item label="业务类型" prop="businessType">
              <el-input v-model="form.businessType" placeholder="DemoApprovalOrder" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :md="12">
            <el-form-item label="模板类型" prop="templateType">
              <el-input v-model="form.templateType" placeholder="Document / Label / Contract" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :md="12">
            <el-form-item label="纸张" prop="paperSize">
              <el-select v-model="form.paperSize" class="full-width">
                <el-option label="A4" value="A4" />
                <el-option label="A5" value="A5" />
                <el-option label="Label" value="Label" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :xs="24" :md="12">
            <el-form-item label="方向" prop="orientation">
              <el-select v-model="form.orientation" class="full-width">
                <el-option label="纵向" value="Portrait" />
                <el-option label="横向" value="Landscape" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :xs="24" :md="12">
            <el-form-item label="版本">
              <el-input-number v-model="form.version" :min="1" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :md="12">
            <el-form-item label="状态">
              <el-switch v-model="form.isEnabled" active-text="启用" inactive-text="禁用" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :md="12">
            <el-form-item label="默认模板">
              <el-switch v-model="form.isDefault" />
            </el-form-item>
          </el-col>
          <el-col :xs="24">
            <el-form-item label="模板 HTML" prop="contentHtml">
              <el-input v-model="form.contentHtml" type="textarea" :rows="8" />
            </el-form-item>
          </el-col>
          <el-col :xs="24">
            <el-form-item label="模板 JSON">
              <el-input v-model="form.contentJson" type="textarea" :rows="3" />
            </el-form-item>
          </el-col>
          <el-col :xs="24">
            <el-form-item label="备注">
              <el-input v-model="form.remark" type="textarea" :rows="2" />
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="save">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="previewDialogVisible" :title="previewTitle" width="900px">
      <iframe class="preview-frame" :srcdoc="previewHtml" />
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.full-width {
  width: 100%;
}

.preview-frame {
  background: #fff;
  border: 1px solid var(--el-border-color);
  min-height: 520px;
  width: 100%;
}
</style>
