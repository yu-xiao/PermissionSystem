<script setup lang="ts">
import { Check, Close, EditPen, Link, Stamp } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref, watch } from 'vue'
import {
  cancelAiDocumentDraft,
  confirmAiDocumentDraft,
  executeAiDocumentDraft,
  updateAiDocumentDraft,
  type AiDocumentDraft,
  type AiDocumentExecutionResult,
} from '../api/ai'
import SensitiveVerificationDialog from './SensitiveVerificationDialog/index.vue'

const props = defineProps<{ draft: AiDocumentDraft; canExecute?: boolean }>()
const emit = defineEmits<{ updated: [draft: AiDocumentDraft] }>()

const editing = ref(false)
const saving = ref(false)
const executing = ref(false)
const executionResult = ref<AiDocumentExecutionResult>()
const sensitiveVerificationRef = ref<InstanceType<typeof SensitiveVerificationDialog>>()
const formRef = ref<FormInstance>()
const form = reactive({
  title: '',
  customerName: '',
  amount: undefined as number | undefined,
  departmentReference: '',
})
const rules: FormRules = {
  title: [{ required: true, message: '请输入标题', trigger: 'blur' }],
  customerName: [{ required: true, message: '请输入客户名称', trigger: 'blur' }],
  amount: [{ required: true, message: '请输入金额', trigger: 'change' }],
}

const editable = computed(() => [1, 2, 3].includes(props.draft.status))
const executable = computed(
  () => props.canExecute === true && props.draft.status === 3 && !editing.value,
)
const status = computed(() => {
  switch (props.draft.status) {
    case 1:
      return { text: '待补充', type: 'warning' as const }
    case 2:
      return { text: '校验失败', type: 'danger' as const }
    case 3:
      return { text: '校验通过', type: 'success' as const }
    case 4:
      return { text: '已过期', type: 'info' as const }
    case 5:
      return { text: '已取消', type: 'info' as const }
    default:
      return { text: '已创建', type: 'success' as const }
  }
})

watch(
  () => props.draft,
  (value) => {
    Object.assign(form, {
      title: value.payload.title ?? '',
      customerName: value.payload.customerName ?? '',
      amount: value.payload.amount,
      departmentReference: value.payload.departmentCode ?? value.payload.departmentReference ?? '',
    })
    executionResult.value = value.execution
  },
  { immediate: true },
)

async function save() {
  await formRef.value?.validate()
  saving.value = true
  try {
    const updated = await updateAiDocumentDraft(props.draft.id, {
      title: form.title,
      customerName: form.customerName,
      amount: form.amount,
      departmentReference: form.departmentReference || undefined,
      concurrencyToken: props.draft.concurrencyToken,
    })
    emit('updated', updated)
    editing.value = false
    ElMessage.success(updated.status === 3 ? '草稿校验通过' : '草稿已更新，请检查校验结果')
  } finally {
    saving.value = false
  }
}

async function cancelDraft() {
  await ElMessageBox.confirm('确认取消这份草稿？取消后不能继续编辑。', '取消草稿')
  saving.value = true
  try {
    emit('updated', await cancelAiDocumentDraft(props.draft.id, props.draft.concurrencyToken))
    editing.value = false
  } finally {
    saving.value = false
  }
}

async function executeDraft() {
  await ElMessageBox.confirm(
    '将按当前预览创建一张正式 Demo 业务单据，创建后状态为草稿。确认继续？',
    '创建正式单据',
    { confirmButtonText: '确认并验证', cancelButtonText: '返回检查', type: 'warning' },
  )
  const stepUpTicket = await sensitiveVerificationRef.value?.open('ai:document:execute')
  if (!stepUpTicket) return

  executing.value = true
  try {
    const confirmation = await confirmAiDocumentDraft(
      props.draft.id,
      props.draft.concurrencyToken,
      stepUpTicket,
    )
    const result = await executeAiDocumentDraft(
      props.draft.id,
      props.draft.concurrencyToken,
      confirmation,
    )
    executionResult.value = result
    emit('updated', {
      ...props.draft,
      status: result.draftStatus,
      concurrencyToken: result.draftConcurrencyToken,
      execution: result,
    })
    ElMessage.success(`正式单据 ${result.businessNo} 已创建`)
  } finally {
    executing.value = false
  }
}

