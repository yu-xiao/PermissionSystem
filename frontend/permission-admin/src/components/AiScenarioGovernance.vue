<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type UploadFile } from 'element-plus'
import { useAuthStore } from '../stores/auth'
import {
  listAiScenarios,
  getAiScenarioDetail,
  saveAiScenario,
  freezeAiScenario,
  copyAiScenarioVersion,
  exportAiScenarioSnapshot,
  importAiScenarioEvaluation,
  getAiScenarioEvaluationReport,
  reviewAiScenarioEvaluation,
  changeAiScenarioVersion,
  revokeAiScenarioEvaluation,
  type AiScenario,
  type AiScenarioDetail,
  type AiScenarioVersion,
  type AiScenarioEvaluation,
  type AiScenarioConfiguration,
  type AiEvaluationReport,
  type AiCaseReview,
} from '../api/aiScenario'

const auth = useAuthStore()
const canManage = computed(() => auth.hasPermission('ai:governance:manage'))
const busy = ref(false)
const scenarios = ref<AiScenario[]>([])
const detail = ref<AiScenarioDetail>()
const editing = ref(false)
const importVersion = ref<AiScenarioVersion>()
const reportFile = ref('')
const reviewing = ref<AiScenarioEvaluation>()
const reviewReport = ref<AiEvaluationReport>()
const reviewCases = ref<AiCaseReview[]>([])
const goldenApproved = ref(false)
const reviewReason = ref('')
const tools = [
  { code: 'permission.diagnose', name: '权限排障' },
  { code: 'permission.users.search', name: '用户查找' },
  { code: 'permission.departments.search', name: '部门查找' },
  { code: 'permission.roles.summary', name: '角色摘要' },
]
const defaults = (): AiScenarioConfiguration => ({
  supplementPrompt: '',
  toolCodes: tools.map((t) => t.code),
  maxModelRounds: 6,
  maxToolCalls: 10,
  maxHistoryMessages: 20,
  maxRunSeconds: 90,
  temperature: 0,
  maxTokens: 2048,
})
const form = reactive({ name: '权限助手', description: '', configuration: defaults() })
const limits = [
  { key: 'maxModelRounds', label: '模型轮数', max: 6 },
  { key: 'maxToolCalls', label: '工具调用次数', max: 10 },
  { key: 'maxHistoryMessages', label: '历史用户消息数', max: 20 },
  { key: 'maxRunSeconds', label: '运行秒数', max: 90 },
  { key: 'maxTokens', label: '最大输出 Token', max: 128000 },
] as const
const lifecycle = ['人工审核', '发布', '停用', '回退', '撤销评测资格']

