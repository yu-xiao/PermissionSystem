<script setup lang="ts">
import type { AiUserTableData } from '../api/ai'
defineProps<{ table: AiUserTableData }>()
</script>

<template>
  <section aria-label="用户查询表格">
    <p>匹配总量：{{ table.totalCount }}；当前展示：{{ table.displayedRowCount }} 行。</p>
    <div class="table-viewport">
      <table>
        <thead>
          <tr>
            <th>用户名</th>
            <th>显示名</th>
            <th>用户 ID</th>
            <th>部门 ID</th>
            <th>状态</th>
            <th>创建时间</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in table.items" :key="row.id">
            <td>{{ row.userName }}</td>
            <td>{{ row.displayName }}</td>
            <td>{{ row.id }}</td>
            <td>{{ row.departmentId ?? '未设置' }}</td>
            <td>{{ row.isEnabled ? '启用' : '停用' }}</td>
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
