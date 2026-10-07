<script setup lang="ts">
defineOptions({ name: 'AiOperations' })

import { Download, Refresh, Search } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onActivated, onDeactivated, onMounted, onUnmounted, ref, watch } from 'vue'
import {
  getAiOperationsSummary,
  getAiScenarioOperations,
  exportAiTechnicalMetadata,
  type AiCurrencyCost,
  type AiOperationsSummary,
  type AiScenarioOperations,
  type AiScenarioOperationsItem,
} from '../../../api/ai'
import PageContainer from '../../../components/PageContainer/index.vue'
import { useAuthStore } from '../../../stores/auth'
import AiTechnicalExportReceipts from './AiTechnicalExportReceipts.vue'
import AiCostQuality from './AiCostQuality.vue'

const auth = useAuthStore()
const canView = computed(() => auth.hasPermission('ai:operations:view'))
const canExport = computed(() => canView.value && auth.hasPermission('ai:operations:export'))
const identityKey = computed(() =>
  JSON.stringify([
    auth.effectiveTenantId,
    auth.currentUser?.userId,
    auth.currentUser?.permissionCodes,
    canView.value,
  ]),
)
const loading = ref(false)
const exporting = ref(false)
const exportDialog = ref(false)
const receiptsOpen = ref(false)
const costQualityOpen = ref(false)
const exportError = ref('')
let exportAbort: AbortController | undefined
const summary = ref<AiOperationsSummary>()
const scenarioStatistics = ref<AiScenarioOperations>()
const summaryError = ref('')
const scenarioError = ref('')
const scenarioPage = ref(1)
const scenarioPageSize = ref(20)
const activeTab = ref('providers')
const localTimeZone = Intl.DateTimeFormat().resolvedOptions().timeZone
const dateRange = ref<[Date, Date] | null>([
  new Date(Date.now() - 30 * 24 * 60 * 60 * 1000),
  new Date(),
])
let generation = 0
let active = true
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
  clear()
  const id = generation
  const identity = identityKey.value
  if (!active || !canView.value) return
  if (
    !dateRange.value ||
    !Number.isFinite(dateRange.value[0]?.getTime()) ||
    !Number.isFinite(dateRange.value[1]?.getTime()) ||
    dateRange.value[0] >= dateRange.value[1] ||
    dateRange.value[1].getTime() - dateRange.value[0].getTime() > 90 * 24 * 60 * 60 * 1000 ||
    dateRange.value[1].getTime() > Date.now() + 5 * 60 * 1000
  ) {
    summaryError.value = scenarioError.value = '请选择有效的时间范围，最多 90 天。'
    return
  }
  const params = {
    from: dateRange.value[0].toISOString(),
    to: dateRange.value[1].toISOString(),
  }
  loading.value = true
  try {
    const [overview, statistics] = await Promise.allSettled([
      getAiOperationsSummary(params),
      getAiScenarioOperations({
        ...params,
        pageIndex: scenarioPage.value,
        pageSize: scenarioPageSize.value,
      }),
    ])
    if (id !== generation || identity !== identityKey.value || !active || !canView.value) return
    if (overview.status === 'fulfilled') summary.value = overview.value
    else summaryError.value = '运营汇总读取失败，请重试。'
    if (statistics.status === 'fulfilled') scenarioStatistics.value = statistics.value
    else
      scenarioError.value =
        '场景统计读取失败。请核对当前权限和时间范围；数据超过上限时，请缩短时间范围后重试。'
  } finally {
    if (id === generation) loading.value = false
  }
}

function clear() {
  receiptsOpen.value = false
  costQualityOpen.value = false
  generation++
  summary.value = undefined
  scenarioStatistics.value = undefined
  summaryError.value = ''
  scenarioError.value = ''
  loading.value = false
  cancelExport()
}

function cancelExport() {
  exportAbort?.abort()
  exportAbort = undefined
  exporting.value = false
  exportDialog.value = false
  exportError.value = ''
}

function validRange() {
  return (
    dateRange.value &&
    Number.isFinite(dateRange.value[0]?.getTime()) &&
    Number.isFinite(dateRange.value[1]?.getTime()) &&
    dateRange.value[0] < dateRange.value[1] &&
    dateRange.value[1].getTime() - dateRange.value[0].getTime() <= 90 * 24 * 60 * 60 * 1000 &&
    dateRange.value[1].getTime() <= Date.now() + 5 * 60 * 1000
  )
}

function openExport() {
  if (!active || !canExport.value || exporting.value) return
  exportError.value = ''
  if (!validRange()) {
    exportError.value = '请选择有效的时间范围，最多 90 天。'
    return
  }
  exportDialog.value = true
}