async function refresh(id = detail.value?.scenario.id) {
  scenarios.value = await listAiScenarios()
  const selected = id ?? scenarios.value[0]?.id
  detail.value = selected ? await getAiScenarioDetail(selected) : undefined
}
async function perform(action: () => Promise<unknown>) {
  if (!canManage.value || busy.value) return
  busy.value = true
  try {
    await action()
    await refresh()
    ElMessage.success('操作已保存')
  } finally {
    busy.value = false
  }
}
function openEditor() {
  if (!canManage.value) return
  const current = detail.value?.scenario
  Object.assign(form, {
    name: current?.name ?? '权限助手',
    description: current?.description ?? '',
    configuration: current ? JSON.parse(JSON.stringify(current.configuration)) : defaults(),
  })
  editing.value = true
}
async function save() {
  if (!form.name.trim() || !form.configuration.toolCodes.length) {
    ElMessage.warning('请填写名称并选择工具')
    return
  }
  await perform(async () => {
    const saved = await saveAiScenario({
      code: 'permission-assistant',
      ...form,
      concurrencyToken: detail.value?.scenario.concurrencyToken,
    })
    detail.value = await getAiScenarioDetail(saved.id)
    editing.value = false
  })
}
const token = () => detail.value!.scenario.concurrencyToken
async function freeze() {
  if (!detail.value) return
  await perform(() =>
    freezeAiScenario(detail.value!.scenario.id, {
      concurrencyToken: token(),
      reason: '冻结当前草稿',
    }),
  )
}
async function copy(version: AiScenarioVersion) {
  await ElMessageBox.confirm(
    '此操作将以所选版本配置替换当前草稿，已冻结版本保持不变。',
    '复制为草稿',
  )
  await perform(() =>
    copyAiScenarioVersion(version.id, {
      concurrencyToken: token(),
      reason: '复制历史配置为新草稿',
    }),
  )
}
async function exportSnapshot(version: AiScenarioVersion) {
  const snapshot = await exportAiScenarioSnapshot(version.id)
  const url = URL.createObjectURL(
    new Blob([JSON.stringify(snapshot)], { type: 'application/json' }),
  )
  const link = document.createElement('a')
  link.href = url
  link.download = `permission-assistant-v${version.versionNumber}.json`
  link.click()
  URL.revokeObjectURL(url)
}
function openImport(version: AiScenarioVersion) {
  importVersion.value = version
  reportFile.value = ''
}
async function chooseFile(file: UploadFile) {
  reportFile.value = ''
  if (!file.raw || file.raw.size > 32 * 1024 * 1024) {
    ElMessage.warning('报告最大为 32 MiB')
    return
  }
  reportFile.value = await file.raw.text()
}
async function importReport() {
  if (!importVersion.value || !reportFile.value) return
  await perform(async () => {
    await importAiScenarioEvaluation(importVersion.value!.id, reportFile.value)
    importVersion.value = undefined
    reportFile.value = ''
  })
}
async function versionAction(version: AiScenarioVersion, action: 'publish' | 'rollback' | 'stop') {
  const { value } = await ElMessageBox.prompt(
    '请输入操作理由',
    lifecycle[action === 'publish' ? 1 : action === 'stop' ? 2 : 3],
    {
      inputValidator: (text) =>
        Boolean(text?.trim() && text.trim().length <= 1000) || '请输入 1～1000 字理由',
    },
  )
  let confirmInitialBaseline = false
  if (
    action === 'publish' &&
    !detail.value?.scenario.currentVersionId &&
    !detail.value?.versions.some((v) => v.published)
  ) {
    await ElMessageBox.confirm(
      '确认将此版本作为首个基线？离线、真实模型及逐案例人工审核仍必须通过。',
      '首版基线确认',
    )
    confirmInitialBaseline = true
  }
  await perform(() =>
    changeAiScenarioVersion(version.id, action, {
      concurrencyToken: token(),
      reason: value,
      confirmInitialBaseline,
    }),
  )
}
async function openReview(evaluation: AiScenarioEvaluation) {
  const report = await getAiScenarioEvaluationReport(evaluation.id)
  reviewReport.value = report
  reviewCases.value = report.results
    .filter((r) => r.status !== 'NotApplicable')
    .map((r) => ({ key: r.key, caseHash: r.caseHash, passed: false, notes: '' }))
  goldenApproved.value = false
  reviewReason.value = ''
  reviewing.value = evaluation
}
async function submitReview() {
  if (!reviewReason.value.trim() || reviewCases.value.some((c) => !c.notes.trim())) {
    ElMessage.warning('请填写审核理由和每个案例的事实依据')
    return
  }
  await perform(async () => {
    await reviewAiScenarioEvaluation(reviewing.value!.id, {
      concurrencyToken: token(),
      reportHash: reviewing.value!.reportHash,
      goldenCasesApproved: goldenApproved.value,
      cases: reviewCases.value,
      reason: reviewReason.value,
    })
    reviewing.value = undefined
  })
}
async function revoke(evaluation: AiScenarioEvaluation) {
  const { value } = await ElMessageBox.prompt(
    '请输入撤销理由。关联版本可能因此无法继续运行。',
    '撤销资格',
    {
      inputValidator: (text) =>
        Boolean(text?.trim() && text.trim().length <= 1000) || '请输入 1～1000 字理由',
    },
  )
  await perform(() =>
    revokeAiScenarioEvaluation(evaluation.id, { concurrencyToken: token(), reason: value }),
  )
}
onMounted(() => refresh())
</script>