function useCandidate(code: string) {
  form.departmentReference = code
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

function businessStatusText(value: string) {
  return value === 'Draft' ? '草稿' : value
}
</script>

<template>
  <article class="draft-card">
    <header class="draft-card__header">
      <div>
        <strong>Demo 业务单据草稿</strong>
        <span>版本 {{ draft.draftVersion }}</span>
      </div>
      <el-tag :type="status.type" effect="plain">{{ status.text }}</el-tag>
    </header>

    <el-form v-if="editing" ref="formRef" :model="form" :rules="rules" label-position="top">
      <div class="draft-card__form-grid">
        <el-form-item label="标题" prop="title">
          <el-input v-model="form.title" maxlength="200" />
        </el-form-item>
        <el-form-item label="客户" prop="customerName">
          <el-input v-model="form.customerName" maxlength="200" />
        </el-form-item>
        <el-form-item label="金额" prop="amount">
          <el-input-number v-model="form.amount" :min="0" :precision="2" class="full-width" />
        </el-form-item>
        <el-form-item label="部门编码或名称">
          <el-input v-model="form.departmentReference" maxlength="200" clearable />
        </el-form-item>
      </div>
      <div
        v-for="error in draft.validationErrors"
        :key="`${error.field}-${error.code}`"
        class="draft-card__error"
      >
        <span>{{ error.message }}</span>
        <el-select
          v-if="error.candidates.length"
          placeholder="选择明确部门"
          size="small"
          @change="useCandidate"
        >
          <el-option
            v-for="candidate in error.candidates"
            :key="candidate.id"
            :label="`${candidate.code} · ${candidate.name}`"
            :value="candidate.code"
          />
        </el-select>
      </div>
    </el-form>

    <dl v-else class="draft-card__preview">
      <div>
        <dt>标题</dt>
        <dd>{{ draft.payload.title || '待补充' }}</dd>
      </div>
      <div>
        <dt>客户</dt>
        <dd>{{ draft.payload.customerName || '待补充' }}</dd>
      </div>
      <div>
        <dt>金额</dt>
        <dd>{{ draft.payload.amount ?? '待补充' }}</dd>
      </div>
      <div>
        <dt>部门</dt>
        <dd>{{ draft.payload.departmentName || draft.payload.departmentReference || '未指定' }}</dd>
      </div>
    </dl>

    <div v-if="!editing && draft.validationErrors.length" class="draft-card__errors">
      <span v-for="error in draft.validationErrors" :key="`${error.field}-${error.code}`">{{
        error.message
      }}</span>
    </div>

    <div v-if="executionResult" class="draft-card__execution-result">
      <div>
        <span>正式单号</span>
        <strong>{{ executionResult.businessNo }}</strong>
      </div>
      <el-tag type="success" effect="plain">
        {{ businessStatusText(executionResult.businessStatus) }}
      </el-tag>
      <el-button tag="a" :href="executionResult.linkUrl" type="primary" text :icon="Link">
        查看单据
      </el-button>
    </div>

    <footer class="draft-card__footer">
      <span>有效期至 {{ formatTime(draft.expiresAt) }} · {{ draft.payloadHash.slice(0, 12) }}</span>
      <div v-if="editable">
        <template v-if="editing">
          <el-tooltip content="放弃本次修改" placement="top">
            <el-button :icon="Close" :disabled="saving" @click="editing = false" />
          </el-tooltip>
          <el-tooltip content="保存并重新校验" placement="top">
            <el-button type="primary" :icon="Check" :loading="saving" @click="save" />
          </el-tooltip>
        </template>
        <template v-else>
          <el-tooltip content="编辑草稿" placement="top">
            <el-button :icon="EditPen" @click="editing = true" />
          </el-tooltip>
          <el-tooltip content="取消草稿" placement="top">
            <el-button :icon="Close" :loading="saving" @click="cancelDraft" />
          </el-tooltip>
          <el-tooltip v-if="executable" content="确认并创建正式单据" placement="top">
            <el-button type="primary" :icon="Stamp" :loading="executing" @click="executeDraft">
              创建正式单据
            </el-button>
          </el-tooltip>
        </template>
      </div>
    </footer>
    <SensitiveVerificationDialog ref="sensitiveVerificationRef" />
  </article>
</template>

<style scoped>
.draft-card {
  width: min(100%, 720px);
  margin: 4px 0 18px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-bg-color);
}

.draft-card__header,
.draft-card__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 11px 13px;
}

.draft-card__execution-result {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 14px;
  border-top: 1px solid var(--el-border-color-lighter);
  background: var(--el-color-success-light-9);
}

.draft-card__execution-result > div {
  display: grid;
  gap: 2px;
  min-width: 0;
  margin-right: auto;
}

.draft-card__execution-result span {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.draft-card__header {
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.draft-card__header > div {
  display: flex;
  align-items: baseline;
  gap: 8px;
}

.draft-card__header span,
.draft-card__footer > span {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.draft-card__preview {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px 20px;
  margin: 0;
  padding: 14px;
}

.draft-card__preview div {
  min-width: 0;
}

.draft-card__preview dt {
  margin-bottom: 4px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.draft-card__preview dd {
  margin: 0;
  overflow-wrap: anywhere;
}

.draft-card :deep(.el-form) {
  padding: 14px 14px 0;
}

.draft-card__form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 14px;
}

.draft-card__error,
.draft-card__errors {
  display: grid;
  gap: 6px;
  color: var(--el-color-danger);
  font-size: 12px;
}

.draft-card__error {
  margin-bottom: 10px;
}

.draft-card__errors {
  padding: 0 14px 12px;
}

.draft-card__footer {
  min-height: 52px;
  border-top: 1px solid var(--el-border-color-lighter);
}

.draft-card__footer > div {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.full-width {
  width: 100%;
}

@media (max-width: 640px) {
  .draft-card__form-grid,
  .draft-card__preview {
    grid-template-columns: 1fr;
  }

  .draft-card__footer {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
