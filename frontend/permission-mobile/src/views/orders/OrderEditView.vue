<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { createDemoApprovalOrder, getDemoApprovalOrder, updateDemoApprovalOrder } from '../../api/demoApprovalOrder'
import { createDemoBusinessOrder, getDemoBusinessOrder, updateDemoBusinessOrder } from '../../api/demoBusinessOrder'
import { usePermissionStore } from '../../stores/permission'

const route = useRoute(); const router = useRouter()
const permission = usePermissionStore()
const id = computed(() => typeof route.params.id === 'string' ? route.params.id : '')
const kind = computed(() => route.query.kind === 'approval' ? 'approval' : 'business')
const loading = ref(Boolean(id.value)); const saving = ref(false); const error = ref('')
const form = reactive({ title: '', customerName: '', amount: '', departmentId: '' })
const isBusiness = computed(() => kind.value === 'business')
async function load() {
  if (!id.value) return
  loading.value = true; error.value = ''
  try { const item = isBusiness.value ? await getDemoBusinessOrder(id.value) : await getDemoApprovalOrder(id.value); form.title = item.title; form.amount = String(item.amount ?? ''); form.departmentId = item.departmentId || ''; if ('customerName' in item) form.customerName = item.customerName } catch (reason) { error.value = reason instanceof Error ? reason.message : '草稿加载失败。' } finally { loading.value = false }
}
async function save() {
  const requiredPermission = id.value
    ? (isBusiness.value ? 'demo-business-order:update' : 'demo-approval-order:update')
    : (isBusiness.value ? 'demo-business-order:create' : 'demo-approval-order:create')
  if (!permission.hasPermission(requiredPermission)) { error.value = '当前账号没有保存此类单据的权限。'; return }
  if (!form.title.trim() || !form.amount || Number(form.amount) < 0 || (isBusiness.value && !form.customerName.trim())) { error.value = isBusiness.value ? '请填写标题、客户和有效金额。' : '请填写标题和有效金额。'; return }
  saving.value = true; error.value = ''
  try {
    if (isBusiness.value) { const payload = { title: form.title.trim(), customerName: form.customerName.trim(), amount: Number(form.amount), departmentId: form.departmentId.trim() || undefined }; const item = id.value ? await updateDemoBusinessOrder(id.value, payload) : await createDemoBusinessOrder(payload); await router.replace({ path: `/orders/${item.id}`, query: { kind: kind.value } }) }
    else { const payload = { title: form.title.trim(), amount: Number(form.amount), departmentId: form.departmentId.trim() || undefined }; const item = id.value ? await updateDemoApprovalOrder(id.value, payload) : await createDemoApprovalOrder(payload); await router.replace({ path: `/orders/${item.id}`, query: { kind: kind.value } }) }
  } catch (reason) { error.value = reason instanceof Error ? reason.message : '保存失败，请稍后重试。' } finally { saving.value = false }
}
onMounted(() => void load())
</script>

<template>
  <section class="order-edit-view"><div v-if="loading" class="surface edit-loading"><span class="spinner" /><span>正在加载草稿</span></div><template v-else><p v-if="error" class="inline-error" role="alert">{{ error }}</p><div class="surface edit-form"><div class="form-stack"><div class="form-field"><label for="order-title">标题</label><input id="order-title" v-model="form.title" maxlength="160" placeholder="请输入单据标题" /></div><div v-if="isBusiness" class="form-field"><label for="customer-name">客户</label><input id="customer-name" v-model="form.customerName" maxlength="120" placeholder="请输入客户名称" /></div><div class="form-field"><label for="order-amount">金额</label><input id="order-amount" v-model="form.amount" inputmode="decimal" type="number" min="0" step="0.01" placeholder="0.00" /></div><div class="form-field"><label for="department-id">部门 ID（可选）</label><input id="department-id" v-model="form.departmentId" maxlength="80" placeholder="请输入部门 ID" /></div></div></div><div class="edit-actions"><button class="button button--ghost" type="button" @click="router.back()">取消</button><button class="button button--primary" type="button" :disabled="saving" @click="save">{{ saving ? '保存中…' : '保存草稿' }}</button></div></template></section>
</template>

<style scoped>
.edit-form { padding: 16px; }
.edit-actions { display: flex; gap: 8px; margin-top: 14px; }
.edit-actions .button { flex: 1; }
.edit-loading { display: grid; min-height: 180px; place-items: center; align-content: center; gap: 10px; color: var(--mobile-text-secondary); font-size: 13px; }
.inline-error { margin: 0 0 12px; padding: 9px 10px; border-radius: 8px; color: var(--mobile-danger); background: var(--mobile-danger-soft); font-size: 12px; }
</style>
