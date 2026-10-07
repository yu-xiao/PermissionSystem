<script setup lang="ts">
import { computed, onActivated, onDeactivated, onUnmounted, ref, watch } from 'vue'
import { useAuthStore } from '../../../stores/auth'
import {
  getAiCostQuality,
  getAiCostQualityTrends,
  type AiCostQualityResponse,
  type AiCostQualityTrendResponse,
} from '../../../api/ai'
import AiCostQualityTrends from './AiCostQualityTrends.vue'

const props = defineProps<{ modelValue: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()
const auth = useAuthStore()
const allowed = computed(() => auth.hasPermission('ai:operations:view'))
const identity = computed(() =>
  JSON.stringify([
    auth.effectiveTenantId,
    auth.currentUser?.userId,
    auth.currentUser?.permissionCodes,
    auth.currentUser?.roles,
    auth.currentUser?.isSuperAdmin,
    allowed.value,
  ]),
)
const range = ref<[Date, Date] | null>([new Date(Date.now() - 30 * 86400000), new Date()])
const pageIndex = ref(1)
const response = ref<AiCostQualityResponse>()
const trend = ref<AiCostQualityTrendResponse>()
const view = ref<'summary' | 'trends'>('summary')
const summary = computed(() => response.value ?? trend.value)
const loading = ref(false)
const error = ref('')
let generation = 0
let active = true
let controller: AbortController | undefined
const comparisons = computed(() =>
  summary.value
    ? [
        { label: '输入估算比较', ...summary.value.inputComparison },
        { label: '输出上限使用情况', ...summary.value.outputLimitComparison },
      ]
    : [],
)
const issueLabels: Record<string, string> = {
  MissingInputTokens: '缺已记录输入',
  NegativeInputTokens: '负已记录输入',
  MissingOutputTokens: '缺已记录输出',
  NegativeOutputTokens: '负已记录输出',
  MissingTotalTokens: '缺已记录总量',
  NegativeTotalTokens: '负已记录总量',
  MissingEstimatedInputTokens: '缺输入估算',
  NegativeEstimatedInputTokens: '负输入估算',
  MissingEstimatedOutputTokens: '缺输出上限',
  NegativeEstimatedOutputTokens: '负输出上限',
  MissingPrice: '缺单价快照',
  NegativePrice: '负单价快照',
  MissingCurrency: '缺币种',
  InvalidCurrency: '无效币种',
  MissingStoredCost: '缺存储费用',
  NegativeStoredCost: '负存储费用',
  ArithmeticOverflow: '单条计算溢出',
}
function clear() {
  generation++
  controller?.abort()
  controller = undefined
  loading.value = false
  response.value = undefined
  trend.value = undefined
  error.value = ''
}
function current(id: number, key: string) {
  return id === generation && key === identity.value && active && allowed.value && props.modelValue
}
function validRange() {
  return (
    range.value &&
    Number.isFinite(range.value[0]?.getTime()) &&
    Number.isFinite(range.value[1]?.getTime()) &&
    range.value[0] < range.value[1] &&
    range.value[1].getTime() - range.value[0].getTime() <= 90 * 86400000 &&
    range.value[1].getTime() <= Date.now() + 5 * 60000
  )
}
async function load() {
  clear()
  if (!active || !allowed.value || !props.modelValue) return
  if (!validRange() || !range.value) {
    error.value = '请选择有效的调用记录时间，最多 90 天。'
    return
  }
  const id = generation,
    key = identity.value
  const request = new AbortController()
  controller = request
  loading.value = true
  const params = {
    from: range.value[0].toISOString(),
    to: range.value[1].toISOString(),
    pageIndex: pageIndex.value,
    pageSize: 20,
  }
  const queriedView = view.value
  try {
    const value =
      queriedView === 'summary'
        ? await getAiCostQuality(params, request.signal)
        : await getAiCostQualityTrends(params, request.signal)
    if (!current(id, key)) return
    if (
      value.scope !== 'CurrentTenantReadableRunInvocations' ||
      value.costBasis !== 'HistoricalSnapshotEstimateNotSupplierInvoice' ||
      value.metricsVersion !== 1 ||
      value.tenantId.toLowerCase() !== auth.effectiveTenantId?.toLowerCase() ||
      Date.parse(value.from) !== Date.parse(params.from) ||
      Date.parse(value.to) !== Date.parse(params.to) ||
      value.currencies.pageIndex !== params.pageIndex ||
      value.currencies.pageSize !== params.pageSize
    )
      throw new Error('scope')
    if (queriedView === 'trends') {
      if (
        !('grouping' in value) ||
        value.grouping !== 'UsageCreatedAtUtcDay' ||
        value.bucketTimezone !== 'UTC'
      )
        throw new Error('grouping')
      trend.value = value
    } else {
      if ('grouping' in value) throw new Error('view')
      response.value = value
    }
  } catch {
    if (current(id, key)) error.value = '估算质量读取失败，请核对权限或缩短调用记录时间后重试。'
  } finally {
    if (current(id, key)) {
      loading.value = false
      controller = undefined
    }
  }
}
function close() {
  clear()
  emit('update:modelValue', false)
}
function money(value: string | null) {
  if (value === null) return '—'
  const [integer, fraction = ''] = value.split('.')
  return `${integer}.${fraction.padEnd(6, '0')}`
}
watch(
  () => props.modelValue,
  (open) => {
    clear()
    if (open) {
      pageIndex.value = 1
      void load()
    } else {
      view.value = 'summary'
    }
  },
  { immediate: true },
)
watch(identity, close, { flush: 'sync' })
watch(
  view,
  () => {
    pageIndex.value = 1
    void load()
  },
  { flush: 'sync' },
)
watch(
  range,
  () => {
    pageIndex.value = 1
    void load()
  },
  { deep: true, flush: 'sync' },
)
onActivated(() => {
  active = true
})
onDeactivated(() => {
  active = false
  close()
})
onUnmounted(() => {
  active = false
  clear()
})
</script>

<template>
  <el-dialog
    :model-value="modelValue && allowed"
    title="估算质量核验"
    width="min(1100px, 94vw)"
    :close-on-click-modal="false"
    @update:model-value="close"
  >
    <p>仅当前活动租户内关联仍可读 Run 的调用；当前窗口不是完整历史或供应商账期。</p>
    <el-radio-group v-model="view" aria-label="质量视图" :disabled="!allowed">
      <el-radio-button value="summary">窗口摘要</el-radio-button>
      <el-radio-button value="trends">按日趋势</el-radio-button>
    </el-radio-group>
    <el-form inline @submit.prevent="load">
      <el-form-item label="调用记录时间"
        ><el-date-picker
          v-model="range"
          type="datetimerange"
          start-placeholder="开始时间"
          end-placeholder="结束时间"
          :disabled="!allowed"
      /></el-form-item>
      <el-form-item
        ><el-button :loading="loading" :disabled="!allowed" @click="load"
          >查询质量</el-button
        ></el-form-item
      >
    </el-form>
    <p class="note">
      含开始、不含结束，最多 90 天、50,000 条。与原运行创建时间独立，两页合计可能不同。
    </p>
    <el-alert v-if="error" :title="error" type="error" :closable="false" role="alert" />
    <p v-if="loading" role="status">正在读取估算质量…</p>
    <template v-if="summary">
      <p class="note">
        本次观察：{{ summary.observedFrom }} 至
        {{ summary.observedTo }}；重新查询或翻页会重新观察，结果可能变化。
      </p>
      <section aria-label="调用与费用样本">
        <h3>调用与费用样本</h3>
        <p>
          参与 {{ summary.population.invocationCount }}；终态
          {{ summary.population.terminalCount }}； 未决
          {{ summary.population.unsettledCount }}；未知状态
          {{ summary.population.unknownStatusCount }}。
        </p>
        <p>终态包含完成、失败和取消；失败不等于零费用。未决和未知状态不参与费用比较。</p>
        <p>
          费用可比 {{ summary.population.comparableCostCount }}；一致
          {{ summary.population.consistentCostCount }}； 不一致
          {{ summary.population.differentCostCount }}；不可比
          {{ summary.population.uncomparableCostCount }}。
        </p>
        <p>
          Token 依据：两项已记录 {{ summary.basis.recordedBothCount }}；混合回退
          {{ summary.basis.mixedFallbackCount }}； 两项估算回退
          {{ summary.basis.estimatedBothCount }}；无法成对
          {{ summary.basis.unusableTokenPairCount }}。
        </p>
      </section>
      <section aria-label="Token 比较">
        <h3>Token 成对样本</h3>
        <p>输入是字符估算，输出是请求上限；已记录 Token 不具备来源认证，不能一律当供应商实测。</p>
        <el-table :data="comparisons" row-key="label" empty-text="无样本">
          <el-table-column prop="label" label="比较口径" min-width="160" />
          <el-table-column prop="sampleCount" label="成对样本" min-width="100" />
          <el-table-column prop="recordedTokens" label="已记录合计" min-width="120" />
          <el-table-column prop="estimatedTokens" label="估算／上限合计" min-width="140" />
          <el-table-column prop="differenceTokens" label="记录减估算／上限" min-width="160" />
          <el-table-column prop="absoluteDifferenceTokens" label="逐条绝对差合计" min-width="150" />
          <el-table-column label="加权比" min-width="100"
            ><template #default="{ row }">
              {{
                row.weightedRatioPercentage === null ? '—' : `${row.weightedRatioPercentage}%`
              }}</template
            ></el-table-column
          >
          <el-table-column prop="zeroEstimatePairCount" label="零估算／上限对" min-width="140" />
          <el-table-column prop="aboveEstimateCount" label="记录高于估算／上限" min-width="160" />
        </el-table>
        <p class="note">
          比例只用已记录非负、估算／上限为正的成对样本；无样本为“—”。加权比不是逐条比率平均或改价系数。
          输出超过上限只作质量提示，不裁剪到 100%，不视为预测误差或费用节省。
        </p>
        <p>
          TotalTokens 可比 {{ summary.totalTokens.comparableCount }}；与记录输入＋输出不一致
          {{ summary.totalTokens.differentCount }}。
        </p>
      </section>
      <section aria-label="终态数据质量诊断">
        <h3>终态数据质量诊断</h3>
        <p>问题计数可以重叠；缺失、负值和合法零分开。不可重算不等于没有费用。</p>
        <dl class="issues">
          <template v-for="issue in summary.issues" :key="issue.code">
            <div>
              <dt>{{ issueLabels[issue.code] ?? '未知诊断' }}</dt>
              <dd>{{ issue.count }}</dd>
            </div>
          </template>
        </dl>
      </section>
      <section v-if="response" aria-label="分币种历史费用一致性">
        <h3>分币种历史费用一致性</h3>
        <p>
          金额只比较同币种、同一可比样本；差额为存储减重算，绝对差额逐条累加。无样本为“—”，合法零为
          0。 币种格式合格不证明其为真实 ISO 币种；不跨币种合计、不换汇。
        </p>
        <el-table
          :data="response.currencies.items"
          row-key="currency"
          max-height="360"
          empty-text="当前窗口没有有效币种分组"
        >
          <el-table-column prop="currency" label="币种" width="80" />
          <el-table-column prop="terminalCount" label="终态" min-width="80" />
          <el-table-column prop="comparableCostCount" label="费用可比样本" min-width="120" />
          <el-table-column prop="consistentCostCount" label="一致" min-width="80" />
          <el-table-column prop="differentCostCount" label="不一致" min-width="90" />
          <el-table-column prop="uncomparableCostCount" label="不可比" min-width="90" />
          <el-table-column label="存储费用" min-width="140"
            ><template #default="{ row }">{{ money(row.storedCost) }}</template></el-table-column
          >
          <el-table-column label="历史快照重算" min-width="140"
            ><template #default="{ row }">{{
              money(row.recomputedCost)
            }}</template></el-table-column
          >
          <el-table-column label="差额合计" min-width="140"
            ><template #default="{ row }">{{
              money(row.differenceCost)
            }}</template></el-table-column
          >
          <el-table-column label="绝对差额合计" min-width="140"
            ><template #default="{ row }">{{
              money(row.absoluteDifferenceCost)
            }}</template></el-table-column
          >
          <el-table-column label="Token 依据" min-width="260"
            ><template #default="{ row }">
              记录 {{ row.basis.recordedBothCount }}／混合 {{ row.basis.mixedFallbackCount }}／估算
              {{ row.basis.estimatedBothCount }}／不可用 {{ row.basis.unusableTokenPairCount }}
            </template></el-table-column
          >
        </el-table>
        <el-pagination
          v-model:current-page="pageIndex"
          :page-size="20"
          :total="response.currencies.totalCount"
          layout="prev, pager, next"
          @current-change="load"
        />
        <p class="note">
          {{ response.currencies.totalCount }}
          个有效币种分组；分页只影响币种表，窗口全局样本不随页变化。
        </p>
      </section>
      <AiCostQualityTrends
        v-if="trend"
        :value="trend"
        :page-index="pageIndex"
        :issue-labels="issueLabels"
        @page="
          (page) => {
            pageIndex = page
            void load()
          }
        "
      />
    </template>
    <p class="note">
      只核验历史快照估算的一致性，不是供应商账单或财务对账；差异不能直接证明计费错误。
      本页不导入账单、不修改单价、费用、预算或历史记录。
    </p>
    <template #footer><el-button @click="close">关闭</el-button></template>
  </el-dialog>
</template>

<style scoped lang="scss">
.note {
  color: var(--el-text-color-secondary);
  line-height: 1.6;
}
section {
  margin-top: 24px;
  min-width: 0;
}
.issues {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 12px;
}
.issues div {
  display: flex;
  gap: 12px;
  justify-content: space-between;
}
.issues dt {
  color: var(--el-text-color-secondary);
}
.issues dd {
  margin: 0;
}
.el-alert,
.el-pagination {
  margin-top: 12px;
}
.el-form-item {
  max-width: 100%;
}
:deep(.el-form-item__content) {
  min-width: 0;
}
:deep(.el-date-editor) {
  max-width: 100%;
}
@media (max-width: 600px) {
  .el-form-item {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    margin-right: 0;
  }
}
</style>
