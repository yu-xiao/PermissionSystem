<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { useTenantStore } from '../../stores/tenant'
import StateView from '../../components/StateView.vue'

const router = useRouter()
const authStore = useAuthStore()
const tenantStore = useTenantStore()
const loading = ref(true)
const saving = ref(false)
const error = ref('')
const notice = ref('')
const editing = ref(false)
const changingPassword = ref(false)
const profile = ref<Record<string, any>>()
const form = reactive({ nickName: '', realName: '', email: '', phoneNumber: '' })
const passwordForm = reactive({ oldPassword: '', newPassword: '', confirmPassword: '' })

const displayName = computed(() => profile.value?.nickName || profile.value?.realName || profile.value?.userName || '用户')
const initials = computed(() => String(displayName.value).slice(0, 1).toUpperCase())
const tenantName = computed(() => tenantStore.currentTenant?.name || profile.value?.tenantName || authStore.currentUser?.tenantId || '当前租户')
const isSuperAdmin = computed(() => authStore.isSuperAdmin)

async function load() {
  loading.value = true; error.value = ''
  try {
    const result = await authStore.loadMyProfile()
    profile.value = result as Record<string, any>
    form.nickName = result.nickName || ''
    form.realName = result.realName || ''
    form.email = result.email || ''
    form.phoneNumber = result.phoneNumber || ''
    if (isSuperAdmin.value && !tenantStore.initialized) await tenantStore.loadTenants()
  } catch (reason) { error.value = reason instanceof Error ? reason.message : '个人资料加载失败。' } finally { loading.value = false }
}

function startEdit() { editing.value = true; notice.value = ''; error.value = '' }
function cancelEdit() { editing.value = false }
async function saveProfile() {
  saving.value = true; error.value = ''; notice.value = ''
  try { profile.value = await authStore.updateProfile(form) as Record<string, any>; editing.value = false; notice.value = '资料已更新。' } catch (reason) { error.value = reason instanceof Error ? reason.message : '保存失败。' } finally { saving.value = false }
}
function openPassword() { changingPassword.value = true; passwordForm.oldPassword = ''; passwordForm.newPassword = ''; passwordForm.confirmPassword = ''; error.value = '' }
async function savePassword() {
  if (passwordForm.newPassword.length < 8 || passwordForm.newPassword !== passwordForm.confirmPassword) { error.value = '请确认新密码不少于 8 位且两次输入一致。'; return }
  saving.value = true; error.value = ''
  try { await authStore.changePassword(passwordForm); changingPassword.value = false; notice.value = '密码已更新，请重新登录。'; await authStore.logout() } catch (reason) { error.value = reason instanceof Error ? reason.message : '密码修改失败。' } finally { saving.value = false }
}
async function switchTenant(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  if (!value || value === tenantStore.targetTenantId) return
  try { tenantStore.selectTenant(value); notice.value = '租户已切换，正在刷新数据。'; await authStore.reloadAuthorizationState() } catch (reason) { error.value = reason instanceof Error ? reason.message : '租户切换失败。' }
}
async function logout() { saving.value = true; try { await authStore.logout(); await router.replace('/login') } finally { saving.value = false } }
onMounted(() => void load())
</script>

