<script setup lang="ts">
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { reactive, ref } from 'vue'
import {
  createNotificationTemplate,
  deleteNotificationTemplate,
  getNotificationTemplates,
  sendSystemNotification,
  updateNotificationTemplate,
  type NotificationTemplateItem,
} from '../../../api/notifications'

const activeTab = ref('send')
const sending = ref(false)
const templateLoading = ref(false)
const templateDialogVisible = ref(false)
const editingTemplateId = ref('')
const templateFormRef = ref<FormInstance>()
const templateData = ref<NotificationTemplateItem[]>([])
const templateTotal = ref(0)

const sendForm = reactive({
  type: 'System' as const,
  title: '',
  content: '',
  linkUrl: '',
  payload: '',
})

const templateQuery = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  type: '',
  status: '',
})

const templateForm = reactive({
  code: '',
  name: '',
  type: 'System' as const,
  titleTemplate: '',
  contentTemplate: '',
  status: 'Enabled',
  sort: 0,
  remark: '',
})

const rules: FormRules = {
  title: [{ required: true, message: 'Please input title.', trigger: 'blur' }],
  content: [{ required: true, message: 'Please input content.', trigger: 'blur' }],
}

const templateRules: FormRules = {
  code: [{ required: true, message: 'Please input code.', trigger: 'blur' }],
  name: [{ required: true, message: 'Please input name.', trigger: 'blur' }],
  titleTemplate: [{ required: true, message: 'Please input title template.', trigger: 'blur' }],
  contentTemplate: [{ required: true, message: 'Please input content template.', trigger: 'blur' }],
}

async function submitNotification(form?: FormInstance) {
  await form?.validate()
  sending.value = true
  try {
    await sendSystemNotification({
      ...sendForm,
      linkUrl: sendForm.linkUrl || undefined,
      payload: sendForm.payload || undefined,
    })
    ElMessage.success('Notification queued.')
    Object.assign(sendForm, { title: '', content: '', linkUrl: '', payload: '' })
  } finally {
    sending.value = false
  }
}

async function loadTemplates() {
  templateLoading.value = true
  try {
    const result = await getNotificationTemplates({
      ...templateQuery,
      type: templateQuery.type || undefined,
      status: templateQuery.status || undefined,
    })
    templateData.value = result.items
    templateTotal.value = result.totalCount
  } finally {
    templateLoading.value = false
  }
}

function openCreateTemplate() {
  editingTemplateId.value = ''
  Object.assign(templateForm, {
    code: '',
    name: '',
    type: 'System',
    titleTemplate: '',
    contentTemplate: '',
    status: 'Enabled',
    sort: 0,
    remark: '',
  })
  templateDialogVisible.value = true
}

function openEditTemplate(row: NotificationTemplateItem) {
  editingTemplateId.value = row.id
  Object.assign(templateForm, row)
  templateDialogVisible.value = true
}

async function saveTemplate() {
  await templateFormRef.value?.validate()
  if (editingTemplateId.value) {
    await updateNotificationTemplate(editingTemplateId.value, templateForm)
  } else {
    await createNotificationTemplate(templateForm)
  }
  ElMessage.success('Template saved.')
  templateDialogVisible.value = false
  await loadTemplates()
}

async function removeTemplate(row: NotificationTemplateItem) {
  await ElMessageBox.confirm(`Delete template "${row.code}"?`, 'Confirm Delete')
  await deleteNotificationTemplate(row.id)
  ElMessage.success('Template deleted.')
  await loadTemplates()
}

loadTemplates()
</script>

