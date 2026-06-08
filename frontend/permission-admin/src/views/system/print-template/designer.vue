<script setup lang="ts">
defineOptions({
  name: 'SystemPrintTemplateDesigner',
})

import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  getPrintTemplate,
  previewPrintTemplate,
  renderPrintTemplate,
  updatePrintTemplate,
} from '../../../api/printTemplate'
import PageContainer from '../../../components/PageContainer/index.vue'

const route = useRoute()
const router = useRouter()
const templateId = computed(() => String(route.params.id ?? ''))
const loading = ref(false)
const saving = ref(false)
const previewing = ref(false)
const formRef = ref<FormInstance>()
const previewHtml = ref('')

const form = reactive({
  templateCode: '',
  templateName: '',
  businessType: '',
  templateType: 'Document',
  contentHtml: '',
  contentJson: '',
  paperSize: 'A4',
  orientation: 'Portrait',
  isDefault: false,
  isEnabled: true,
  version: 1,
  remark: '',
})

const rules: FormRules = {
  templateName: [{ required: true, message: '请输入模板名称', trigger: 'blur' }],
  businessType: [{ required: true, message: '请输入业务类型', trigger: 'blur' }],
  templateType: [{ required: true, message: '请输入模板类型', trigger: 'blur' }],
  contentHtml: [{ required: true, message: '请输入模板 HTML', trigger: 'blur' }],
  paperSize: [{ required: true, message: '请选择纸张', trigger: 'change' }],
  orientation: [{ required: true, message: '请选择方向', trigger: 'change' }],
}

const variables = [
  { label: '单号', value: '{{OrderNo}}' },
  { label: '创建时间', value: '{{CreatedAt}}' },
  { label: '申请人', value: '{{ApplicantName}}' },
  { label: '金额', value: '{{Amount}}' },
]

const loopSnippet = `{{#items}}
  {{Name}} {{Qty}} {{Price}}
{{/items}}`

const sampleData = {
  OrderNo: 'PO202605260001',
  CreatedAt: '2026-05-26 10:30:00',
  ApplicantName: 'Admin',
  Amount: 1234.56,
  items: [
    { Name: 'Sample Item A', Qty: 2, Price: 100 },
    { Name: 'Sample Item B', Qty: 3, Price: 88.5 },
  ],
}

async function loadTemplate() {
  loading.value = true
  try {
    const data = await getPrintTemplate(templateId.value)
    Object.assign(form, {
      templateCode: data.templateCode,
      templateName: data.templateName,
      businessType: data.businessType,
      templateType: data.templateType,
      contentHtml: data.contentHtml,
      contentJson: data.contentJson ?? '',
      paperSize: data.paperSize,
      orientation: data.orientation,
      isDefault: data.isDefault,
      isEnabled: data.isEnabled,
      version: data.version,
      remark: data.remark ?? '',
    })
    await preview()
  } finally {
    loading.value = false
  }
}

async function save() {
  await formRef.value?.validate()
  saving.value = true
  try {
    await updatePrintTemplate(templateId.value, {
      templateName: form.templateName.trim(),
      businessType: form.businessType.trim(),
      templateType: form.templateType.trim(),
      contentHtml: form.contentHtml.trim(),
      contentJson: form.contentJson.trim() || undefined,
      paperSize: form.paperSize,
      orientation: form.orientation,
      isDefault: form.isDefault,
      isEnabled: form.isEnabled,
      version: form.version,
      remark: form.remark.trim() || undefined,
    })
    ElMessage.success('保存成功')
  } finally {
    saving.value = false
  }
}

async function preview() {
  if (!templateId.value) {
    return
  }

  previewing.value = true
  try {
    const result = await previewPrintTemplate(templateId.value, {
      businessId: 'designer-preview',
      data: sampleData,
    })
    previewHtml.value = result.html
  } finally {
    previewing.value = false
  }
}

async function renderTest() {
  const result = await renderPrintTemplate(templateId.value, {
    businessId: 'designer-preview',
    data: sampleData,
  })
  previewHtml.value = result.html
  ElMessage.success('测试渲染成功，已记录打印日志')
}