<template>
  <section class="profile-view">
    <StateView v-if="loading" kind="loading" title="正在加载个人资料" />
    <StateView v-else-if="error && !profile" kind="error" :hint="error" action-label="重新加载" @action="load" />
    <template v-else>
      <div class="surface profile-head"><div class="avatar" aria-hidden="true">{{ initials }}</div><div class="profile-head__identity"><div class="profile-head__name">{{ displayName }}</div><div class="profile-head__sub">{{ profile?.userName || profile?.email || '已登录用户' }}</div></div><button class="button button--text" type="button" @click="startEdit">编辑</button></div>
      <p v-if="notice" class="notice" role="status">{{ notice }}</p><p v-if="error" class="inline-error" role="alert">{{ error }}</p>

      <section class="surface profile-section"><h2>账户信息</h2><dl class="detail-grid"><dt>姓名</dt><dd>{{ profile?.realName || '--' }}</dd><dt>邮箱</dt><dd>{{ profile?.email || '--' }}</dd><dt>手机号</dt><dd>{{ profile?.phoneNumber || '--' }}</dd><dt>部门</dt><dd>{{ profile?.departmentName || '--' }}</dd></dl></section>
      <section class="surface profile-section"><h2>当前租户</h2><div v-if="isSuperAdmin && tenantStore.tenants.length" class="tenant-select"><select :value="tenantStore.targetTenantId || ''" aria-label="选择租户" @change="switchTenant"><option v-for="tenant in tenantStore.tenants" :key="tenant.tenantId || tenant.id" :value="tenant.tenantId || tenant.id">{{ tenant.name }}</option></select></div><div v-else class="tenant-static">{{ tenantName }}</div></section>
      <section class="surface settings-list"><button class="settings-item" type="button" @click="openPassword"><span class="settings-item__icon" aria-hidden="true">♢</span><span class="settings-item__label">修改密码</span><span class="settings-item__arrow" aria-hidden="true">›</span></button><button class="settings-item" type="button" @click="router.push('/sessions')"><span class="settings-item__icon" aria-hidden="true">▣</span><span class="settings-item__label">当前会话</span><span class="settings-item__arrow" aria-hidden="true">›</span></button></section>
      <button class="button button--danger button--block logout-button" type="button" :disabled="saving" @click="logout">退出当前设备</button>
    </template>

    <div v-if="editing" class="action-modal" role="dialog" aria-modal="true" aria-label="编辑个人资料"><button class="action-modal__backdrop" type="button" aria-label="关闭" @click="cancelEdit" /><section class="action-modal__sheet"><div class="action-modal__handle" /><h2>编辑资料</h2><div class="form-stack"><div class="form-field"><label for="profile-nickname">昵称</label><input id="profile-nickname" v-model="form.nickName" maxlength="80" /></div><div class="form-field"><label for="profile-realname">姓名</label><input id="profile-realname" v-model="form.realName" maxlength="80" /></div><div class="form-field"><label for="profile-email">邮箱</label><input id="profile-email" v-model="form.email" type="email" maxlength="160" /></div><div class="form-field"><label for="profile-phone">手机号</label><input id="profile-phone" v-model="form.phoneNumber" inputmode="tel" maxlength="30" /></div></div><div class="action-modal__buttons"><button class="button button--ghost" type="button" @click="cancelEdit">取消</button><button class="button button--primary" type="button" :disabled="saving" @click="saveProfile">{{ saving ? '保存中…' : '保存' }}</button></div></section></div>
    <div v-if="changingPassword" class="action-modal" role="dialog" aria-modal="true" aria-label="修改密码"><button class="action-modal__backdrop" type="button" aria-label="关闭" @click="changingPassword = false" /><section class="action-modal__sheet"><div class="action-modal__handle" /><h2>修改密码</h2><div class="form-stack"><div class="form-field"><label for="old-password">当前密码</label><input id="old-password" v-model="passwordForm.oldPassword" type="password" autocomplete="current-password" /></div><div class="form-field"><label for="new-password">新密码</label><input id="new-password" v-model="passwordForm.newPassword" type="password" autocomplete="new-password" /></div><div class="form-field"><label for="confirm-password">确认新密码</label><input id="confirm-password" v-model="passwordForm.confirmPassword" type="password" autocomplete="new-password" /></div></div><div class="action-modal__buttons"><button class="button button--ghost" type="button" @click="changingPassword = false">取消</button><button class="button button--primary" type="button" :disabled="saving" @click="savePassword">{{ saving ? '提交中…' : '确认修改' }}</button></div></section></div>
  </section>
</template>

<style scoped>
.profile-head__identity { min-width: 0; flex: 1; }
.profile-section { margin-top: 12px; padding: 16px; }
.profile-section h2 { margin: 0 0 14px; font-size: 16px; }
.tenant-select select { width: 100%; min-height: 42px; padding: 0 11px; border: 1px solid var(--mobile-border); border-radius: 8px; color: var(--mobile-text); background: var(--mobile-surface); }
.tenant-static { color: var(--mobile-text); font-size: 14px; }
.logout-button { margin-top: 18px; }
.notice { margin: 12px 0 0; padding: 9px 10px; border-radius: 8px; color: var(--mobile-success); background: var(--mobile-success-soft); font-size: 12px; }
.inline-error { margin: 12px 0 0; padding: 9px 10px; border-radius: 8px; color: var(--mobile-danger); background: var(--mobile-danger-soft); font-size: 12px; }
.action-modal { position: fixed; z-index: 60; inset: 0; display: grid; align-items: end; }
.action-modal__backdrop { position: absolute; inset: 0; border: 0; background: rgba(12,22,38,.45); }
.action-modal__sheet { position: relative; width: min(100%, 768px); max-height: 92vh; margin: 0 auto; overflow: auto; padding: 10px 16px calc(18px + env(safe-area-inset-bottom)); border-radius: 17px 17px 0 0; background: var(--mobile-surface); }
.action-modal__handle { width: 38px; height: 4px; margin: 0 auto 14px; border-radius: 3px; background: var(--mobile-border); }
.action-modal__sheet h2 { margin: 0 0 16px; font-size: 17px; }
.action-modal__buttons { display: flex; gap: 8px; margin-top: 18px; }
.action-modal__buttons .button { flex: 1; }
</style>
