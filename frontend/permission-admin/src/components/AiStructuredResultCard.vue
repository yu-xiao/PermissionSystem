<script setup lang="ts">
import { computed } from 'vue'
import type { AiContextReference, AiStructuredResult } from '../api/ai'
import AiPermissionDiagnosticCard from './AiPermissionDiagnosticCard.vue'
import AiQueryTableCard from './AiQueryTableCard.vue'
import AiStatisticsSummaryCard from './AiStatisticsSummaryCard.vue'
import AiControlledMetricsCard from './AiControlledMetricsCard.vue'
import AiDemoBusinessOrderTableCard from './AiDemoBusinessOrderTableCard.vue'
import AiKnowledgeCitationCard from './AiKnowledgeCitationCard.vue'

const props = defineProps<{ result: AiStructuredResult; selected?: boolean; busy?: boolean }>()
const emit = defineEmits<{ select: [reference: AiContextReference] }>()
const supported = computed(
  () =>
    props.result.version === 1 &&
    props.result.context?.version === 1 &&
    ((props.result.type === 'knowledge-citations' &&
      props.result.toolCode === 'knowledge.documents.search' &&
      props.result.toolVersion === '1.0' &&
      Array.isArray(props.result.knowledgeHits)) ||
      (props.result.type === 'permission-diagnostic' && props.result.diagnostic?.version === 1) ||
      (props.result.type === 'table' && Boolean(props.result.table)) ||
      (props.result.type === 'statistics-summary' && Boolean(props.result.statistics)) ||
      (props.result.type === 'demo-business-orders' &&
        props.result.toolCode === 'business.demo_business_order.query' &&
        props.result.toolVersion === '1.0' &&
        Boolean(props.result.demoOrders)) ||
      (props.result.type === 'controlled-report' &&
        props.result.toolCode === 'permission.reports.query_dataset' &&
        props.result.toolVersion === '2.0' &&
        Boolean(props.result.report) &&
        Boolean(props.result.metrics) !== Boolean(props.result.table))),
)
const labels: Record<string, string> = {
  kind: '诊断类型',
  targetUserId: '目标用户',
  menuId: '菜单 ID',
  permissionCode: '权限要求',
  keyword: '关键词',
  documentId: '文档 ID',
  isEnabled: '启用状态',
  limit: '展示上限',
  departmentScope: '部门过滤',
  userName: '用户名包含',
  module: '模块包含',
  startTime: '开始时间（含）',
  endTime: '结束时间（含）',
  reportDefinitionId: '报表 ID',
  mode: '查询模式',
  dimension: '统计维度',
  sort: '排序',
  departmentId: '部门 ID（与授权范围取交集）',
  approvalStatus: '审批状态',
}
const conditions = computed(() =>
  Object.entries(props.result.context?.parameters ?? {})
    .flatMap<[string, unknown]>(([key, value]) =>
      props.result.type === 'controlled-report' &&
      key === 'params' &&
      value &&
      typeof value === 'object'
        ? Object.entries(value)
        : [[key, value]],
    )
    .map(([key, value]) => ({
      key,
      label:
        props.result.type === 'controlled-report' && key === 'endTime'
          ? '创建结束时间（不含）'
          : props.result.type === 'controlled-report' && key === 'startTime'
            ? '创建开始时间（含）'
            : (labels[key] ?? key),
      value:
        value === null
          ? key === 'targetUserId'
            ? '本人'
            : '未设置'
          : value === 'CurrentDepartment'
            ? '本部门（不含下级，与授权范围取交集）'
            : value === 'Authorized'
              ? '当前授权范围'
              : typeof value === 'string'
                ? value
                : JSON.stringify(value),
    })),
)
const diagnostic = computed(() =>
  props.result.diagnostic
    ? {
        runId: props.result.runId,
        invocationId: props.result.invocationId,
        data: props.result.diagnostic,
      }
    : undefined,
)
</script>

<template>
  <article class="structured-result-card" aria-label="服务端结构化查询结果">
    <template v-if="supported">
      <header>
        <strong>{{
          result.type === 'knowledge-citations'
            ? '文档知识依据'
            : result.type === 'demo-business-orders'
              ? 'Demo 业务单据查询'
              : result.type === 'controlled-report'
                ? result.metrics
                  ? '受控用户指标'
                  : '受控用户明细'
                : result.type === 'table'
                  ? '用户查询'
                  : result.type === 'statistics-summary'
                    ? '日志统计'
                    : '权限证据'
        }}</strong>
        <el-button
          size="small"
          :disabled="busy"
          :type="selected ? 'primary' : 'default'"
          @click="emit('select', { runId: result.runId, invocationId: result.invocationId })"
        >
          {{ selected ? '已选择追问对象' : '继续追问此结果' }}
        </el-button>
      </header>
      <p>
        查询时间：{{ new Date(result.queriedAt).toLocaleString() }}（{{
          result.type === 'knowledge-citations'
            ? '当前授权下重新核验的固定版本来源'
            : result.type === 'demo-business-orders'
              ? '历史查询结果，非历史状态快照'
              : result.type === 'controlled-report'
                ? '历史查询结果，非历史人员快照'
                : '历史快照'
        }}）
      </p>
      <p>口径：{{ result.evaluationBasis }}</p>
      <dl class="conditions">
        <template v-for="condition in conditions" :key="condition.key"
          ><dt>{{ condition.label }}</dt>
          <dd>{{ condition.value }}</dd></template
        >
      </dl>
      <AiKnowledgeCitationCard
        v-if="result.type === 'knowledge-citations' && result.knowledgeHits"
        :hits="result.knowledgeHits"
      />
      <AiPermissionDiagnosticCard v-else-if="diagnostic" :diagnostic="diagnostic" />
      <AiDemoBusinessOrderTableCard v-else-if="result.demoOrders" :table="result.demoOrders" />
      <AiQueryTableCard v-else-if="result.table" :table="result.table" />
      <AiStatisticsSummaryCard v-else-if="result.statistics" :statistics="result.statistics" />
      <AiControlledMetricsCard v-else-if="result.metrics" :metrics="result.metrics" />
      <p v-if="result.isTruncated" class="result-note">结果已截断，展示数量不代表匹配总量。</p>
      <p>
        来源：{{ result.citation.sourceSystem }} · {{ result.toolCode }} v{{ result.toolVersion }}
      </p>
      <ul v-if="result.limitations.length">
        <li v-for="item in result.limitations" :key="item">{{ item }}</li>
      </ul>
    </template>
    <p v-else>该结构化结果版本或类型暂不支持展示，请重新查询。</p>
  </article>
</template>

<style scoped>
.structured-result-card {
  margin: 12px 0;
  padding: 14px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  overflow-wrap: anywhere;
  background: var(--el-bg-color);
  font-size: 13px;
}
header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
.conditions {
  display: grid;
  grid-template-columns: 110px minmax(0, 1fr);
  gap: 5px;
}
dd {
  margin: 0;
}
dt,
.result-note {
  color: var(--el-text-color-secondary);
}
</style>