<template>
  <div v-loading="busy" class="scenario-governance">
    <div class="scenario-toolbar">
      <el-select
        v-if="scenarios.length"
        :model-value="detail?.scenario.id"
        aria-label="治理场景"
        @change="refresh"
      >
        <el-option
          v-for="scenario in scenarios"
          :key="scenario.id"
          :label="scenario.name"
          :value="scenario.id"
        />
      </el-select>
      <el-button @click="refresh()">刷新</el-button>
      <el-button v-if="canManage" type="primary" @click="openEditor">{{
        detail ? '编辑草稿' : '新增权限助手'
      }}</el-button>
      <el-button v-if="canManage && detail" @click="freeze">冻结候选版本</el-button>
    </div>
    <el-alert
      title="版本通过离线、真实模型和人工审核后才能发布。已有会话固定版本，升级会新建会话。"
      type="info"
      :closable="false"
    />
    <el-empty v-if="!detail" description="暂无受控场景" />
    <template v-else>
      <p>
        {{ detail.scenario.description }} · 当前版本：{{
          detail.versions.find((v) => v.id === detail?.scenario.currentVersionId)?.versionNumber ??
          '未发布'
        }}
      </p>
      <el-table :data="detail.versions" row-key="id">
        <el-table-column prop="versionNumber" label="版本" width="65" />
        <el-table-column prop="contentHash" label="内容摘要" show-overflow-tooltip />
        <el-table-column label="状态" width="170"
          ><template #default="{ row }">
            <el-tag>{{ row.stopped ? '已停用' : row.published ? '已发布' : '候选' }}</el-tag>
            <el-tag v-if="!row.compatible" type="danger">代码不兼容</el-tag>
          </template></el-table-column
        >
        <el-table-column label="操作" min-width="380"
          ><template #default="{ row }">
            <el-button link :disabled="!row.compatible" @click="exportSnapshot(row)"
              >导出快照</el-button
            >
            <template v-if="canManage">
              <el-button link @click="copy(row)">复制为草稿</el-button>
              <el-button link :disabled="!row.compatible" @click="openImport(row)"
                >导入评测</el-button
              >
              <el-button
                link
                :disabled="!row.compatible || row.stopped"
                @click="versionAction(row, row.published ? 'rollback' : 'publish')"
                >{{ row.published ? '回退至此版本' : '发布' }}</el-button
              >
              <el-button
                v-if="row.published && !row.stopped"
                link
                type="danger"
                @click="versionAction(row, 'stop')"
                >停用</el-button
              >
            </template>
          </template></el-table-column
        >
      </el-table>
      <h4>评测证据</h4>
      <el-table :data="detail.evaluations" row-key="id">
        <el-table-column prop="mode" label="模式" width="90" />
        <el-table-column prop="reportHash" label="报告摘要" show-overflow-tooltip />
        <el-table-column prop="modelFingerprint" label="模型配置摘要" show-overflow-tooltip />
        <el-table-column label="自动检查" width="100"
          ><template #default="{ row }">{{
            row.automaticPassed ? '通过' : '失败／不完整'
          }}</template></el-table-column
        >
        <el-table-column label="人工审核" width="100"
          ><template #default="{ row }">{{
            row.reviewJson ? '已记录' : '待审核'
          }}</template></el-table-column
        >
        <el-table-column label="操作" width="180"
          ><template #default="{ row }">
            <el-button link @click="openReview(row)">{{
              canManage ? '逐案例审核' : '查看报告'
            }}</el-button>
            <el-button v-if="canManage" link type="danger" @click="revoke(row)">撤销资格</el-button>
          </template></el-table-column
        >
      </el-table>
      <h4>发布与审核记录</h4>
      <el-table :data="detail.events" row-key="id">
        <el-table-column label="类型" width="120"
          ><template #default="{ row }">{{ lifecycle[row.type] }}</template></el-table-column
        >
        <el-table-column prop="actorUserId" label="操作者" show-overflow-tooltip />
        <el-table-column prop="reason" label="理由" show-overflow-tooltip />
        <el-table-column prop="createdAt" label="时间" />
      </el-table>
    </template>
    <el-dialog v-model="editing" title="权限助手草稿" width="720px">
      <el-form label-width="140px">
        <el-form-item label="名称"><el-input v-model="form.name" maxlength="100" /></el-form-item>
        <el-form-item label="说明"
          ><el-input v-model="form.description" maxlength="1000"
        /></el-form-item>
        <el-form-item label="场景补充指令"
          ><el-input
            v-model="form.configuration.supplementPrompt"
            type="textarea"
            :rows="5"
            maxlength="8000"
            show-word-limit
        /></el-form-item>
        <el-form-item label="工具"
          ><el-checkbox-group v-model="form.configuration.toolCodes"
            ><el-checkbox v-for="tool in tools" :key="tool.code" :value="tool.code">{{
              tool.name
            }}</el-checkbox></el-checkbox-group
          ></el-form-item
        >
        <el-form-item v-for="limit in limits" :key="limit.key" :label="limit.label"
          ><el-input-number v-model="form.configuration[limit.key]" :min="1" :max="limit.max"
        /></el-form-item>
        <el-form-item label="Temperature"
          ><el-input-number v-model="form.configuration.temperature" :min="0" :max="2" :step="0.1"
        /></el-form-item>
      </el-form>
      <p>补充指令和工具选择不授予权限，也不能关闭服务端校验；请勿填入敏感凭据。</p>
      <template #footer
        ><el-button @click="editing = false">取消</el-button
        ><el-button type="primary" :loading="busy" @click="save">保存草稿</el-button></template
      >
    </el-dialog>
    <el-dialog
      :model-value="Boolean(importVersion)"
      title="导入候选版本评测报告"
      width="620px"
      @close="importVersion = undefined"
    >
      <p>请选择使用此快照运行的脱敏 report.json。导入不会自动批准或发布。</p>
      <el-upload
        :auto-upload="false"
        accept=".json"
        :limit="1"
        :on-change="chooseFile"
        :on-remove="() => (reportFile = '')"
        ><el-button>选择报告文件</el-button></el-upload
      >
      <template #footer
        ><el-button type="primary" :disabled="!reportFile" :loading="busy" @click="importReport"
          >导入</el-button
        ></template
      >
    </el-dialog>
    <el-dialog
      :model-value="Boolean(reviewing)"
      :title="canManage ? '逐案例事实审核' : '评测报告'"
      width="900px"
      @close="reviewing = undefined"
    >
      <el-alert
        title="通过自动检查不等于事实已人工核验。每个案例须独立判定，审核人由服务器记录。"
        type="info"
        :closable="false"
      />
      <div v-for="result in reviewReport?.results" :key="result.key" class="scenario-case">
        <strong
          >{{ result.key }} · {{ result.status
          }}{{ result.safetyCritical ? ' · 安全案例' : '' }}</strong
        >
        <div v-for="(step, index) in result.steps" :key="index">
          <p>{{ step.input }}</p>
          <pre>{{ step.output }}</pre>
          <details>
            <summary>服务端证据与实际工具</summary>
            <pre>{{ JSON.stringify({ evidence: step.evidence, tools: step.tools }, null, 2) }}</pre>
          </details>
          <p>
            检查：{{
              step.checks.map((c) => `${c.code}: ${c.passed ? '通过' : '失败'}`).join('；')
            }}
          </p>
        </div>
        <template
          v-for="review in reviewCases.filter((c) => c.key === result.key)"
          :key="review.key"
        >
          <el-checkbox v-model="review.passed" :disabled="!canManage">事实审核通过</el-checkbox>
          <el-input
            v-model="review.notes"
            :disabled="!canManage"
            placeholder="事实依据、限制或拒绝理由"
            maxlength="2000"
          />
        </template>
      </div>
      <template v-if="canManage">
        <el-checkbox v-model="goldenApproved">确认本报告关联的黄金案例预期已经人工审核</el-checkbox>
        <el-input v-model="reviewReason" placeholder="本次审核理由" maxlength="1000" />
      </template>
      <template #footer
        ><el-button v-if="canManage" type="primary" :loading="busy" @click="submitReview"
          >保存人工审核</el-button
        ></template
      >
    </el-dialog>
  </div>
</template>

<style scoped>
.scenario-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-bottom: 16px;
}
.scenario-case {
  padding: 16px 0;
  border-bottom: 1px solid var(--el-border-color);
}
pre {
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}
</style>