async function confirmExport() {
  if (!active || !canExport.value || exporting.value || !validRange() || !dateRange.value) return
  const identity = identityKey.value
  const id = generation
  const controller = new AbortController()
  exportAbort = controller
  exporting.value = true
  exportError.value = ''
  exportDialog.value = false
  try {
    const file = await exportAiTechnicalMetadata(
      {
        from: dateRange.value[0].toISOString(),
        to: dateRange.value[1].toISOString(),
      },
      controller.signal,
    )
    if (
      controller.signal.aborted ||
      id !== generation ||
      identity !== identityKey.value ||
      !active ||
      !canExport.value
    )
      return
    const url = URL.createObjectURL(file.content)
    try {
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = file.fileName
      anchor.click()
      ElMessage.success('已交给浏览器下载；接收结果请核对浏览器下载记录。')
    } finally {
      URL.revokeObjectURL(url)
    }
  } catch {
    if (
      !controller.signal.aborted &&
      id === generation &&
      identity === identityKey.value &&
      active &&
      canExport.value
    )
      exportError.value = '导出失败，请核对权限、缩短时间范围或稍后重试。'
  } finally {
    if (exportAbort === controller) {
      exporting.value = false
      exportAbort = undefined
    }
  }
}

function queryData() {
  scenarioPage.value = 1
  void loadData()
}

function changePage(page: number) {
  scenarioPage.value = page
  void loadData()
}

function changePageSize(size: number) {
  scenarioPageSize.value = size
  queryData()
}

function formatNumber(value?: number) {
  return new Intl.NumberFormat('zh-CN').format(value ?? 0)
}

function formatCost() {
  if (!summary.value?.costs.length) return '未知'
  return summary.value.costs.map((item) => `${item.currency} ${item.amount.toFixed(6)}`).join(' / ')
}

function formatPercent(value: number | null) {
  return value === null ? '—' : `${value.toFixed(2)}%`
}

function formatEstimates(costs: AiCurrencyCost[]) {
  return costs.length
    ? costs.map((item) => `${item.currency} ${item.amount.toFixed(6)}`).join(' / ')
    : '暂无已知估算'
}

function formatTime(value: string) {
  return new Date(value).toLocaleString('zh-CN', { timeZone: localTimeZone })
}

function scenarioKey(row: AiScenarioOperationsItem) {
  return row.scenarioId ?? 'unbound'
}

watch(identityKey, queryData, { flush: 'sync' })
watch(canExport, cancelExport, { flush: 'sync' })
watch(dateRange, queryData, { deep: true, flush: 'sync' })
onMounted(() => void loadData())
let activatedOnce = false
onActivated(() => {
  active = true
  if (activatedOnce) void loadData()
  activatedOnce = true
})
onDeactivated(() => {
  active = false
  clear()
})
onUnmounted(() => {
  active = false
  clear()
})
</script>

