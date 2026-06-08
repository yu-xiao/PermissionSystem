<script setup lang="ts">
defineOptions({
  name: 'SsoRoleMapping',
})

import { Delete, Plus } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { ref } from 'vue'
import { getRoles, type RoleItem } from '../../../api/roles'
import { getSsoProviders, type SsoProviderListItem } from '../../../api/ssoProvider'
import {
  getSsoRoleMappings,
  saveSsoRoleMappings,
  type SsoRoleMappingItem,
} from '../../../api/ssoRoleMapping'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const loading = ref(false)
const saving = ref(false)
const providers = ref<SsoProviderListItem[]>([])
const roles = ref<RoleItem[]>([])
const providerId = ref('')
const mappings = ref<SsoRoleMappingItem[]>([])

async function loadProviders() {
  const result = await getSsoProviders({ pageIndex: 1, pageSize: 500 })
  providers.value = result.items
  if (!providerId.value && providers.value.length > 0) {
    providerId.value = providers.value[0].id
  }
}

async function loadRoles() {
  const result = await getRoles({ pageIndex: 1, pageSize: 500, isEnabled: true })
  roles.value = result.items.filter((role) => !role.isSuperAdminRole && role.code !== 'SuperAdmin')
}

async function loadData() {
  if (!providerId.value) {
    mappings.value = []
    return
  }

  loading.value = true
  try {
    mappings.value = await getSsoRoleMappings(providerId.value)
  } finally {
    loading.value = false
  }
}

function addRow() {
  mappings.value.push({
    externalRole: '',
    localRoleId: '',
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
      externalRole: item.externalRole.trim(),
      localRoleId: item.localRoleId,
    }))
    .filter((item) => item.externalRole && item.localRoleId)
  if (payload.length !== mappings.value.length) {
    ElMessage.warning('请补全外部角色和本地角色')
    return
  }

  saving.value = true
  try {
    mappings.value = await saveSsoRoleMappings(providerId.value, payload)
    ElMessage.success('保存成功')
  } finally {
    saving.value = false
  }
}

async function init() {
  await Promise.all([loadProviders(), loadRoles()])
  await loadData()
}

init()
</script>

<template>
  <PageContainer title="SSO 角色映射" description="将外部 group / role 映射到本地角色。">
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
        <el-button v-permission="'sso:role-mapping:view'" type="primary" @click="loadData">查询</el-button>
        <el-button v-permission="'sso:role-mapping:update'" :icon="Plus" @click="addRow">新增映射</el-button>
        <el-button v-permission="'sso:role-mapping:update'" type="primary" :loading="saving" @click="save">保存</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="mappings" border>
      <el-table-column label="外部角色" min-width="220">
        <template #default="{ row }">
          <el-input v-model="row.externalRole" placeholder="如 manager" />
        </template>
      </el-table-column>
      <el-table-column label="本地角色" min-width="260">
        <template #default="{ row }">
          <el-select v-model="row.localRoleId" filterable placeholder="请选择本地角色" class="full-width">
            <el-option v-for="role in roles" :key="role.id" :label="`${role.name} (${role.code})`" :value="role.id" />
          </el-select>
        </template>
      </el-table-column>
      <el-table-column prop="localRoleCode" label="角色编码" min-width="140" />
      <el-table-column label="操作" width="100" fixed="right">
        <template #default="{ $index }">
          <el-button v-permission="'sso:role-mapping:update'" :icon="Delete" link type="danger" @click="removeRow($index)" />
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