<template>
  <section class="page">
    <el-tabs v-model="activeTab">
      <el-tab-pane label="Send Notification" name="send">
        <el-form :model="sendForm" :rules="rules" label-width="110px" style="max-width: 720px" @submit.prevent>
          <el-form-item label="Type">
            <el-select v-model="sendForm.type">
              <el-option label="System" value="System" />
              <el-option label="Security" value="Security" />
              <el-option label="Task" value="Task" />
              <el-option label="Approval" value="Approval" />
            </el-select>
          </el-form-item>
          <el-form-item label="Title" prop="title">
            <el-input v-model="sendForm.title" />
          </el-form-item>
          <el-form-item label="Content" prop="content">
            <el-input v-model="sendForm.content" type="textarea" :rows="5" />
          </el-form-item>
          <el-form-item label="Link">
            <el-input v-model="sendForm.linkUrl" />
          </el-form-item>
          <el-form-item label="Payload">
            <el-input v-model="sendForm.payload" type="textarea" :rows="3" />
          </el-form-item>
          <el-form-item>
            <el-button v-permission="'system:notification:send'" type="primary" :loading="sending" @click="submitNotification">
              Send
            </el-button>
          </el-form-item>
        </el-form>
      </el-tab-pane>

      <el-tab-pane label="Templates" name="templates">
        <el-form class="toolbar" inline @submit.prevent>
          <el-form-item>
            <el-input v-model="templateQuery.keyword" clearable placeholder="Code / name / title" />
          </el-form-item>
          <el-form-item>
            <el-select v-model="templateQuery.type" clearable placeholder="Type" style="width: 140px">
              <el-option label="System" value="System" />
              <el-option label="Security" value="Security" />
              <el-option label="Task" value="Task" />
              <el-option label="Approval" value="Approval" />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-button v-permission="'system:notification-template:view'" type="primary" @click="loadTemplates">Search</el-button>
            <el-button v-permission="'system:notification-template:update'" @click="openCreateTemplate">New</el-button>
          </el-form-item>
        </el-form>

        <el-table v-loading="templateLoading" :data="templateData" border>
          <el-table-column prop="code" label="Code" min-width="160" />
          <el-table-column prop="name" label="Name" min-width="160" />
          <el-table-column prop="type" label="Type" width="120" />
          <el-table-column prop="titleTemplate" label="Title Template" min-width="220" show-overflow-tooltip />
          <el-table-column prop="status" label="Status" width="110" />
          <el-table-column prop="sort" label="Sort" width="90" />
          <el-table-column label="Actions" width="140" fixed="right">
            <template #default="{ row }">
              <el-button v-permission="'system:notification-template:update'" link type="primary" @click="openEditTemplate(row)">Edit</el-button>
              <el-button v-permission="'system:notification-template:update'" link type="danger" @click="removeTemplate(row)">Delete</el-button>
            </template>
          </el-table-column>
        </el-table>

        <el-pagination
          v-model:current-page="templateQuery.pageIndex"
          v-model:page-size="templateQuery.pageSize"
          class="pager"
          background
          layout="total, sizes, prev, pager, next"
          :total="templateTotal"
          @change="loadTemplates"
        />
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="templateDialogVisible" :title="editingTemplateId ? 'Edit Template' : 'New Template'" width="720px">
      <el-form ref="templateFormRef" :model="templateForm" :rules="templateRules" label-width="130px">
        <el-form-item label="Code" prop="code">
          <el-input v-model="templateForm.code" :disabled="Boolean(editingTemplateId)" />
        </el-form-item>
        <el-form-item label="Name" prop="name">
          <el-input v-model="templateForm.name" />
        </el-form-item>
        <el-form-item label="Type">
          <el-select v-model="templateForm.type">
            <el-option label="System" value="System" />
            <el-option label="Security" value="Security" />
            <el-option label="Task" value="Task" />
            <el-option label="Approval" value="Approval" />
          </el-select>
        </el-form-item>
        <el-form-item label="Title Template" prop="titleTemplate">
          <el-input v-model="templateForm.titleTemplate" />
        </el-form-item>
        <el-form-item label="Content Template" prop="contentTemplate">
          <el-input v-model="templateForm.contentTemplate" type="textarea" :rows="5" />
        </el-form-item>
        <el-form-item label="Status">
          <el-select v-model="templateForm.status">
            <el-option label="Enabled" value="Enabled" />
            <el-option label="Disabled" value="Disabled" />
          </el-select>
        </el-form-item>
        <el-form-item label="Sort">
          <el-input-number v-model="templateForm.sort" :min="0" />
        </el-form-item>
        <el-form-item label="Remark">
          <el-input v-model="templateForm.remark" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="templateDialogVisible = false">Cancel</el-button>
        <el-button type="primary" @click="saveTemplate">Save</el-button>
      </template>
    </el-dialog>
  </section>
</template>
