<script setup lang="ts">
import { computed, onActivated, onDeactivated, onUnmounted, ref, watch } from 'vue'
import { useAuthStore } from '../../../stores/auth'
import {
  getAiTechnicalExportReceipt,
  getAiTechnicalExportReceipts,
  type AiTechnicalExportReceipt,
  type AiTechnicalExportReceiptDetail,
  type AiTechnicalExportReceiptPage,
  type AiTechnicalExportReceiptSummary,
} from '../../../api/ai'
import {
  hashTechnicalExportFile,
  maxTechnicalExportBytes,
  supportsLocalExportIntegrity,
} from '../../../utils/aiTechnicalExportIntegrity'

const props = defineProps<{ modelValue: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()
const auth = useAuthStore()
const allowed = computed(
  () => auth.hasPermission('ai:operations:view') && auth.hasPermission('ai:operations:export'),
)
const identity = computed(() =>
  JSON.stringify([
    auth.effectiveTenantId,
    auth.currentUser?.userId,
    auth.currentUser?.permissionCodes,
    allowed.value,
  ]),
)
const range = ref<[Date, Date] | null>([new Date(Date.now() - 30 * 86400000), new Date()])
const pageIndex = ref(1)
const pageSize = ref(20)
const page = ref<AiTechnicalExportReceiptPage>()
const detail = ref<AiTechnicalExportReceiptDetail>()
const loading = ref(false)
const detailLoading = ref(false)
const hashing = ref(false)
const error = ref('')
const result = ref<'match' | 'mismatch' | ''>('')
const fileInput = ref<HTMLInputElement>()
const chosenFile = ref<File>()
let generation = 0
let controller: AbortController | undefined
let active = true
const prepared = computed(() => {
  if (!detail.value?.windowInterpretable || !detail.value.export.canVerify) return undefined
  const entries = detail.value.receipts.filter((entry) => entry.outcome === 'Prepared')
  return entries.length === 1 ? entries[0] : undefined
})
const cryptoReady = supportsLocalExportIntegrity()

function invalidate() {
  generation++
  controller?.abort()
  controller = undefined
  loading.value = detailLoading.value = hashing.value = false
  result.value = ''
  chosenFile.value = undefined
  if (fileInput.value) fileInput.value.value = ''
}
function clear() {
  invalidate()
  page.value = undefined
  detail.value = undefined
  error.value = ''
}
function current(id: number, key: string) {
  return id === generation && key === identity.value && active && allowed.value && props.modelValue
}
function validRange() {
  return (
    range.value &&
    Number.isFinite(range.value[0]?.getTime()) &&
    Number.isFinite(range.value[1]?.getTime()) &&
    range.value[0] < range.value[1] &&
    range.value[1].getTime() - range.value[0].getTime() <= 90 * 86400000 &&
    range.value[1].getTime() <= Date.now() + 5 * 60000
  )
}
function correctScope(value: AiTechnicalExportReceiptPage | AiTechnicalExportReceiptDetail) {
  return (
    value.scope === 'CurrentCaller' &&
    value.tenantId.toLowerCase() === auth.effectiveTenantId?.toLowerCase()
  )
}
async function load() {
  clear()
  if (!active || !allowed.value || !props.modelValue) return
  if (!validRange() || !range.value) {
    error.value = '请选择有效的凭据记录时间，最多 90 天。'
    return
  }
  const id = generation
  const key = identity.value
  const request = new AbortController()
  controller = request
  loading.value = true
  try {
    const response = await getAiTechnicalExportReceipts(
      {
        from: range.value[0].toISOString(),
        to: range.value[1].toISOString(),
        pageIndex: pageIndex.value,
        pageSize: pageSize.value,
      },
      request.signal,
    )
    if (!current(id, key)) return
    if (!correctScope(response)) throw new Error('scope')
    page.value = response
  } catch {
    if (current(id, key)) error.value = '凭据读取失败，请核对权限或缩短凭据记录时间后重试。'
  } finally {
    if (current(id, key)) {
      loading.value = false
      controller = undefined
    }
  }
}
async function selectExport(row: AiTechnicalExportReceiptSummary) {
  if (!page.value || !active || !allowed.value || !props.modelValue) return
  invalidate()
  detail.value = undefined
  error.value = ''
  const id = generation
  const key = identity.value
  const window = { from: page.value.receiptFrom, to: page.value.receiptTo }
  const request = new AbortController()
  controller = request
  detailLoading.value = true
  try {
    const response = await getAiTechnicalExportReceipt(row.exportId, window, request.signal)
    if (!current(id, key)) return
    if (
      !correctScope(response) ||
      response.export.exportId !== row.exportId ||
      response.receiptFrom !== window.from ||
      response.receiptTo !== window.to
    )
      throw new Error('scope')
    detail.value = response
  } catch {
    if (current(id, key)) error.value = '凭据详情不可用，请重新查询或核对当前权限。'
  } finally {
    if (current(id, key)) {
      detailLoading.value = false
      controller = undefined
    }
  }
}
function onFileChange(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  invalidate()
  error.value = ''
  if (!file) return
  if (file.size <= 0 || file.size > maxTechnicalExportBytes) {
    error.value = '请选择非空且不超过 16 MiB 的文件。'
    return
  }
  chosenFile.value = file
}
function fingerprint(entry: AiTechnicalExportReceipt, value: AiTechnicalExportReceiptDetail) {
  return JSON.stringify([
    value.tenantId,
    value.export,
    value.receiptFrom,
    value.receiptTo,
    value.receipts,
    entry,
  ])
}
async function verify() {
  const original = detail.value
  const receipt = prepared.value
  const file = chosenFile.value
  if (
    !original ||
    !receipt ||
    !file ||
    !cryptoReady ||
    hashing.value ||
    !active ||
    !allowed.value ||
    !props.modelValue
  )
    return
  const signature = fingerprint(receipt, original)
  const id = generation
  const key = identity.value
  result.value = ''
  error.value = ''
  hashing.value = true
  const request = new AbortController()
  controller = request
  try {
    const hash = await hashTechnicalExportFile(file)
    if (!current(id, key)) return
    const response = await getAiTechnicalExportReceipt(
      original.export.exportId,
      { from: original.receiptFrom, to: original.receiptTo },
      request.signal,
    )
    if (!current(id, key)) return
    const entries = response.receipts.filter((entry) => entry.outcome === 'Prepared')
    if (
      !correctScope(response) ||
      !response.windowInterpretable ||
      !response.export.canVerify ||
      entries.length !== 1 ||
      signature !== fingerprint(entries[0]!, response)
    )
      throw new Error('changed')
    detail.value = response
    result.value =
      hash.bytes === entries[0]!.bytes && hash.sha256 === entries[0]!.fileSha256
        ? 'match'
        : 'mismatch'
  } catch {
    if (current(id, key)) {
      invalidate()
      detail.value = undefined
      error.value = '文件核对失败或凭据已变化，请重新选择凭据与文件。'
    }
  } finally {
    if (current(id, key)) {
      hashing.value = false
      controller = undefined
    }
  }
}
function close() {
  clear()
  emit('update:modelValue', false)
}
function reason(code: string) {
  const labels: Record<string, string> = {
    WindowUnreadable: '窗口含损坏或不支持的凭据，无法核对。',
    ConflictingReceipts: '凭据范围存在冲突，无法核对。',
    MissingPrepared: '窗口内没有有效准备凭据。',
    MultiplePrepared: '窗口内有多条准备凭据，无法选定核对。',
    Ready: '可核对本地文件。',
  }
  return labels[code] ?? '凭据不可核对。'
}
function stage(value: string) {
  return (
    (
      { Requested: '已记录请求', Prepared: '已准备文件', Failed: '已记录失败' } as Record<
        string,
        string
      >
    )[value] ?? '未知阶段'
  )
}
function formatTime(value: string) {
  return new Date(value).toLocaleString('zh-CN')
}
watch(
  () => props.modelValue,
  (open) => {
    clear()
    if (open) void load()
  },
  { immediate: true },
)
watch(identity, close, { flush: 'sync' })
watch(
  range,
  () => {
    pageIndex.value = 1
    void load()
  },
  { deep: true, flush: 'sync' },
)
onActivated(() => {
  active = true
})
onDeactivated(() => {
  active = false
  close()
})
onUnmounted(() => {
  active = false
  clear()
})
</script>

<template>
  <el-dialog
    :model-value="modelValue && allowed"
    title="我的导出凭据"
    width="min(960px, 94vw)"
    :close-on-click-modal="false"
    @update:model-value="close"
  >
    <p>仅当前活动租户内本人凭据；只展示所选时间窗口内仍可读的记录，非完整历史。</p>
    <el-form inline @submit.prevent="load">
      <el-form-item label="凭据记录时间">
        <el-date-picker
          v-model="range"
          type="datetimerange"
          start-placeholder="开始时间"
          end-placeholder="结束时间"
          :disabled="!allowed"
        />
      </el-form-item>
      <el-form-item
        ><el-button :loading="loading" :disabled="!allowed" @click="load"
          >查询凭据</el-button
        ></el-form-item
      >
    </el-form>
    <p class="note">
      含开始、不含结束，最多 90 天；每次最多读取 1,000 条凭据。此范围与运行创建时间独立。
    </p>
    <el-alert v-if="error" :title="error" type="error" :closable="false" role="alert" />
    <template v-if="page">
      <p>
        匹配凭据 {{ page.matchedRecordCount }} 条；可解析导出
        {{ page.exports.totalCount }} 组；不可解析 {{ page.unreadableRecordCount }} 条。
      </p>
      <el-alert
        v-if="!page.windowInterpretable"
        title="窗口包含损坏或不支持的凭据，核对已禁用；请缩短窗口或通过原审计流程复核。"
        type="warning"
        :closable="false"
      />
      <el-table
        :data="page.exports.items"
        row-key="exportId"
        max-height="320"
        empty-text="本窗口没有可解析的导出凭据，不能据此推定从未导出。"
      >
        <el-table-column prop="exportId" label="导出编号" min-width="250" />
        <el-table-column label="窗口内事实" min-width="240"
          ><template #default="{ row }"
            >请求 {{ row.requestedCount }}／准备 {{ row.preparedCount }}／失败
            {{ row.failedCount }}</template
          ></el-table-column
        >
        <el-table-column label="核对" min-width="180"
          ><template #default="{ row }">{{
            reason(row.verificationReason)
          }}</template></el-table-column
        >
        <el-table-column label="操作" width="100"
          ><template #default="{ row }"
            ><el-button link type="primary" @click="selectExport(row)"
              >查看凭据</el-button
            ></template
          ></el-table-column
        >
      </el-table>
      <el-pagination
        v-model:current-page="pageIndex"
        :page-size="pageSize"
        :total="page.exports.totalCount"
        layout="prev, pager, next"
        @current-change="load"
      />
    </template>
    <p v-if="detailLoading" role="status">正在读取凭据详情…</p>
    <section v-if="detail" class="receipt-detail" aria-label="导出凭据详情">
      <h3>导出 {{ detail.export.exportId }}</h3>
      <p>{{ reason(detail.export.verificationReason) }}</p>
      <el-table :data="detail.receipts" row-key="receiptId" max-height="320">
        <el-table-column label="阶段" min-width="110"
          ><template #default="{ row }">{{ stage(row.outcome) }}</template></el-table-column
        >
        <el-table-column label="记录时间" min-width="170"
          ><template #default="{ row }">{{ formatTime(row.recordedAt) }}</template></el-table-column
        >
        <el-table-column label="文件字节数" min-width="120"
          ><template #default="{ row }">{{ row.bytes ?? '—' }}</template></el-table-column
        >
        <el-table-column label="失败代码" min-width="150"
          ><template #default="{ row }">{{ row.failureCode ?? '—' }}</template></el-table-column
        >
      </el-table>
      <template v-if="prepared">
        <p>
          运行范围：{{ formatTime(prepared.from) }} 至 {{ formatTime(prepared.to) }}；运行
          {{ prepared.runCount }}／用量 {{ prepared.usageCount }}。
        </p>
        <p class="hash">文件 SHA-256：{{ prepared.fileSha256 }}</p>
        <el-alert
          v-if="!cryptoReady"
          title="当前环境无法进行本地核对，请使用 HTTPS 安全环境。"
          type="info"
          :closable="false"
        />
        <label class="file-label"
          >选择本地文件（不上传，最多 16 MiB）<input
            ref="fileInput"
            type="file"
            accept=".json"
            :disabled="!cryptoReady"
            @change="onFileChange"
        /></label>
        <p v-if="chosenFile">已选择 {{ chosenFile.size }} 字节。</p>
        <el-button
          :loading="hashing"
          :disabled="!chosenFile || !cryptoReady || hashing"
          @click="verify"
          >核对本地文件</el-button
        >
        <el-alert
          v-if="result"
          :title="result === 'match' ? '与当前可见准备凭据一致。' : '所选文件与准备凭据不一致。'"
          :type="result === 'match' ? 'success' : 'warning'"
          :closable="false"
          role="status"
        />
      </template>
    </section>
    <p class="note">
      Prepared 只证明文件准备，不能证明最终响应获准或浏览器收到。SHA-256
      不是签名或法律认证，文件匹配不能覆盖失败事实。
    </p>
    <template #footer><el-button @click="close">关闭</el-button></template>
  </el-dialog>
</template>

<style scoped lang="scss">
.note {
  color: var(--el-text-color-secondary);
  line-height: 1.6;
}
.receipt-detail {
  margin-top: 24px;
  min-width: 0;
}
.receipt-detail h3,
.hash {
  overflow-wrap: anywhere;
}
.file-label {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
  margin: 16px 0;
}
.file-label input {
  max-width: 100%;
}
.el-alert,
.el-pagination {
  margin-top: 12px;
}
.el-form-item {
  max-width: 100%;
}
:deep(.el-form-item__content) {
  min-width: 0;
}
:deep(.el-date-editor) {
  max-width: 100%;
}
@media (max-width: 600px) {
  .el-form-item {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    margin-right: 0;
  }
}
</style>
