<script setup lang="ts">
import type { AiDemoBusinessOrderTableData } from '../api/ai'

defineProps<{ table: AiDemoBusinessOrderTableData }>()
const statuses: Record<AiDemoBusinessOrderTableData['items'][number]['approvalStatus'], string> = {
  Draft: '草稿',
  Pending: '审批中',
  Approved: '已通过',
  Rejected: '已拒绝',
  Withdrawn: '已撤回',
  Cancelled: '已取消',
}
</script>

<template>
  <section aria-label="Demo 业务单据查询表格">
    <p>匹配总量：{{ table.totalCount }} 单；当前展示：{{ table.displayedRowCount }} 行。</p>
    <div class="table-viewport">
      <table>
        <thead>
          <tr>
            <th>单号</th>
            <th>标题</th>
            <th>单据 ID</th>
            <th>部门 ID</th>
            <th>审批状态</th>
            <th>创建时间</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in table.items" :key="row.id">
            <td>{{ row.orderNo }}</td>
            <td>{{ row.title }}</td>
            <td>{{ row.id }}</td>
            <td>{{ row.departmentId ?? '未设置' }}</td>
            <td>{{ statuses[row.approvalStatus] ?? '未知状态' }}</td>
            <td>{{ new Date(row.createdAt).toLocaleString() }}</td>
          </tr>
        </tbody>
      </table>
    </div>
    <p v-if="!table.items.length">当前没有展示行；请结合匹配总量及截断提示查看。</p>
  </section>
</template>

<style scoped>
.table-viewport {
  overflow-x: auto;
}
table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
th,
td {
  padding: 8px;
  text-align: left;
  border-bottom: 1px solid var(--el-border-color);
}
td {
  overflow-wrap: anywhere;
  min-width: 80px;
}
</style>
