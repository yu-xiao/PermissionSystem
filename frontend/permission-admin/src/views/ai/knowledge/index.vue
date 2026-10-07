<script setup lang="ts">
defineOptions({ name: 'AiKnowledge' })
import { computed, onActivated, onDeactivated, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox, type UploadFile } from 'element-plus'
import PageContainer from '../../../components/PageContainer/index.vue'
import AiKnowledgeCitationCard from '../../../components/AiKnowledgeCitationCard.vue'
import { useAuthStore } from '../../../stores/auth'
import { getRoles, type RoleItem } from '../../../api/roles'
import {
  createKnowledgeDocument,
  deleteKnowledgeDocument,
  getKnowledgeDocuments,
  previewKnowledgeVersion,
  publishKnowledgeVersion,
  searchKnowledge,
  setKnowledgeAccess,
  uploadKnowledgeVersion,
  type AiKnowledgeDocument,
  type AiKnowledgeSearchResult,
} from '../../../api/aiKnowledge'

const auth = useAuthStore()
const canView = computed(() => auth.hasPermission('ai:knowledge:view'))
const canManage = computed(() => auth.hasPermission('ai:knowledge:manage'))
const canQuery = computed(() => auth.hasPermission('ai:knowledge:query'))
const canListRoles = computed(() => auth.hasPermission('system:role:view'))
const rows = ref<AiKnowledgeDocument[]>([])
const total = ref(0)
const page = ref(1)
const loading = ref(false)
const saving = ref(false)
const error = ref('')
const roles = ref<RoleItem[]>([])
const dialog = ref<'create' | 'access' | 'upload' | ''>('')
const selected = ref<AiKnowledgeDocument>()
const selectedFile = ref<File>()
const form = reactive({
  title: '',
  owner: '',
  license: '',
  roleIds: [] as string[],
  synthetic: false,
  validFrom: '',
  validUntil: '',
})
const keyword = ref('')
const searching = ref(false)
const searchResult = ref<AiKnowledgeSearchResult>()
const preview = ref<AiKnowledgeSearchResult>()
const reviewing = ref<{ documentId: string; versionId: string; sequence: number }>()
let generation = 0

