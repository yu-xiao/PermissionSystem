<script setup lang="ts">
defineOptions({
  name: 'ReportViewer',
})

import { ElMessage } from 'element-plus'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import {
  exportReport,
  getReport,
  getReports,
  queryReport,
  type ReportColumn,
  type ReportDefinitionItem,
} from '../../../api/report'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const loading = ref(false)
const querying = ref(false)
const exporting = ref(false)
const reports = ref<ReportDefinitionItem[]>([])
const currentReport = ref<ReportDefinitionItem>()
const reportId = ref('')
const rows = ref<Record<string, unknown>[]>([])
const columns = ref<ReportColumn[]>([])
const summary = reactive({
  rowCount: 0,
  elapsedMilliseconds: 0,
})
const queryParams = reactive<Record<string, unknown>>({})

const enabledReports = computed(() =>
  reports.value.filter((item) => item.isEnabled && item.dataSourceType.toLowerCase() === 'sql'),
)

async function loadReports() {
  loading.value = true
  try {
    const result = await getReports({
      pageIndex: 1,
      pageSize: 200,
      isEnabled: true,
    })
    reports.value = result.items
    const firstAvailable = result.items.find(
      (item) => item.isEnabled && item.dataSourceType.toLowerCase() === 'sql',
    )
    if (!reportId.value && firstAvailable) {
      reportId.value = firstAvailable.id
    }
  } finally {
    loading.value = false
  }
}

async function loadReport(id: string) {
  if (!id) {
    currentReport.value = undefined
    return
  }

  currentReport.value = await getReport(id)
  Object.keys(queryParams).forEach((key) => delete queryParams[key])
  currentReport.value.queryParams.forEach((item) => {
    queryParams[item.paramCode] = item.defaultValue ?? ''
  })
  rows.value = []
  columns.value = []
  summary.rowCount = 0
  summary.elapsedMilliseconds = 0
}

async function executeQuery() {
  if (!currentReport.value) {
    ElMessage.warning('请选择报表')
    return
  }

  querying.value = true
  try {
    const result = await queryReport(currentReport.value.id, {
      params: normalizeParams(),
    })
    columns.value = result.columns
    rows.value = result.rows
    summary.rowCount = result.rowCount
    summary.elapsedMilliseconds = result.elapsedMilliseconds
  } finally {
    querying.value = false
  }
}

async function executeExport() {
  if (!currentReport.value) {
    ElMessage.warning('请选择报表')
    return
  }

  exporting.value = true
  try {
    const blob = await exportReport(currentReport.value.id, {
      params: normalizeParams(),
    })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `${currentReport.value.reportCode}-${new Date().toISOString().slice(0, 19).replace(/[-:T]/g, '')}.xlsx`
    anchor.click()
    URL.revokeObjectURL(url)
  } finally {
    exporting.value = false
  }
}

function normalizeParams() {
  const result: Record<string, unknown> = {}
  for (const [key, value] of Object.entries(queryParams)) {
    result[key] = value === '' ? null : value
  }

  return result
}

function formatCell(value: unknown) {
  if (value === null || value === undefined || value === '') {
    return '-'
  }

  if (typeof value === 'boolean') {
    return value ? '是' : '否'
  }

  return String(value)
}

function columnWidth(column: ReportColumn) {
  const parsed = Number.parseInt(column.width ?? '', 10)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : undefined
}

watch(reportId, (value) => {
  loadReport(value)
})

onMounted(loadReports)
</script>

<template>
  <PageContainer title="报表查看" description="选择报表，输入查询参数并导出 Excel。">
    <template #actions>
      <TableToolbar @refresh="loadReports" />
    </template>

    <div v-loading="loading">
      <el-form class="toolbar" inline @submit.prevent>
        <el-form-item label="报表">
          <el-select v-model="reportId" filterable placeholder="请选择报表" style="width: 280px">
            <el-option
              v-for="item in enabledReports"
              :key="item.id"
              :label="`${item.reportName} (${item.reportCode})`"
              :value="item.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item
          v-for="param in currentReport?.queryParams ?? []"
          :key="param.paramCode"
          :label="param.paramName"
        >
          <el-date-picker
            v-if="param.paramType.toLowerCase().includes('date')"
            v-model="queryParams[param.paramCode]"
            type="datetime"
            value-format="YYYY-MM-DDTHH:mm:ssZ"
            style="width: 190px"
          />
          <el-input-number
            v-else-if="['int', 'integer', 'decimal', 'number'].includes(param.paramType.toLowerCase())"
            v-model="queryParams[param.paramCode]"
          />
          <el-switch
            v-else-if="['bool', 'boolean'].includes(param.paramType.toLowerCase())"
            v-model="queryParams[param.paramCode]"
          />
          <el-input v-else v-model="queryParams[param.paramCode]" clearable style="width: 180px" />
        </el-form-item>
        <el-form-item>
          <el-button v-permission="'report:view'" type="primary" :loading="querying" @click="executeQuery">查询</el-button>
          <el-button v-permission="'report:export'" :loading="exporting" @click="executeExport">导出 Excel</el-button>
        </el-form-item>
      </el-form>

      <div v-if="currentReport" class="report-meta">
        <el-tag>{{ currentReport.category }}</el-tag>
        <span>{{ currentReport.reportCode }}</span>
        <span>行数：{{ summary.rowCount }}</span>
        <span>耗时：{{ summary.elapsedMilliseconds }} ms</span>
      </div>

      <el-table v-loading="querying" :data="rows" border>
        <el-table-column
          v-for="column in columns"
          :key="column.key"
          :prop="column.key"
          :label="column.title"
          :width="columnWidth(column)"
          min-width="120"
          show-overflow-tooltip
        >
          <template #default="{ row }">{{ formatCell(row[column.key]) }}</template>
        </el-table-column>
      </el-table>
    </div>
  </PageContainer>
</template>

<style scoped>
.report-meta {
  align-items: center;
  color: var(--el-text-color-regular);
  display: flex;
  gap: 12px;
  margin: 4px 0 14px;
}
</style>
