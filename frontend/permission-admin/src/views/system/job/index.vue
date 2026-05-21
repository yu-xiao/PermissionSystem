<script setup lang="ts">
import { ElMessage, ElMessageBox } from 'element-plus'
import { Link, Refresh, VideoPlay } from '@element-plus/icons-vue'
import { reactive, ref } from 'vue'
import {
  disableJob,
  enableJob,
  getHangfireDashboardUrl,
  getJobLogs,
  getJobs,
  triggerJob,
  type JobExecutionLogItem,
  type JobInfoItem,
} from '../../../api/jobs'

const loading = ref(false)
const logLoading = ref(false)
const tableData = ref<JobInfoItem[]>([])
const logData = ref<JobExecutionLogItem[]>([])
const total = ref(0)
const logTotal = ref(0)
const logDialogVisible = ref(false)
const currentJob = ref<JobInfoItem>()

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  status: '',
})

const logQuery = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  jobName: '',
  status: '',
})

async function loadData() {
  loading.value = true
  try {
    const result = await getJobs({
      ...query,
      status: query.status || undefined,
    })
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
    status: '',
  })
  loadData()
}

async function runJob(row: JobInfoItem) {
  await ElMessageBox.confirm(`Trigger job "${row.jobName}" once?`, 'Confirm Trigger')
  await triggerJob(row.jobName)
  ElMessage.success('Job trigger request submitted.')
  await loadData()
}

async function toggleJob(row: JobInfoItem) {
  if (row.isEnabled) {
    await disableJob(row.jobName)
    ElMessage.success('Job disabled.')
  } else {
    await enableJob(row.jobName)
    ElMessage.success('Job enabled.')
  }

  await loadData()
}

async function openLogs(row: JobInfoItem) {
  currentJob.value = row
  Object.assign(logQuery, {
    pageIndex: 1,
    keyword: '',
    jobName: row.jobName,
    status: '',
  })
  logDialogVisible.value = true
  await loadLogs()
}

async function loadLogs() {
  logLoading.value = true
  try {
    const result = await getJobLogs({
      ...logQuery,
      jobName: logQuery.jobName || undefined,
      status: logQuery.status || undefined,
    })
    logData.value = result.items
    logTotal.value = result.totalCount
  } finally {
    logLoading.value = false
  }
}

function openDashboard() {
  window.open(getHangfireDashboardUrl(), '_blank', 'noopener,noreferrer')
}

function statusType(status?: string) {
  if (status === 'Enabled' || status === 'Succeeded') {
    return 'success'
  }

  if (status === 'Skipped') {
    return 'warning'
  }

  if (status === 'Failed' || status === 'Disabled') {
    return 'danger'
  }

  return 'info'
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

function formatDuration(value?: number) {
  return typeof value === 'number' ? `${value} ms` : '-'
}

loadData()
</script>

<template>
  <section class="page">
    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="Job name / type / source" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.status" clearable placeholder="Status" style="width: 140px">
          <el-option label="Enabled" value="Enabled" />
          <el-option label="Disabled" value="Disabled" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:job:view'" type="primary" :icon="Refresh" @click="loadData">Search</el-button>
        <el-button @click="resetQuery">Reset</el-button>
        <el-button v-permission="'system:job:view'" :icon="Link" @click="openDashboard">Dashboard</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="jobName" label="Job Name" min-width="180" show-overflow-tooltip />
      <el-table-column prop="jobType" label="Type" min-width="150" show-overflow-tooltip />
      <el-table-column prop="source" label="Source" width="130" />
      <el-table-column prop="queue" label="Queue" width="110" />
      <el-table-column prop="cronExpression" label="Cron" width="130">
        <template #default="{ row }">{{ row.cronExpression || '-' }}</template>
      </el-table-column>
      <el-table-column prop="status" label="Status" width="120">
        <template #default="{ row }">
          <el-tag :type="statusType(row.status)">{{ row.status }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="lastRunAt" label="Last Run" width="180">
        <template #default="{ row }">{{ formatDate(row.lastRunAt) }}</template>
      </el-table-column>
      <el-table-column prop="lastRunStatus" label="Last Status" width="130">
        <template #default="{ row }">
          <el-tag v-if="row.lastRunStatus" :type="statusType(row.lastRunStatus)">{{ row.lastRunStatus }}</el-tag>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column prop="lastErrorMessage" label="Last Error" min-width="180" show-overflow-tooltip>
        <template #default="{ row }">{{ row.lastErrorMessage || '-' }}</template>
      </el-table-column>
      <el-table-column label="Actions" width="220" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:job:trigger'" link type="primary" :icon="VideoPlay" @click="runJob(row)">Trigger</el-button>
          <el-button
            v-if="row.source === 'ScheduledTask'"
            v-permission="'system:job:trigger'"
            link
            type="primary"
            @click="toggleJob(row)"
          >
            {{ row.isEnabled ? 'Disable' : 'Enable' }}
          </el-button>
          <el-button v-permission="'system:job:view'" link type="primary" @click="openLogs(row)">Logs</el-button>
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

    <el-dialog v-model="logDialogVisible" :title="`Execution Logs - ${currentJob?.jobName ?? ''}`" width="960px">
      <el-form class="toolbar" inline @submit.prevent>
        <el-form-item>
          <el-input v-model="logQuery.keyword" clearable placeholder="JobId / TraceId" />
        </el-form-item>
        <el-form-item>
          <el-select v-model="logQuery.status" clearable placeholder="Status" style="width: 140px">
            <el-option label="Succeeded" value="Succeeded" />
            <el-option label="Failed" value="Failed" />
            <el-option label="Skipped" value="Skipped" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="loadLogs">Search</el-button>
        </el-form-item>
      </el-form>

      <el-table v-loading="logLoading" :data="logData" border>
        <el-table-column prop="startedAt" label="Started" width="180">
          <template #default="{ row }">{{ formatDate(row.startedAt) }}</template>
        </el-table-column>
        <el-table-column prop="finishedAt" label="Finished" width="180">
          <template #default="{ row }">{{ formatDate(row.finishedAt) }}</template>
        </el-table-column>
        <el-table-column prop="status" label="Status" width="120">
          <template #default="{ row }">
            <el-tag :type="statusType(row.status)">{{ row.status }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="elapsedMilliseconds" label="Elapsed" width="120">
          <template #default="{ row }">{{ formatDuration(row.elapsedMilliseconds) }}</template>
        </el-table-column>
        <el-table-column prop="jobId" label="JobId" min-width="130" show-overflow-tooltip>
          <template #default="{ row }">{{ row.jobId || '-' }}</template>
        </el-table-column>
        <el-table-column prop="traceId" label="TraceId" min-width="220" show-overflow-tooltip>
          <template #default="{ row }">{{ row.traceId || '-' }}</template>
        </el-table-column>
        <el-table-column prop="errorMessage" label="Error" min-width="220" show-overflow-tooltip>
          <template #default="{ row }">{{ row.errorMessage || '-' }}</template>
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
    </el-dialog>
  </section>
</template>