<template>
  <PageContainer title="AI 运营中心">
    <template #actions>
      <el-button v-if="canView" aria-label="估算质量核验" @click="costQualityOpen = true"
        >估算质量核验</el-button
      >
      <el-button v-if="canExport" aria-label="我的导出凭据" @click="receiptsOpen = true"
        >我的导出凭据</el-button
      >
      <el-button
        v-if="canExport"
        aria-label="导出技术元数据"
        :icon="Download"
        :loading="exporting"
        :disabled="loading"
        @click="openExport"
      >
        导出技术元数据
      </el-button>
      <el-tooltip content="刷新"
        ><el-button
          :icon="Refresh"
          aria-label="刷新运营统计"
          :disabled="loading || !canView"
          circle
          @click="loadData"
      /></el-tooltip>
    </template>

    <AiTechnicalExportReceipts v-if="canExport" v-model="receiptsOpen" />
    <AiCostQuality v-if="canView" v-model="costQualityOpen" />

    <el-form class="toolbar" inline @submit.prevent="queryData">
      <el-form-item label="运行创建时间">
        <el-date-picker
          v-model="dateRange"
          type="datetimerange"
          range-separator="至"
          start-placeholder="开始时间"
          end-placeholder="结束时间"
          :disabled="!canView"
        />
      </el-form-item>
      <el-form-item
        ><el-button
          type="primary"
          :icon="Search"
          :loading="loading"
          :disabled="!canView"
          @click="queryData"
          >查询</el-button
        ></el-form-item
      >
    </el-form>

    <p class="statistics-note">
      查询范围按本机时区 {{ localTimeZone }} 显示；区间含开始、不含结束，最多 90 天。 原质量趋势按
      UTC 日期汇总。
    </p>
    <el-alert v-if="!canView" title="当前无 AI 运营查看权限" type="info" :closable="false" />
    <el-alert v-if="exportError" :title="exportError" type="error" :closable="false" role="alert" />
    <el-dialog
      v-model="exportDialog"
      title="导出 AI 技术元数据"
      width="min(520px, 92vw)"
      :close-on-click-modal="false"
    >
      <p>用途：内部技术复核；接收人：当前登录用户。</p>
      <p>目标租户：{{ auth.effectiveTenantId }}。</p>
      <p v-if="dateRange">
        创建时间：{{ dateRange[0].toLocaleString('zh-CN') }} 至
        {{ dateRange[1].toLocaleString('zh-CN') }}，含开始、不含结束。
      </p>
      <p>仅导出 Run／usage 技术字段，不含正文或用户明细；费用为估算。</p>
      <p>最多 10,000 次运行、50,000 条用量、16 MiB。导出不改变留存，文件不是长期归档。</p>
      <template #footer>
        <el-button @click="exportDialog = false">取消</el-button>
        <el-button type="primary" :disabled="!canExport || exporting" @click="confirmExport"
          >确认导出 JSON</el-button
        >
      </template>
    </el-dialog>
    <el-alert
      v-if="summaryError"
      :title="summaryError"
      type="error"
      :closable="false"
      role="alert"
    />
    <div v-if="summary && canView" class="metrics-band">
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

    <div v-if="summary && canView" class="quality-band">
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

    <el-tabs v-if="canView" v-model="activeTab" v-loading="loading">
      <el-tab-pane label="Provider 统计" name="providers">
        <el-table v-loading="loading" :data="summary?.providers ?? []" border>
          <el-table-column prop="providerName" label="Provider" min-width="200" />
          <el-table-column prop="invocationCount" label="调用次数" width="110" />
          <el-table-column prop="failedInvocationCount" label="失败次数" width="110" />
          <el-table-column prop="inputTokens" label="输入 Token" min-width="130" />
          <el-table-column prop="outputTokens" label="输出 Token" min-width="130" />
        </el-table>
      </el-tab-pane>
      <el-tab-pane label="质量趋势" name="daily">
        <el-table v-loading="loading" :data="summary?.daily ?? []" border>
          <el-table-column prop="date" label="日期" min-width="130" />
          <el-table-column prop="runCount" label="运行次数" width="110" />
          <el-table-column prop="successfulRunCount" label="成功次数" width="110" />
          <el-table-column prop="positiveFeedbackCount" label="好评" width="100" />
          <el-table-column prop="negativeFeedbackCount" label="差评" width="100" />
        </el-table>
      </el-tab-pane>
      <el-tab-pane label="场景统计" name="scenarios">
        <p class="statistics-note">
          终态技术完成率 = 完成 /（完成 + 失败 + 取消），等待中和执行中不进入分母。 P95
          仅使用已有的有效终态耗时，不补算缺失记录。
          反馈反映当前评价；人工纠错率、业务完成率：未采集。费用为估算，不是账单结算。
        </p>
        <p v-if="scenarioStatistics" class="statistics-note" data-testid="observation">
          实际区间：{{ formatTime(scenarioStatistics.from) }} —
          {{ formatTime(scenarioStatistics.to) }}； 观测：{{
            formatTime(scenarioStatistics.observedFrom)
          }}
          — {{ formatTime(scenarioStatistics.observedTo) }}。期间数据可能更新。 查询上限：{{
            formatNumber(scenarioStatistics.limits.maxRuns)
          }}
          次运行、 {{ formatNumber(scenarioStatistics.limits.maxUsages) }} 条用量、
          {{ formatNumber(scenarioStatistics.limits.maxFeedback) }} 条反馈。
        </p>
        <el-alert
          v-if="scenarioError"
          :title="scenarioError"
          type="error"
          :closable="false"
          role="alert"
        />
        <div class="scenario-table">
          <el-table
            :data="scenarioStatistics?.scenarios.items ?? []"
            :row-key="scenarioKey"
            empty-text="所选时间范围暂无场景运行记录"
            border
          >
            <el-table-column type="expand">
              <template #default="{ row }">
                <dl class="scenario-details">
                  <div>
                    <dt>等待中 / 执行中</dt>
                    <dd>{{ row.pendingRunCount }} / {{ row.runningRunCount }}</dd>
                  </div>
                  <div>
                    <dt>完成 / 失败 / 取消</dt>
                    <dd>
                      {{ row.completedRunCount }} / {{ row.failedRunCount }} /
                      {{ row.cancelledRunCount }}
                    </dd>
                  </div>
                  <div>
                    <dt>超时失败（失败子集）</dt>
                    <dd>{{ row.timeoutFailureCount }}</dd>
                  </div>
                  <div>
                    <dt>未知运行状态</dt>
                    <dd>{{ row.unknownStatusRunCount }}</dd>
                  </div>
                  <div>
                    <dt>正 / 负评价</dt>
                    <dd>{{ row.positiveFeedbackCount }} / {{ row.negativeFeedbackCount }}</dd>
                  </div>
                  <div>
                    <dt>可评价完成运行</dt>
                    <dd>{{ row.feedbackEligibleRunCount }}</dd>
                  </div>
                  <div>
                    <dt>实际输入 / 输出 Token</dt>
                    <dd>
                      {{ formatNumber(row.inputTokens) }} / {{ formatNumber(row.outputTokens) }}
                    </dd>
                  </div>
                  <div>
                    <dt>实际 usage 不完整调用</dt>
                    <dd>{{ row.unknownUsageInvocationCount }}</dd>
                  </div>
                  <div>
                    <dt>估算费用未知调用</dt>
                    <dd>{{ row.unknownCostInvocationCount }}</dd>
                  </div>
                  <div>
                    <dt>未决 / 未知状态调用</dt>
                    <dd>
                      {{ row.unsettledInvocationCount }} / {{ row.unknownStatusInvocationCount }}
                    </dd>
                  </div>
                  <div>
                    <dt>人工纠错率 / 业务完成率</dt>
                    <dd>未采集 / 未采集</dd>
                  </div>
                </dl>
              </template>
            </el-table-column>
            <el-table-column label="场景（当前名称）" min-width="180">
              <template #default="{ row }">
                <span>{{ row.scenarioName }}</span>
                <span v-if="row.scenarioCode" class="scenario-code">{{ row.scenarioCode }}</span>
              </template>
            </el-table-column>
            <el-table-column label="运行次数" min-width="100">
              <template #default="{ row }">{{ formatNumber(row.runCount) }}</template>
            </el-table-column>
            <el-table-column label="终态技术完成率" min-width="165">
              <template #default="{ row }">
                {{ formatPercent(row.technicalCompletionRate) }}
                <span class="scenario-code"
                  >{{ row.completedRunCount }} / {{ row.terminalRunCount }}</span
                >
              </template>
            </el-table-column>
            <el-table-column label="反馈覆盖率" min-width="130">
              <template #default="{ row }">
                {{ formatPercent(row.feedbackCoverageRate) }}
                <span class="scenario-code"
                  >{{ row.positiveFeedbackCount + row.negativeFeedbackCount }} /
                  {{ row.feedbackEligibleRunCount }}</span
                >
              </template>
            </el-table-column>
            <el-table-column label="反馈好评率" min-width="130">
              <template #default="{ row }">
                {{ formatPercent(row.positiveFeedbackRate) }}
                <span class="scenario-code"
                  >{{ row.positiveFeedbackCount }} /
                  {{ row.positiveFeedbackCount + row.negativeFeedbackCount }}</span
                >
              </template>
            </el-table-column>
            <el-table-column label="P95 运行耗时" min-width="150">
              <template #default="{ row }">
                {{
                  row.p95DurationMilliseconds == null
                    ? '—'
                    : `${formatNumber(row.p95DurationMilliseconds)} ms`
                }}
                <span class="scenario-code">{{ row.durationSampleCount }} 个有效终态样本</span>
              </template>
            </el-table-column>
            <el-table-column label="分币种估算费用" min-width="200">
              <template #default="{ row }">
                {{ formatEstimates(row.estimatedCosts) }}
                <span class="scenario-code"
                  >未知 {{ row.unknownCostInvocationCount }} / 未决
                  {{ row.unsettledInvocationCount }}</span
                >
              </template>
            </el-table-column>
          </el-table>
        </div>
        <el-pagination
          v-if="scenarioStatistics"
          class="scenario-pagination"
          :current-page="scenarioPage"
          :page-size="scenarioPageSize"
          :page-sizes="[10, 20, 50]"
          :total="scenarioStatistics.scenarios.totalCount"
          :disabled="loading"
          layout="total, sizes, prev, pager, next"
          @current-change="changePage"
          @size-change="changePageSize"
        />
      </el-tab-pane>
    </el-tabs>
  </PageContainer>
</template>

<style scoped>
.statistics-note {
  margin: 12px 0;
  color: var(--el-text-color-secondary);
  line-height: 1.6;
}

.scenario-table {
  max-width: 100%;
  overflow-x: auto;
}

.scenario-code {
  display: block;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.scenario-details {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 16px;
  margin: 16px;
}

.scenario-details dt {
  color: var(--el-text-color-secondary);
}

.scenario-details dd {
  margin: 4px 0 0;
}

.scenario-pagination {
  margin-top: 16px;
  flex-wrap: wrap;
  gap: 8px;
}

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
