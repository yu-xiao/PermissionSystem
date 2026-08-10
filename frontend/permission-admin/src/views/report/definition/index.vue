<script setup lang="ts">
defineOptions({
  name: 'ReportDefinition',
})

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { onMounted, reactive, ref } from 'vue'
import {
  createReport,
  deleteReport,
  getReport,
  getReportDatasets,
  getReportExecutionLogs,
  getReports,
  updateReport,
  type ReportDefinitionItem,
  type ReportDatasetItem,
  type ReportExecutionLogItem,
  type ReportQueryParam,
} from '../../../api/report'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const activeTab = ref('definitions')
const loading = ref(false)
const logLoading = ref(false)
const dialogVisible = ref(false)
const editingRow = ref<ReportDefinitionItem>()
const formRef = ref<FormInstance>()
const tableData = ref<ReportDefinitionItem[]>([])
const total = ref(0)
const logs = ref<ReportExecutionLogItem[]>([])
const logTotal = ref(0)
const datasets = ref<ReportDatasetItem[]>([])

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  category: '',
  dataSourceType: '',
  isEnabled: undefined as boolean | undefined,
})

const logQuery = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  reportCode: '',
  executeUserName: '',
})

const form = reactive({
  reportCode: '',
  reportName: '',
  category: 'System',
  dataSourceType: 'Sql',
  datasetKey: '',
  apiUrl: '',
  columnsJson: '',
  paramsJson: '',
  isEnabled: true,
  remark: '',
  queryParams: [] as ReportQueryParam[],
})

const rules: FormRules = {
  reportCode: [{ required: true, message: '请输入报表编码', trigger: 'blur' }],
  reportName: [{ required: true, message: '请输入报表名称', trigger: 'blur' }],
  category: [{ required: true, message: '请输入分类', trigger: 'blur' }],
  dataSourceType: [{ required: true, message: '请选择数据源类型', trigger: 'change' }],
  datasetKey: [{ required: true, message: '请选择数据集', trigger: 'change' }],
}

const defaultColumnsJson = `[
  {"key":"UserName","title":"用户名","width":"140"},
  {"key":"DisplayName","title":"显示名称","width":"160"},
  {"key":"CreatedAt","title":"创建时间","width":"180"}
]`

