<script setup lang="ts">
defineOptions({ name: 'AiGovernance' })

import { Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import {
  getAiBudgetPolicies,
  getAiModelRoutes,
  getAiModelRouteProviders,
  saveAiBudgetPolicy,
  saveAiModelRoute,
  type AiBudgetPolicy,
  type AiModelRoutePolicy,
  type AiModelRouteProviderOption,
} from '../../../api/ai'
import PageContainer from '../../../components/PageContainer/index.vue'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const canManage = computed(() => authStore.hasPermission('ai:governance:manage'))
const loading = ref(false)
const activeTab = ref('routes')
const routes = ref<AiModelRoutePolicy[]>([])
const budgets = ref<AiBudgetPolicy[]>([])
const providers = ref<AiModelRouteProviderOption[]>([])
const routeDialogVisible = ref(false)
const budgetDialogVisible = ref(false)
const routeFormRef = ref<FormInstance>()
const budgetFormRef = ref<FormInstance>()

const routeForm = reactive({
  agentCode: 'permission-platform-agent',
  primaryProviderConfigId: '',
  canaryProviderConfigId: '',
  canaryPercentage: 0,
  fallbackProviderConfigId: '',
  isEnabled: true,
  concurrencyToken: '',
})
const budgetForm = reactive({
  policyCode: '',
  policyName: '',
  scopeType: 1 as 1 | 2,
  userId: '',
  monthlyLimit: 0,
  currency: 'CNY',
  isHardLimit: true,
  alertThresholdPercentage: 80,
  isEnabled: true,
  concurrencyToken: '',
})
const routeRules: FormRules = {
  agentCode: [{ required: true, message: '请输入 AgentCode', trigger: 'blur' }],
  primaryProviderConfigId: [{ required: true, message: '请选择主 Provider', trigger: 'change' }],
}
const budgetRules: FormRules = {
  policyCode: [{ required: true, message: '请输入策略编码', trigger: 'blur' }],
  policyName: [{ required: true, message: '请输入策略名称', trigger: 'blur' }],
  currency: [{ required: true, message: '请输入币种', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const [routeData, budgetData, providerData] = await Promise.all([
      getAiModelRoutes(),
      getAiBudgetPolicies(),
      getAiModelRouteProviders(),
    ])
    routes.value = routeData
    budgets.value = budgetData
    providers.value = providerData
  } finally {
    loading.value = false
  }
}

function providerName(id?: string) {
  if (!id) return '-'
  const provider = providers.value.find((item) => item.id === id)
  return provider ? `${provider.providerName} / ${provider.modelName}` : id
}

function providerUnavailable(item: AiModelRouteProviderOption) {
  return !item.isEnabled || !item.isComplianceConfirmed || !item.supportsTools
}

function openRoute(item?: AiModelRoutePolicy) {
  Object.assign(routeForm, {
    agentCode: item?.agentCode ?? 'permission-platform-agent',
    primaryProviderConfigId: item?.primaryProviderConfigId ?? '',
    canaryProviderConfigId: item?.canaryProviderConfigId ?? '',
    canaryPercentage: item?.canaryPercentage ?? 0,
    fallbackProviderConfigId: item?.fallbackProviderConfigId ?? '',
    isEnabled: item?.isEnabled ?? true,
    concurrencyToken: item?.concurrencyToken ?? '',
  })
  routeDialogVisible.value = true
}

async function saveRoute() {
  await routeFormRef.value?.validate()
  await saveAiModelRoute({
    tenantId: authStore.effectiveTenantId || undefined,
    agentCode: routeForm.agentCode,
    primaryProviderConfigId: routeForm.primaryProviderConfigId,
    canaryProviderConfigId: routeForm.canaryProviderConfigId || undefined,
    canaryPercentage: routeForm.canaryProviderConfigId ? routeForm.canaryPercentage : 0,
    fallbackProviderConfigId: routeForm.fallbackProviderConfigId || undefined,
    isEnabled: routeForm.isEnabled,
    concurrencyToken: routeForm.concurrencyToken || undefined,
  })
  ElMessage.success('模型路由已保存')
  routeDialogVisible.value = false
  await loadData()
}

function openBudget(item?: AiBudgetPolicy) {
  Object.assign(budgetForm, {
    policyCode: item?.policyCode ?? '',
    policyName: item?.policyName ?? '',
    scopeType: item?.scopeType ?? 1,
    userId: item?.userId ?? '',
    monthlyLimit: item?.monthlyLimit ?? 0,
    currency: item?.currency ?? 'CNY',
    isHardLimit: item?.isHardLimit ?? true,
    alertThresholdPercentage: item?.alertThresholdPercentage ?? 80,
    isEnabled: item?.isEnabled ?? true,
    concurrencyToken: item?.concurrencyToken ?? '',
  })
  budgetDialogVisible.value = true
}

async function saveBudget() {
  await budgetFormRef.value?.validate()
  await saveAiBudgetPolicy({
    tenantId: authStore.effectiveTenantId || undefined,
    policyCode: budgetForm.policyCode,
    policyName: budgetForm.policyName,
    scopeType: budgetForm.scopeType,
    userId: budgetForm.scopeType === 2 ? budgetForm.userId || undefined : undefined,
    monthlyLimit: budgetForm.monthlyLimit,
    currency: budgetForm.currency.trim().toUpperCase(),
    isHardLimit: budgetForm.isHardLimit,
    alertThresholdPercentage: budgetForm.alertThresholdPercentage,
    isEnabled: budgetForm.isEnabled,
    concurrencyToken: budgetForm.concurrencyToken || undefined,
  })
  ElMessage.success('预算策略已保存')
  budgetDialogVisible.value = false
  await loadData()
}

loadData()
</script>

<template>
  <PageContainer title="AI 模型治理">
    <template #actions>
      <el-tooltip content="刷新"><el-button :icon="Refresh" circle @click="loadData" /></el-tooltip>
    </template>

    <el-tabs v-model="activeTab">
      <el-tab-pane label="模型路由" name="routes">
        <div class="section-toolbar">
          <span>按会话稳定灰度；仅瞬时故障切换至备用 Provider。</span>
          <el-button v-if="canManage" type="primary" :icon="Plus" @click="openRoute()"
            >新增路由</el-button
          >
        </div>
        <el-table v-loading="loading" :data="routes" border>
          <el-table-column prop="agentCode" label="AgentCode" min-width="190" />
          <el-table-column label="主 Provider" min-width="210"
            ><template #default="{ row }">{{
              providerName(row.primaryProviderConfigId)
            }}</template></el-table-column
          >
          <el-table-column label="灰度 Provider" min-width="210"
            ><template #default="{ row }">{{
              providerName(row.canaryProviderConfigId)
            }}</template></el-table-column
          >
          <el-table-column prop="canaryPercentage" label="灰度比例" width="100"
            ><template #default="{ row }">{{ row.canaryPercentage }}%</template></el-table-column
          >
          <el-table-column label="备用 Provider" min-width="210"
            ><template #default="{ row }">{{
              providerName(row.fallbackProviderConfigId)
            }}</template></el-table-column
          >
          <el-table-column label="状态" width="90"
            ><template #default="{ row }"
              ><el-tag :type="row.isEnabled ? 'success' : 'info'">{{
                row.isEnabled ? '启用' : '停用'
              }}</el-tag></template
            ></el-table-column
          >
          <el-table-column v-if="canManage" label="操作" width="80"
            ><template #default="{ row }"
              ><el-tooltip content="编辑"
                ><el-button text :icon="Edit" @click="openRoute(row)" /></el-tooltip></template
          ></el-table-column>
        </el-table>
      </el-tab-pane>

      <el-tab-pane label="预算策略" name="budgets">
        <div class="section-toolbar">
          <span>预算按币种分别计算，不执行自动汇率换算。</span>
          <el-button v-if="canManage" type="primary" :icon="Plus" @click="openBudget()"
            >新增预算</el-button
          >
        </div>
        <el-table v-loading="loading" :data="budgets" border>
          <el-table-column prop="policyCode" label="策略编码" min-width="150" />
          <el-table-column prop="policyName" label="名称" min-width="160" />
          <el-table-column label="范围" width="90"
            ><template #default="{ row }">{{
              row.scopeType === 1 ? '租户' : '用户'
            }}</template></el-table-column
          >
          <el-table-column prop="userId" label="用户 ID" min-width="220" show-overflow-tooltip />
          <el-table-column label="月度上限" min-width="130"
            ><template #default="{ row }"
              >{{ row.currency }} {{ row.monthlyLimit }}</template
            ></el-table-column
          >
          <el-table-column label="本月用量" min-width="190">
            <template #default="{ row }">
              <el-progress
                :percentage="
                  Math.min(100, Math.round((row.currentAmount / row.monthlyLimit) * 100))
                "
                :status="
                  row.isLimitExceeded
                    ? 'exception'
                    : row.isAlertThresholdExceeded
                      ? 'warning'
                      : undefined
                "
              />
            </template>
          </el-table-column>
          <el-table-column label="阈值" width="90"
            ><template #default="{ row }"
              >{{ row.alertThresholdPercentage }}%</template
            ></el-table-column
          >
          <el-table-column label="限制" width="90"
            ><template #default="{ row }">{{
              row.isHardLimit ? '硬限制' : '仅告警'
            }}</template></el-table-column
          >
          <el-table-column label="状态" width="90"
            ><template #default="{ row }"
              ><el-tag :type="row.isEnabled ? 'success' : 'info'">{{
                row.isEnabled ? '启用' : '停用'
              }}</el-tag></template
            ></el-table-column
          >
          <el-table-column v-if="canManage" label="操作" width="80"
            ><template #default="{ row }"
              ><el-tooltip content="编辑"
                ><el-button text :icon="Edit" @click="openBudget(row)" /></el-tooltip></template
          ></el-table-column>
        </el-table>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="routeDialogVisible" title="模型路由" width="680px">
      <el-form ref="routeFormRef" :model="routeForm" :rules="routeRules" label-width="130px">
        <el-form-item label="AgentCode" prop="agentCode"
          ><el-input v-model="routeForm.agentCode" :disabled="Boolean(routeForm.concurrencyToken)"
        /></el-form-item>
        <el-form-item label="主 Provider" prop="primaryProviderConfigId"
          ><el-select v-model="routeForm.primaryProviderConfigId" filterable
            ><el-option
              v-for="item in providers"
              :key="item.id"
              :label="providerName(item.id)"
              :value="item.id"
              :disabled="providerUnavailable(item)" /></el-select
        ></el-form-item>
        <el-form-item label="灰度 Provider"
          ><el-select v-model="routeForm.canaryProviderConfigId" clearable filterable
            ><el-option
              v-for="item in providers"
              :key="item.id"
              :label="providerName(item.id)"
              :value="item.id"
              :disabled="providerUnavailable(item)" /></el-select
        ></el-form-item>
        <el-form-item label="灰度比例"
          ><el-slider
            v-model="routeForm.canaryPercentage"
            :disabled="!routeForm.canaryProviderConfigId"
            :min="1"
            :max="100"
            show-input
        /></el-form-item>
        <el-form-item label="备用 Provider"
          ><el-select v-model="routeForm.fallbackProviderConfigId" clearable filterable
            ><el-option
              v-for="item in providers"
              :key="item.id"
              :label="providerName(item.id)"
              :value="item.id"
              :disabled="providerUnavailable(item)" /></el-select
        ></el-form-item>
        <el-form-item label="启用"><el-switch v-model="routeForm.isEnabled" /></el-form-item>
      </el-form>
      <template #footer
        ><el-button @click="routeDialogVisible = false">取消</el-button
        ><el-button type="primary" @click="saveRoute">保存</el-button></template
      >
    </el-dialog>

    <el-dialog v-model="budgetDialogVisible" title="预算策略" width="640px">
      <el-form ref="budgetFormRef" :model="budgetForm" :rules="budgetRules" label-width="130px">
        <el-form-item label="策略编码" prop="policyCode"
          ><el-input
            v-model="budgetForm.policyCode"
            :disabled="Boolean(budgetForm.concurrencyToken)"
        /></el-form-item>
        <el-form-item label="策略名称" prop="policyName"
          ><el-input v-model="budgetForm.policyName"
        /></el-form-item>
        <el-form-item label="范围"
          ><el-segmented
            v-model="budgetForm.scopeType"
            :options="[
              { label: '租户', value: 1 },
              { label: '用户', value: 2 },
            ]"
        /></el-form-item>
        <el-form-item v-if="budgetForm.scopeType === 2" label="用户 ID"
          ><el-input v-model="budgetForm.userId"
        /></el-form-item>
        <el-form-item label="月度上限"
          ><el-input-number v-model="budgetForm.monthlyLimit" :min="0.000001" :precision="6"
        /></el-form-item>
        <el-form-item label="币种" prop="currency"
          ><el-input v-model="budgetForm.currency" maxlength="3"
        /></el-form-item>
        <el-form-item label="告警阈值"
          ><el-slider v-model="budgetForm.alertThresholdPercentage" :min="1" :max="100" show-input
        /></el-form-item>
        <el-form-item label="硬限制"><el-switch v-model="budgetForm.isHardLimit" /></el-form-item>
        <el-form-item label="启用"><el-switch v-model="budgetForm.isEnabled" /></el-form-item>
      </el-form>
      <template #footer
        ><el-button @click="budgetDialogVisible = false">取消</el-button
        ><el-button type="primary" @click="saveBudget">保存</el-button></template
      >
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.section-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 14px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

:deep(.el-select) {
  width: 100%;
}

@media (max-width: 720px) {
  .section-toolbar {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
