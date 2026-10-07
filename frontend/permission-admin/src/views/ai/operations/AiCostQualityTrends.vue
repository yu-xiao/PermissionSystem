<script setup lang="ts">
import type { AiCostQualityTrendResponse } from '../../../api/ai'

defineProps<{
  value: AiCostQualityTrendResponse
  pageIndex: number
  issueLabels: Record<string, string>
}>()
defineEmits<{ page: [value: number] }>()
function money(value: string | null) {
  if (value === null) return '—'
  const [integer, fraction = ''] = value.split('.')
  return `${integer}.${fraction.padEnd(6, '0')}`
}
function ratio(value: number | null) {
  return value === null ? '—' : `${value}%`
}
</script>

<template>
  <section aria-label="UTC 按日质量趋势">
    <h3>UTC 按日质量趋势</h3>
    <p>
      按调用创建时间归属 UTC 日，最多 91
      个日桶。首尾部分日不是完整自然日；空日只表示当前可读范围未观察到，不证明没有调用或零收费。
    </p>
    <p class="note">
      日期直接按 UTC 显示；计数和 Token
      合计来自同一观察，窗口比率重新加权，不取每日比率平均。展开可查看日样本、依据和重叠诊断。
    </p>
    <el-table :data="value.daily" row-key="date" max-height="360" empty-text="当前窗口没有日桶">
      <el-table-column type="expand">
        <template #default="{ row }">
          <div class="details">
            <p>UTC 桶：{{ row.bucketFrom }} 至 {{ row.bucketTo }}（含开始、不含结束）。</p>
            <p>
              Token 依据：记录 {{ row.basis.recordedBothCount }}／混合
              {{ row.basis.mixedFallbackCount }}／估算 {{ row.basis.estimatedBothCount }}／无法成对
              {{ row.basis.unusableTokenPairCount }}。
            </p>
            <p
              v-for="comparison in [
                { label: '输入估算', ...row.inputComparison },
                { label: '输出上限', ...row.outputLimitComparison },
              ]"
              :key="comparison.label"
            >
              {{ comparison.label }}：样本 {{ comparison.sampleCount }}；已记录
              {{ comparison.recordedTokens }}；估算／上限
              {{ comparison.estimatedTokens }}；记录减估算／上限
              {{ comparison.differenceTokens }}；逐条绝对差
              {{ comparison.absoluteDifferenceTokens }}；加权比
              {{ ratio(comparison.weightedRatioPercentage) }}；零基线对
              {{ comparison.zeroEstimatePairCount }}；高于基线 {{ comparison.aboveEstimateCount }}。
            </p>
            <p>
              TotalTokens 可比 {{ row.totalTokens.comparableCount }}；输入＋输出不一致
              {{ row.totalTokens.differentCount }}。
            </p>
            <dl class="issues">
              <div v-for="issue in row.issues" :key="issue.code">
                <dt>{{ issueLabels[issue.code] ?? '未知诊断' }}</dt>
                <dd>{{ issue.count }}</dd>
              </div>
            </dl>
          </div>
        </template>
      </el-table-column>
      <el-table-column prop="date" label="UTC 日期" min-width="110" />
      <el-table-column label="日范围" min-width="100"
        ><template #default="{ row }">{{
          row.isPartialDay ? '部分日' : '完整日'
        }}</template></el-table-column
      >
      <el-table-column prop="population.invocationCount" label="参与" min-width="80" />
      <el-table-column prop="population.terminalCount" label="终态" min-width="80" />
      <el-table-column prop="population.unsettledCount" label="未决" min-width="80" />
      <el-table-column prop="population.unknownStatusCount" label="未知状态" min-width="100" />
      <el-table-column prop="population.comparableCostCount" label="费用可比" min-width="100" />
      <el-table-column prop="population.consistentCostCount" label="费用一致" min-width="100" />
      <el-table-column prop="population.differentCostCount" label="费用不一致" min-width="110" />
      <el-table-column prop="population.uncomparableCostCount" label="费用不可比" min-width="110" />
      <el-table-column prop="inputComparison.sampleCount" label="输入成对样本" min-width="120" />
      <el-table-column label="输入加权比" min-width="110"
        ><template #default="{ row }">{{
          ratio(row.inputComparison.weightedRatioPercentage)
        }}</template></el-table-column
      >
      <el-table-column
        prop="outputLimitComparison.sampleCount"
        label="输出上限样本"
        min-width="120"
      />
      <el-table-column label="输出上限使用比" min-width="140"
        ><template #default="{ row }">{{
          ratio(row.outputLimitComparison.weightedRatioPercentage)
        }}</template></el-table-column
      >
    </el-table>
  </section>
  <section aria-label="全窗口分币种费用差异方向">
    <h3>全窗口分币种费用差异方向</h3>
    <p>
      仅同币种、同一可比终态样本。高于＋等于＋低于＝可比样本；不可比单列。方向不是多收、少收或改价建议，日表不提供跨币种金额。
    </p>
    <el-table
      :data="value.currencies.items"
      row-key="summary.currency"
      max-height="360"
      empty-text="当前窗口没有有效币种分组"
    >
      <el-table-column prop="summary.currency" label="币种" width="80" />
      <el-table-column prop="summary.terminalCount" label="终态" min-width="80" />
      <el-table-column prop="summary.comparableCostCount" label="可比样本" min-width="100" />
      <el-table-column prop="storedAboveRecomputedCount" label="存储高于重算" min-width="130" />
      <el-table-column prop="summary.consistentCostCount" label="存储等于重算" min-width="130" />
      <el-table-column prop="storedBelowRecomputedCount" label="存储低于重算" min-width="130" />
      <el-table-column prop="summary.uncomparableCostCount" label="不可比" min-width="90" />
      <el-table-column label="存储费用" min-width="150"
        ><template #default="{ row }">{{
          money(row.summary.storedCost)
        }}</template></el-table-column
      >
      <el-table-column label="历史快照重算" min-width="150"
        ><template #default="{ row }">{{
          money(row.summary.recomputedCost)
        }}</template></el-table-column
      >
      <el-table-column label="差额合计" min-width="150"
        ><template #default="{ row }">{{
          money(row.summary.differenceCost)
        }}</template></el-table-column
      >
      <el-table-column label="绝对差额合计" min-width="150"
        ><template #default="{ row }">{{
          money(row.summary.absoluteDifferenceCost)
        }}</template></el-table-column
      >
    </el-table>
    <el-pagination
      :current-page="pageIndex"
      :page-size="20"
      :total="value.currencies.totalCount"
      layout="prev, pager, next"
      @current-change="(page: number) => $emit('page', page)"
    />
    <p class="note">
      {{ value.currencies.totalCount }}
      个币种分组；分页仅改变币种表范围，日与窗口摘要不按页缩小样本。每次翻页重新观察，结果可能改变。
    </p>
  </section>
</template>

<style scoped lang="scss">
section {
  margin-top: 24px;
  min-width: 0;
}
.note {
  color: var(--el-text-color-secondary);
  line-height: 1.6;
}
.details {
  padding: 0 16px;
  line-height: 1.6;
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
.el-pagination {
  margin-top: 12px;
}
</style>