async function loadData() {
  loading.value = true
  try {
    const result = await getReports({
      ...query,
      keyword: query.keyword || undefined,
      category: query.category || undefined,
      dataSourceType: query.dataSourceType || undefined,
    })
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function loadLogs() {
  logLoading.value = true
  try {
    const result = await getReportExecutionLogs({
      ...logQuery,
      keyword: logQuery.keyword || undefined,
      reportCode: logQuery.reportCode || undefined,
      executeUserName: logQuery.executeUserName || undefined,
    })
    logs.value = result.items
    logTotal.value = result.totalCount
  } finally {
    logLoading.value = false
  }
}

async function loadDatasets() {
  datasets.value = await getReportDatasets()
}

function openCreate() {
  editingRow.value = undefined
  Object.assign(form, {
    reportCode: '',
    reportName: '',
    category: 'System',
    dataSourceType: 'Sql',
    datasetKey: '',
    apiUrl: '',
    columnsJson: defaultColumnsJson,
    paramsJson: '{}',
    isEnabled: true,
    remark: '',
    queryParams: [],
  })
  dialogVisible.value = true
}

async function openEdit(row: ReportDefinitionItem) {
  const detail = await getReport(row.id)
  editingRow.value = detail
  Object.assign(form, {
    reportCode: detail.reportCode,
    reportName: detail.reportName,
    category: detail.category,
    dataSourceType: detail.dataSourceType,
    datasetKey: detail.datasetKey ?? '',
    apiUrl: detail.apiUrl ?? '',
    columnsJson: detail.columnsJson ?? '',
    paramsJson: detail.paramsJson ?? '',
    isEnabled: detail.isEnabled,
    remark: detail.remark ?? '',
    queryParams: detail.queryParams.map((item) => ({ ...item })),
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  const payload = {
    reportName: form.reportName.trim(),
    category: form.category.trim(),
    dataSourceType: form.dataSourceType,
    datasetKey: form.datasetKey || undefined,
    apiUrl: form.apiUrl.trim() || undefined,
    columnsJson: form.columnsJson.trim() || undefined,
    paramsJson: form.paramsJson.trim() || undefined,
    isEnabled: form.isEnabled,
    remark: form.remark.trim() || undefined,
    queryParams: form.queryParams.map((item, index) => ({
      paramCode: item.paramCode.trim(),
      paramName: item.paramName.trim(),
      paramType: item.paramType,
      defaultValue: item.defaultValue?.trim() || undefined,
      required: item.required,
      sort: item.sort || index + 1,
    })),
  }

  if (editingRow.value) {
    await updateReport(editingRow.value.id, payload)
  } else {
    await createReport({
      reportCode: form.reportCode.trim(),
      ...payload,
    })
  }

  ElMessage.success('保存成功')
  dialogVisible.value = false
  await loadData()
}

async function remove(row: ReportDefinitionItem) {
  await ElMessageBox.confirm(`确认删除报表 ${row.reportName}？`, '确认删除')
  await deleteReport(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

function addParam() {
  form.queryParams.push({
    paramCode: '',
    paramName: '',
    paramType: 'String',
    defaultValue: '',
    required: false,
    sort: form.queryParams.length + 1,
  })
}

function removeParam(index: number) {
  form.queryParams.splice(index, 1)
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    category: '',
    dataSourceType: '',
    isEnabled: undefined,
  })
  loadData()
}

function resetLogQuery() {
  Object.assign(logQuery, {
    pageIndex: 1,
    keyword: '',
    reportCode: '',
    executeUserName: '',
  })
  loadLogs()
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

onMounted(() => {
  loadData()
  loadLogs()
  loadDatasets()
})
</script>

<template>
  <PageContainer title="报表管理" description="维护通用报表定义、查询参数和执行日志。">
    <template #actions>
      <TableToolbar @refresh="activeTab === 'definitions' ? loadData() : loadLogs()" />
    </template>

    <el-tabs v-model="activeTab">
      <el-tab-pane label="报表定义" name="definitions">
        <el-form class="toolbar" inline @submit.prevent>
          <el-form-item>
            <el-input v-model="query.keyword" clearable placeholder="编码 / 名称 / 分类" @keyup.enter="loadData" />
          </el-form-item>
          <el-form-item>
            <el-input v-model="query.category" clearable placeholder="分类" style="width: 140px" />
          </el-form-item>
          <el-form-item>
            <el-select v-model="query.dataSourceType" clearable placeholder="数据源" style="width: 120px">
              <el-option label="SQL" value="Sql" />
              <el-option label="API" value="Api" />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-select v-model="query.isEnabled" clearable placeholder="状态" style="width: 120px">
              <el-option label="启用" :value="true" />
              <el-option label="禁用" :value="false" />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-button v-permission="'report:definition:view'" type="primary" @click="loadData">查询</el-button>
            <el-button @click="resetQuery">重置</el-button>
            <el-button v-permission="'report:definition:create'" @click="openCreate">新增</el-button>
          </el-form-item>
        </el-form>

        <el-table v-loading="loading" :data="tableData" border>
          <el-table-column prop="reportCode" label="报表编码" min-width="160" show-overflow-tooltip />
          <el-table-column prop="reportName" label="报表名称" min-width="180" show-overflow-tooltip />
          <el-table-column prop="category" label="分类" width="120" />
          <el-table-column prop="dataSourceType" label="数据源" width="100" />
          <el-table-column label="状态" width="90">
            <template #default="{ row }">
              <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="remark" label="备注" min-width="220" show-overflow-tooltip />
          <el-table-column label="创建时间" width="180">
            <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
          </el-table-column>
          <el-table-column label="操作" width="150" fixed="right">
            <template #default="{ row }">
              <el-button v-permission="'report:definition:update'" link type="primary" @click="openEdit(row)">
                编辑
              </el-button>
              <el-button v-permission="'report:definition:delete'" link type="danger" @click="remove(row)">
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
      </el-tab-pane>

      <el-tab-pane label="执行日志" name="logs">
        <el-form class="toolbar" inline @submit.prevent>
          <el-form-item>
            <el-input v-model="logQuery.reportCode" clearable placeholder="报表编码" />
          </el-form-item>
          <el-form-item>
            <el-input v-model="logQuery.executeUserName" clearable placeholder="执行人" />
          </el-form-item>
          <el-form-item>
            <el-button v-permission="'report:log:view'" type="primary" @click="loadLogs">查询</el-button>
            <el-button @click="resetLogQuery">重置</el-button>
          </el-form-item>
        </el-form>

        <el-table v-loading="logLoading" :data="logs" border>
          <el-table-column prop="reportCode" label="报表编码" min-width="160" show-overflow-tooltip />
          <el-table-column prop="executeUserName" label="执行人" width="140" />
          <el-table-column label="结果" width="90">
            <template #default="{ row }">
              <el-tag :type="row.isSuccess ? 'success' : 'danger'">{{ row.isSuccess ? '成功' : '失败' }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="rowCount" label="行数" width="90" />
          <el-table-column prop="elapsedMilliseconds" label="耗时(ms)" width="110" />
          <el-table-column prop="failureReason" label="失败原因" min-width="220" show-overflow-tooltip />
          <el-table-column prop="paramsJson" label="参数" min-width="220" show-overflow-tooltip />
          <el-table-column label="执行时间" width="180">
            <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
          </el-table-column>
        </el-table>

        <el-pagination
          v-model:current-page="logQuery.pageIndex"
          v-model:page-size="logQuery.pageSize"
          class="pager"
          background
          layout="total, sizes, prev, pager, next"
          :total="logTotal"
          @change="loadLogs"
        />
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="dialogVisible" :title="editingRow ? '编辑报表' : '新增报表'" width="920px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
        <el-row :gutter="16">
          <el-col :xs="24" :md="12">
            <el-form-item label="报表编码" prop="reportCode">
              <el-input v-model="form.reportCode" :disabled="Boolean(editingRow)" placeholder="SystemUserList" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :md="12">
            <el-form-item label="报表名称" prop="reportName">
              <el-input v-model="form.reportName" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :md="12">
            <el-form-item label="分类" prop="category">
              <el-input v-model="form.category" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :md="12">
            <el-form-item label="数据源" prop="dataSourceType">
              <el-select v-model="form.dataSourceType" class="full-width">
                <el-option label="SQL" value="Sql" />
                <el-option label="API" value="Api" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col v-if="form.dataSourceType === 'Sql'" :xs="24">
            <el-form-item label="数据集" prop="datasetKey">
              <el-select v-model="form.datasetKey" class="full-width" placeholder="选择已审核的数据集">
                <el-option v-for="dataset in datasets" :key="dataset.key" :label="dataset.name" :value="dataset.key" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :xs="24">
            <el-form-item label="API URL">
              <el-input v-model="form.apiUrl" placeholder="预留 API 数据源地址" />
            </el-form-item>
          </el-col>
          <el-col :xs="24">
            <el-form-item label="列配置 JSON">
              <el-input v-model="form.columnsJson" type="textarea" :rows="5" />
            </el-form-item>
          </el-col>
          <el-col :xs="24">
            <el-form-item label="参数 JSON">
              <el-input v-model="form.paramsJson" type="textarea" :rows="3" />
            </el-form-item>
          </el-col>
          <el-col :xs="24">
            <el-form-item label="查询参数">
              <div class="param-editor">
                <el-table :data="form.queryParams" border>
                  <el-table-column label="编码" min-width="140">
                    <template #default="{ row }">
                      <el-input v-model="row.paramCode" />
                    </template>
                  </el-table-column>
                  <el-table-column label="名称" min-width="140">
                    <template #default="{ row }">
                      <el-input v-model="row.paramName" />
                    </template>
                  </el-table-column>
                  <el-table-column label="类型" width="130">
                    <template #default="{ row }">
                      <el-select v-model="row.paramType">
                        <el-option label="String" value="String" />
                        <el-option label="Int" value="Int" />
                        <el-option label="Decimal" value="Decimal" />
                        <el-option label="DateTime" value="DateTime" />
                        <el-option label="Bool" value="Bool" />
                        <el-option label="Guid" value="Guid" />
                      </el-select>
                    </template>
                  </el-table-column>
                  <el-table-column label="默认值" min-width="140">
                    <template #default="{ row }">
                      <el-input v-model="row.defaultValue" />
                    </template>
                  </el-table-column>
                  <el-table-column label="必填" width="80">
                    <template #default="{ row }">
                      <el-switch v-model="row.required" />
                    </template>
                  </el-table-column>
                  <el-table-column label="排序" width="100">
                    <template #default="{ row }">
                      <el-input-number v-model="row.sort" :min="0" />
                    </template>
                  </el-table-column>
                  <el-table-column label="操作" width="80">
                    <template #default="{ $index }">
                      <el-button link type="danger" @click="removeParam($index)">删除</el-button>
                    </template>
                  </el-table-column>
                </el-table>
                <el-button class="param-add" @click="addParam">新增参数</el-button>
              </div>
            </el-form-item>
          </el-col>
          <el-col :xs="24" :md="12">
            <el-form-item label="启用">
              <el-switch v-model="form.isEnabled" />
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
  </PageContainer>
</template>

<style scoped>
.full-width,
.param-editor {
  width: 100%;
}

.param-add {
  margin-top: 10px;
}
</style>
