<script setup lang="ts">
defineOptions({
  name: 'SsoDepartmentMapping',
})

import { Delete, Plus } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, ref } from 'vue'
import { getDepartmentTree, type DepartmentItem } from '../../../api/departments'
import { getSsoProviders, type SsoProviderListItem } from '../../../api/ssoProvider'
import {
  getSsoDepartmentMappings,
  saveSsoDepartmentMappings,
  type SsoDepartmentMappingItem,
} from '../../../api/ssoDepartmentMapping'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.currentUser?.tenantId ?? '')
const loading = ref(false)
const saving = ref(false)
const providers = ref<SsoProviderListItem[]>([])
const departments = ref<DepartmentItem[]>([])
const providerId = ref('')
const mappings = ref<SsoDepartmentMappingItem[]>([])

async function loadProviders() {
  const result = await getSsoProviders({ pageIndex: 1, pageSize: 500 })
  providers.value = result.items
  if (!providerId.value && providers.value.length > 0) {
    providerId.value = providers.value[0].id
  }
}

async function loadDepartments() {
  departments.value = await getDepartmentTree(tenantId.value)
}

async function loadData() {
  if (!providerId.value) {
    mappings.value = []
    return
  }

  loading.value = true
  try {
    mappings.value = await getSsoDepartmentMappings(providerId.value)
  } finally {
    loading.value = false
  }
}

function addRow() {
  mappings.value.push({
    externalDepartment: '',
    localDepartmentId: '',
  })
}

function removeRow(index: number) {
  mappings.value.splice(index, 1)
}

async function save() {
  if (!providerId.value) {
    ElMessage.warning('请选择 SSO Provider')
    return
  }

  const payload = mappings.value
    .map((item) => ({
      externalDepartment: item.externalDepartment.trim(),
      localDepartmentId: item.localDepartmentId,
    }))
    .filter((item) => item.externalDepartment && item.localDepartmentId)
  if (payload.length !== mappings.value.length) {
    ElMessage.warning('请补全外部部门和本地部门')
    return
  }

  saving.value = true
  try {
    mappings.value = await saveSsoDepartmentMappings(providerId.value, payload)
    ElMessage.success('保存成功')
  } finally {
    saving.value = false
  }
}

async function init() {
  await Promise.all([loadProviders(), loadDepartments()])
  await loadData()
}

init()
</script>

<template>
  <PageContainer title="SSO 部门映射" description="将外部部门映射到本地组织部门。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-select v-model="providerId" filterable placeholder="SSO Provider" style="width: 260px" @change="loadData">
          <el-option
            v-for="provider in providers"
            :key="provider.id"
            :label="`${provider.providerName} (${provider.providerCode})`"
            :value="provider.id"
          />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'sso:department-mapping:view'" type="primary" @click="loadData">查询</el-button>
        <el-button v-permission="'sso:department-mapping:update'" :icon="Plus" @click="addRow">新增映射</el-button>
        <el-button v-permission="'sso:department-mapping:update'" type="primary" :loading="saving" @click="save">保存</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="mappings" border>
      <el-table-column label="外部部门" min-width="240">
        <template #default="{ row }">
          <el-input v-model="row.externalDepartment" placeholder="如 HQ/Finance" />
        </template>
      </el-table-column>
      <el-table-column label="本地部门" min-width="280">
        <template #default="{ row }">
          <el-tree-select
            v-model="row.localDepartmentId"
            :data="departments"
            node-key="id"
            :props="{ label: 'name', children: 'children' }"
            filterable
            check-strictly
            class="full-width"
          />
        </template>
      </el-table-column>
      <el-table-column prop="localDepartmentCode" label="部门编码" min-width="140" />
      <el-table-column label="操作" width="100" fixed="right">
        <template #default="{ $index }">
          <el-button
            v-permission="'sso:department-mapping:update'"
            :icon="Delete"
            link
            type="danger"
            @click="removeRow($index)"
          />
        </template>
      </el-table-column>
    </el-table>
  </PageContainer>
</template>

<style scoped>
.full-width {
  width: 100%;
}
</style>
