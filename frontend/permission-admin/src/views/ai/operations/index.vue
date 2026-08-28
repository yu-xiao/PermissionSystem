<script setup lang="ts">
defineOptions({ name: 'AiOperations' })

import { Refresh, Search } from '@element-plus/icons-vue'
import { computed, ref } from 'vue'
import { getAiOperationsSummary, type AiOperationsSummary } from '../../../api/ai'
import PageContainer from '../../../components/PageContainer/index.vue'

const loading = ref(false)
const summary = ref<AiOperationsSummary>()
const dateRange = ref<[Date, Date]>([new Date(Date.now() - 30 * 24 * 60 * 60 * 1000), new Date()])
const successRate = computed(() =>
  summary.value?.runCount
    ? Math.round((summary.value.successfulRunCount / summary.value.runCount) * 1000) / 10
    : 0,
)
const feedbackRate = computed(() => {
  const total =
    (summary.value?.positiveFeedbackCount ?? 0) + (summary.value?.negativeFeedbackCount ?? 0)
  return total ? Math.round(((summary.value?.positiveFeedbackCount ?? 0) / total) * 1000) / 10 : 0
})

async function loadData() {
  loading.value = true
  try {
    summary.value = await getAiOperationsSummary({
      from: dateRange.value?.[0]?.toISOString(),
      to: dateRange.value?.[1]?.toISOString(),
    })
  } finally {
    loading.value = false
  }
}

function formatNumber(value?: number) {
  return new Intl.NumberFormat('zh-CN').format(value ?? 0)
}

function formatCost() {
  if (!summary.value?.costs.length) return '未知'
  return summary.value.costs.map((item) => `${item.currency} ${item.amount.toFixed(6)}`).join(' / ')
}

loadData()
</script>

<template>
  <PageContainer title="AI 运营中心">
    <template #actions>
      <el-tooltip content="刷新"><el-button :icon="Refresh" circle @click="loadData" /></el-tooltip>
    </template>

    <el-form class="toolbar" inline @submit.prevent="loadData">
      <el-form-item>
        <el-date-picker
          v-model="dateRange"
          type="datetimerange"
          range-separator="至"
          start-placeholder="开始时间"
          end-placeholder="结束时间"
        />
      </el-form-item>
      <el-form-item
        ><el-button type="primary" :icon="Search" @click="loadData">查询</el-button></el-form-item
      >
    </el-form>

    <div v-loading="loading" class="metrics-band">
      <div>
        <span>运行次数</span><strong>{{ formatNumber(summary?.runCount) }}</strong>
      </div>
      <div>
        <span>成功率</span><strong>{{ successRate }}%</strong>
      </div>
      <div>
        <span>P95 耗时</span><strong>{{ summary?.p95DurationMilliseconds ?? '-' }} ms</strong>
      </div>
      <div>
        <span>故障切换</span><strong>{{ formatNumber(summary?.fallbackRunCount) }}</strong>
      </div>
      <div>
        <span>Token</span
        ><strong>{{
          formatNumber((summary?.inputTokens ?? 0) + (summary?.outputTokens ?? 0))
        }}</strong>
      </div>
      <div>
        <span>估算费用</span><strong>{{ formatCost() }}</strong>
      </div>
    </div>

    <div class="quality-band">
      <div>
        <div class="band-label">
          <span>运行成功率</span><span>{{ successRate }}%</span>
        </div>
        <el-progress :percentage="successRate" :stroke-width="10" />
      </div>
      <div>
        <div class="band-label">
          <span>反馈好评率</span><span>{{ feedbackRate }}%</span>
        </div>
        <el-progress :percentage="feedbackRate" :stroke-width="10" status="success" />
      </div>
      <div class="quality-counts">
        <span>失败 {{ formatNumber(summary?.failedRunCount) }}</span>
        <span>好评 {{ formatNumber(summary?.positiveFeedbackCount) }}</span>
        <span>差评 {{ formatNumber(summary?.negativeFeedbackCount) }}</span>
        <span>成本未知调用 {{ formatNumber(summary?.unknownCostInvocationCount) }}</span>
      </div>
    </div>

    <el-tabs>
      <el-tab-pane label="Provider 统计">
        <el-table v-loading="loading" :data="summary?.providers ?? []" border>
          <el-table-column prop="providerName" label="Provider" min-width="200" />
          <el-table-column prop="invocationCount" label="调用次数" width="110" />
          <el-table-column prop="failedInvocationCount" label="失败次数" width="110" />
          <el-table-column prop="inputTokens" label="输入 Token" min-width="130" />
          <el-table-column prop="outputTokens" label="输出 Token" min-width="130" />
        </el-table>
      </el-tab-pane>
      <el-tab-pane label="质量趋势">
        <el-table v-loading="loading" :data="summary?.daily ?? []" border>
          <el-table-column prop="date" label="日期" min-width="130" />
          <el-table-column prop="runCount" label="运行次数" width="110" />
          <el-table-column prop="successfulRunCount" label="成功次数" width="110" />
          <el-table-column prop="positiveFeedbackCount" label="好评" width="100" />
          <el-table-column prop="negativeFeedbackCount" label="差评" width="100" />
        </el-table>
      </el-tab-pane>
    </el-tabs>
  </PageContainer>
</template>

<style scoped>
.metrics-band {
  display: grid;
  grid-template-columns: repeat(6, minmax(0, 1fr));
  margin: 14px 0 20px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
}

.metrics-band > div {
  display: grid;
  gap: 7px;
  min-width: 0;
  padding: 15px;
  border-right: 1px solid var(--el-border-color);
}

.metrics-band > div:last-child {
  border-right: 0;
}

.metrics-band span,
.band-label,
.quality-counts {
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.metrics-band strong {
  overflow-wrap: anywhere;
  font-size: 20px;
}

.quality-band {
  display: grid;
  grid-template-columns: minmax(220px, 1fr) minmax(220px, 1fr) minmax(300px, auto);
  gap: 28px;
  align-items: center;
  margin-bottom: 20px;
}

.band-label,
.quality-counts {
  display: flex;
  justify-content: space-between;
  gap: 14px;
  margin-bottom: 7px;
}

.quality-counts {
  flex-wrap: wrap;
  justify-content: flex-start;
  margin: 0;
}

@media (max-width: 1000px) {
  .metrics-band {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .metrics-band > div:nth-child(3) {
    border-right: 0;
  }

  .metrics-band > div:nth-child(-n + 3) {
    border-bottom: 1px solid var(--el-border-color);
  }

  .quality-band {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 620px) {
  .metrics-band {
    grid-template-columns: 1fr 1fr;
  }

  .metrics-band > div:nth-child(odd) {
    border-right: 1px solid var(--el-border-color);
  }

  .metrics-band > div:nth-child(even) {
    border-right: 0;
  }

  .metrics-band > div {
    border-bottom: 1px solid var(--el-border-color);
  }

  .metrics-band > div:nth-last-child(-n + 2) {
    border-bottom: 0;
  }
}
</style>