function clearContent() {
  generation++
  searchResult.value = undefined
  preview.value = undefined
  reviewing.value = undefined
  loading.value = false
  searching.value = false
}
function closeDialog(done: () => void) {
  if (!saving.value) done()
}
async function load() {
  clearContent()
  const id = generation
  error.value = ''
  rows.value = []
  total.value = 0
  if (!canView.value) return
  loading.value = true
  try {
    const result = await getKnowledgeDocuments(page.value)
    if (id === generation) {
      rows.value = result.items
      total.value = result.totalCount
    }
  } catch {
    if (id === generation) error.value = '无法加载文档，请检查当前授权与知识库开关后重试。'
  } finally {
    if (id === generation) loading.value = false
  }
}
async function open(kind: 'create' | 'access' | 'upload', doc?: AiKnowledgeDocument) {
  selected.value = doc
  roles.value = []
  selectedFile.value = undefined
  Object.assign(form, {
    title: '',
    owner: '',
    license: '',
    synthetic: false,
    roleIds: [...(doc?.roleIds ?? [])],
    validFrom: '',
    validUntil: '',
  })
  dialog.value = kind
  if (kind === 'upload' || !canListRoles.value) return
  const tenant = auth.effectiveTenantId
  try {
    let index = 1
    const options: RoleItem[] = []
    let more: boolean
    do {
      const result = await getRoles({ pageIndex: index++, pageSize: 100, isEnabled: true })
      options.push(...result.items.filter((r) => r.tenantId === tenant && r.isEnabled))
      more = result.hasNextPage
    } while (more)
    if (
      tenant === auth.effectiveTenantId &&
      dialog.value === kind &&
      selected.value?.id === doc?.id
    )
      roles.value = options
  } catch {
    ElMessage.error('角色选项加载失败；可关闭后重试。')
  }
}
function chooseFile(file: UploadFile) {
  selectedFile.value = undefined
  if (
    !file.raw ||
    !/\.txt$/i.test(file.name) ||
    file.size === undefined ||
    file.size <= 0 ||
    file.size > 256 * 1024
  ) {
    ElMessage.warning('请选择不超过 256 KiB 的 UTF-8 .txt 合成资料。')
    return
  }
  selectedFile.value = file.raw
}
async function save() {
  if (
    dialog.value === 'create' &&
    (!form.title.trim() || !form.owner.trim() || !form.license.trim() || !form.synthetic)
  ) {
    ElMessage.warning('请填写标题、测试 Owner 和许可，并确认仅包含合成资料。')
    return
  }
  if (
    dialog.value === 'upload' &&
    (!selectedFile.value ||
      !form.validFrom ||
      !form.validUntil ||
      new Date(form.validUntil) <= new Date(form.validFrom) ||
      new Date(form.validUntil) <= new Date())
  ) {
    ElMessage.warning('请选择合成文件并填写尚未到期的有效时间范围。')
    return
  }
  saving.value = true
  try {
    if (dialog.value === 'create')
      await createKnowledgeDocument({
        title: form.title,
        owner: form.owner,
        license: form.license,
        synthetic: form.synthetic,
        roleIds: form.roleIds,
      })
    else if (dialog.value === 'access' && selected.value)
      await setKnowledgeAccess(selected.value, form.roleIds)
    else if (dialog.value === 'upload' && selected.value && selectedFile.value)
      await uploadKnowledgeVersion(
        selected.value,
        selectedFile.value,
        new Date(form.validFrom).toISOString(),
        new Date(form.validUntil).toISOString(),
      )
    dialog.value = ''
    ElMessage.success('已保存，请重新核验资料后发布。')
  } catch {
    ElMessage.error('保存失败，请刷新文档后重试。')
  } finally {
    saving.value = false
    await load()
  }
}
async function publish(doc: AiKnowledgeDocument, versionId: string) {
  try {
    await ElMessageBox.confirm(
      '请确认已审核本版本全部片段。发布后旧版本引用将失效。',
      '发布合成版本',
    )
  } catch {
    return
  }
  saving.value = true
  try {
    await publishKnowledgeVersion(doc, versionId)
    ElMessage.success('已发布')
  } catch {
    ElMessage.error('发布失败，请刷新并核对文档授权与版本状态。')
  } finally {
    saving.value = false
    await load()
  }
}
async function remove(doc: AiKnowledgeDocument) {
  try {
    await ElMessageBox.confirm(
      '删除将清除片段和相关知识回答正文，源文件进入删除补偿。',
      '删除合成文档',
      { type: 'warning' },
    )
  } catch {
    return
  }
  saving.value = true
  try {
    await deleteKnowledgeDocument(doc)
    ElMessage.success('已删除')
  } catch {
    ElMessage.error('删除失败，请刷新文档后重试。')
  } finally {
    saving.value = false
    await load()
  }
}
async function inspect(documentId: string, versionId: string, sequence = 1) {
  clearContent()
  const id = generation
  searching.value = true
  try {
    const result = await previewKnowledgeVersion(documentId, versionId, sequence)
    if (id === generation) {
      preview.value = result
      reviewing.value = { documentId, versionId, sequence }
    }
  } catch {
    if (id === generation) error.value = '来源已不可用，请刷新后重新核验。'
  } finally {
    if (id === generation) searching.value = false
  }
}
async function search() {
  clearContent()
  if (!keyword.value.trim()) return
  const id = generation
  error.value = ''
  searching.value = true
  try {
    const result = await searchKnowledge(keyword.value)
    if (id === generation) searchResult.value = result
  } catch {
    if (id === generation) error.value = '查询不可用，请核对当前权限和知识库开关。'
  } finally {
    if (id === generation) searching.value = false
  }
}
watch(
  () => auth.effectiveTenantId,
  () => {
    dialog.value = ''
    selected.value = undefined
    page.value = 1
    void load()
  },
)
watch(canQuery, (value) => {
  if (!value) clearContent()
})
onMounted(() => {
  void load()
})
let activatedOnce = false
onActivated(() => {
  if (activatedOnce) void load()
  activatedOnce = true
})
onDeactivated(() => {
  clearContent()
  rows.value = []
  dialog.value = ''
})
</script>