function insertSnippet(value: string) {
  const separator = form.contentHtml.endsWith('\n') || !form.contentHtml ? '' : '\n'
  form.contentHtml = `${form.contentHtml}${separator}${value}`
}

onMounted(loadTemplate)
</script>

<template>
  <PageContainer :title="form.templateName || '打印模板设计'" :description="form.templateCode || '编辑 HTML 模板并预览变量渲染结果。'">
    <template #actions>
      <el-button @click="router.back()">返回</el-button>
      <el-button v-permission="'system:print-template:preview'" :loading="previewing" @click="preview">预览</el-button>
      <el-button v-permission="'system:print-template:print'" @click="renderTest">测试渲染</el-button>
      <el-button v-permission="'system:print-template:update'" type="primary" :loading="saving" @click="save">保存</el-button>
    </template>

    <div v-loading="loading" class="designer-layout">
      <aside class="variable-panel">
        <div class="panel-title">模板变量</div>
        <el-button
          v-for="item in variables"
          :key="item.value"
          class="variable-button"
          @click="insertSnippet(item.value)"
        >
          {{ item.label }}
        </el-button>
        <div class="panel-title loop-title">明细循环</div>
        <el-button class="variable-button" @click="insertSnippet(loopSnippet)">items 循环</el-button>
      </aside>

      <section class="editor-panel">
        <el-input v-model="form.contentHtml" type="textarea" :rows="24" resize="none" />
        <div class="preview-title">预览</div>
        <iframe class="preview-frame" :srcdoc="previewHtml" />
      </section>

      <aside class="property-panel">
        <el-form ref="formRef" :model="form" :rules="rules" label-position="top">
          <el-form-item label="模板编码">
            <el-input v-model="form.templateCode" disabled />
          </el-form-item>
          <el-form-item label="模板名称" prop="templateName">
            <el-input v-model="form.templateName" />
          </el-form-item>
          <el-form-item label="业务类型" prop="businessType">
            <el-input v-model="form.businessType" />
          </el-form-item>
          <el-form-item label="模板类型" prop="templateType">
            <el-input v-model="form.templateType" />
          </el-form-item>
          <el-form-item label="纸张" prop="paperSize">
            <el-select v-model="form.paperSize" class="full-width">
              <el-option label="A4" value="A4" />
              <el-option label="A5" value="A5" />
              <el-option label="Label" value="Label" />
            </el-select>
          </el-form-item>
          <el-form-item label="方向" prop="orientation">
            <el-select v-model="form.orientation" class="full-width">
              <el-option label="纵向" value="Portrait" />
              <el-option label="横向" value="Landscape" />
            </el-select>
          </el-form-item>
          <el-form-item label="版本">
            <el-input-number v-model="form.version" :min="1" />
          </el-form-item>
          <el-form-item label="默认模板">
            <el-switch v-model="form.isDefault" />
          </el-form-item>
          <el-form-item label="启用">
            <el-switch v-model="form.isEnabled" />
          </el-form-item>
          <el-form-item label="模板 JSON">
            <el-input v-model="form.contentJson" type="textarea" :rows="4" />
          </el-form-item>
          <el-form-item label="备注">
            <el-input v-model="form.remark" type="textarea" :rows="3" />
          </el-form-item>
        </el-form>
      </aside>
    </div>
  </PageContainer>
</template>

<style scoped>
.designer-layout {
  display: grid;
  gap: 16px;
  grid-template-columns: 180px minmax(0, 1fr) 280px;
}

.variable-panel,
.property-panel {
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  padding: 14px;
}

.panel-title,
.preview-title {
  color: var(--el-text-color-primary);
  font-weight: 600;
  margin-bottom: 12px;
}

.loop-title {
  margin-top: 18px;
}

.variable-button {
  justify-content: flex-start;
  margin: 0 0 8px;
  width: 100%;
}

.editor-panel {
  min-width: 0;
}

.preview-title {
  margin-top: 16px;
}

.preview-frame {
  background: #fff;
  border: 1px solid var(--el-border-color);
  min-height: 420px;
  width: 100%;
}

.full-width {
  width: 100%;
}

@media (max-width: 1200px) {
  .designer-layout {
    grid-template-columns: 1fr;
  }
}
</style>
