<script setup lang="ts">
import { computed, ref } from 'vue'
import { Refresh } from '@element-plus/icons-vue'
import { getHealthDetail, type HealthDetailResponse, type HealthEntryResponse } from '../../../api/health'

const loading = ref(false)
const health = ref<HealthDetailResponse>()

const entries = computed(() => health.value?.entries ?? [])

async function loadData() {
  loading.value = true
  try {
    health.value = await getHealthDetail()
  } finally {
    loading.value = false
  }
}

function statusType(status: string) {
  if (status === 'Healthy') {
    return 'success'
  }

  if (status === 'Degraded') {
    return 'warning'
  }

  return 'danger'
}

function formatDuration(value?: number) {
  return typeof value === 'number' ? `${value.toFixed(2)} ms` : '-'
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

function formatData(entry: HealthEntryResponse) {
  const pairs = Object.entries(entry.data ?? {})
  if (pairs.length === 0) {
    return '-'
  }

  return pairs.map(([key, value]) => `${key}: ${value ?? ''}`).join('\n')
}

loadData()
</script>

<template>
  <section class="page health-page">
    <div class="health-header">
      <div>
        <h2>System Health</h2>
        <p>Last checked: {{ formatDate(health?.checkedAt) }}</p>
      </div>
      <el-button
        v-permission="'system:health:view'"
        type="primary"
        :icon="Refresh"
        :loading="loading"
        @click="loadData"
      >
        Refresh
      </el-button>
    </div>

    <div class="summary-grid">
      <el-card shadow="never">
        <template #header>Status</template>
        <el-tag v-if="health" size="large" :type="statusType(health.status)">{{ health.status }}</el-tag>
        <span v-else>-</span>
      </el-card>
      <el-card shadow="never">
        <template #header>Total Duration</template>
        <strong>{{ formatDuration(health?.totalDurationMilliseconds) }}</strong>
      </el-card>
      <el-card shadow="never">
        <template #header>Components</template>
        <strong>{{ entries.length }}</strong>
      </el-card>
    </div>

    <el-table v-loading="loading" :data="entries" border>
      <el-table-column prop="name" label="Component" min-width="170" />
      <el-table-column prop="status" label="Status" width="130">
        <template #default="{ row }">
          <el-tag :type="statusType(row.status)">{{ row.status }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="durationMilliseconds" label="Duration" width="140">
        <template #default="{ row }">{{ formatDuration(row.durationMilliseconds) }}</template>
      </el-table-column>
      <el-table-column prop="description" label="Description" min-width="220" show-overflow-tooltip>
        <template #default="{ row }">{{ row.description || '-' }}</template>
      </el-table-column>
      <el-table-column prop="tags" label="Tags" min-width="180">
        <template #default="{ row }">
          <el-space wrap>
            <el-tag v-for="tag in row.tags" :key="tag" type="info" effect="plain">{{ tag }}</el-tag>
            <span v-if="row.tags.length === 0">-</span>
          </el-space>
        </template>
      </el-table-column>
      <el-table-column label="Data" min-width="260">
        <template #default="{ row }">
          <pre class="health-data">{{ formatData(row) }}</pre>
        </template>
      </el-table-column>
      <el-table-column prop="error" label="Error" min-width="220" show-overflow-tooltip>
        <template #default="{ row }">{{ row.error || '-' }}</template>
      </el-table-column>
    </el-table>
  </section>
</template>

<style scoped>
.health-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.health-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.health-header h2 {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
}

.health-header p {
  margin: 6px 0 0;
  color: #606266;
  font-size: 13px;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
}

.summary-grid strong {
  font-size: 20px;
  line-height: 1.4;
}

.health-data {
  max-height: 120px;
  margin: 0;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: Consolas, 'Courier New', monospace;
  font-size: 12px;
  line-height: 1.5;
}

@media (max-width: 768px) {
  .health-header {
    align-items: flex-start;
    flex-direction: column;
  }

  .summary-grid {
    grid-template-columns: 1fr;
  }
}
</style>