<template>
  <PageContainer title="文档知识库">
    <el-alert
      title="首批仅接收合成 UTF-8 文本；管理权限不自动获得正文读取权限。"
      type="info"
      :closable="false"
      show-icon
    />
    <p v-if="error" role="alert">{{ error }}</p>
    <div class="actions">
      <el-button :loading="loading" @click="load">刷新并重新核验</el-button>
      <el-button v-if="canManage" type="primary" :disabled="saving" @click="open('create')"
        >创建合成文档</el-button
      >
    </div>
    <el-table v-if="canView" v-loading="loading" :data="rows" row-key="id">
      <el-table-column prop="title" label="标题" min-width="180" />
      <el-table-column prop="owner" label="测试 Owner" min-width="140" />
      <el-table-column label="已发布版本" min-width="110">
        <template #default="{ row }">{{
          row.versions.find((v: { id: string }) => v.id === row.currentVersionId)?.versionNumber ??
          '未发布'
        }}</template>
      </el-table-column>
      <el-table-column label="操作" min-width="230">
        <template #default="{ row }">
          <el-button v-if="canManage" :disabled="saving" @click="open('upload', row)"
            >导入版本</el-button
          >
          <el-button
            v-if="canManage"
            :disabled="saving || !canListRoles"
            @click="open('access', row)"
            >角色授权</el-button
          >
          <el-button v-if="canManage" type="danger" :disabled="saving" @click="remove(row)"
            >删除</el-button
          >
        </template>
      </el-table-column>
      <el-table-column type="expand">
        <template #default="{ row }">
          <p>许可：{{ row.license }}；授权角色数：{{ row.roleIds.length }}（空授权拒绝正文读取）</p>
          <el-table :data="row.versions">
            <el-table-column prop="versionNumber" label="版本" width="80" />
            <el-table-column prop="parseStatus" label="解析状态" width="100" />
            <el-table-column prop="errorCode" label="失败码" min-width="160" />
            <el-table-column prop="validFrom" label="生效时间" min-width="200" />
            <el-table-column prop="validUntil" label="到期时间" min-width="200" />
            <el-table-column label="审核与发布" min-width="220">
              <template #default="scope">
                <el-button
                  v-if="canQuery"
                  :disabled="searching || scope.row.parseStatus !== 'Ready'"
                  @click="inspect(row.id, scope.row.id)"
                  >预览</el-button
                >
                <el-button
                  v-if="canManage && canQuery"
                  :disabled="
                    saving ||
                    scope.row.parseStatus !== 'Ready' ||
                    row.currentVersionId === scope.row.id
                  "
                  @click="publish(row, scope.row.id)"
                  >发布</el-button
                >
              </template>
            </el-table-column>
          </el-table>
        </template>
      </el-table-column>
    </el-table>
    <el-pagination
      v-if="canView"
      v-model:current-page="page"
      :page-size="20"
      :total="total"
      layout="prev, pager, next, total"
      @current-change="load"
    />
    <section v-if="canQuery" aria-label="受控知识查询" class="search">
      <el-form inline @submit.prevent="search">
        <el-form-item label="直接匹配关键词"
          ><el-input v-model="keyword" maxlength="100" clearable
        /></el-form-item>
        <el-button native-type="submit" :loading="searching" :disabled="!keyword.trim()"
          >查询</el-button
        >
      </el-form>
      <template v-if="searchResult">
        <p>{{ searchResult.limitation }}</p>
        <AiKnowledgeCitationCard :hits="searchResult.items" />
        <p v-if="searchResult.isTruncated">仅展示前 5 个可见片段，不代表匹配总量。</p>
      </template>
      <template v-if="preview && reviewing">
        <p>{{ preview.limitation }} 当前片段序号：{{ reviewing.sequence }} 起。</p>
        <article v-for="hit in preview.items" :key="hit.reference.chunkId">
          <strong
            >{{ hit.title }} · v{{ hit.versionNumber }} · 第 {{ hit.startLine }}–{{
              hit.endLine
            }}
            行</strong
          >
          <pre>{{ hit.content }}</pre>
        </article>
        <el-button
          :disabled="searching || reviewing.sequence === 1"
          @click="inspect(reviewing.documentId, reviewing.versionId, reviewing.sequence - 5)"
          >上一页</el-button
        >
        <el-button
          :disabled="searching || !preview.isTruncated"
          @click="inspect(reviewing.documentId, reviewing.versionId, reviewing.sequence + 5)"
          >下一页</el-button
        >
      </template>
    </section>
    <el-dialog
      :model-value="!!dialog"
      :title="
        dialog === 'create' ? '创建合成文档' : dialog === 'access' ? '文档角色授权' : '导入合成版本'
      "
      width="min(600px, 94vw)"
      :close-on-click-modal="false"
      :before-close="closeDialog"
      @close="dialog = ''"
    >
      <el-form label-position="top" @submit.prevent="save">
        <template v-if="dialog === 'create'">
          <el-form-item label="文档标题" required
            ><el-input v-model="form.title" maxlength="200"
          /></el-form-item>
          <el-form-item label="测试 Owner" required
            ><el-input v-model="form.owner" maxlength="200"
          /></el-form-item>
          <el-form-item label="测试许可说明" required
            ><el-input v-model="form.license" maxlength="500"
          /></el-form-item>
          <el-checkbox v-model="form.synthetic">确认仅包含合成资料</el-checkbox>
        </template>
        <template v-if="dialog !== 'upload'">
          <el-form-item label="明确允许读取正文的角色">
            <el-select
              v-model="form.roleIds"
              multiple
              filterable
              :disabled="!canListRoles"
              class="full-width"
            >
              <el-option v-for="role in roles" :key="role.id" :label="role.name" :value="role.id" />
            </el-select>
          </el-form-item>
          <p v-if="!canListRoles">
            角色枚举需要角色查看权限；可先创建空 ACL 草稿，再由具备权限者配置。
          </p>
        </template>
        <template v-else>
          <el-form-item label="合成 UTF-8 .txt 文件（最大 256 KiB）" required>
            <el-upload
              :auto-upload="false"
              :limit="1"
              accept=".txt"
              :on-change="chooseFile"
              :on-remove="() => (selectedFile = undefined)"
              ><el-button>选择文件</el-button></el-upload
            >
          </el-form-item>
          <el-form-item label="生效时间（本地时区）" required
            ><el-date-picker
              v-model="form.validFrom"
              type="datetime"
              value-format="YYYY-MM-DDTHH:mm:ss"
          /></el-form-item>
          <el-form-item label="到期时间（本地时区）" required
            ><el-date-picker
              v-model="form.validUntil"
              type="datetime"
              value-format="YYYY-MM-DDTHH:mm:ss"
          /></el-form-item>
        </template>
      </el-form>
      <template #footer
        ><el-button :disabled="saving" @click="dialog = ''">取消</el-button
        ><el-button type="primary" :loading="saving" @click="save">保存</el-button></template
      >
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.actions {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
  margin: 16px 0;
}
.search {
  margin-top: 24px;
}
.full-width {
  width: 100%;
}
pre {
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  font: inherit;
}
article {
  padding: 12px;
  border: 1px solid var(--el-border-color-light);
  margin-bottom: 12px;
}
</style>
