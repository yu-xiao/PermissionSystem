<script setup lang="ts">
defineOptions({
  name: 'SystemNotificationAdmin',
})

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
  title: [{ required: true, message: '请输入标题', trigger: 'blur' }],
  content: [{ required: true, message: '请输入内容', trigger: 'blur' }],
}

const templateRules: FormRules = {
  code: [{ required: true, message: '请输入编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  titleTemplate: [{ required: true, message: '请输入标题模板', trigger: 'blur' }],
  contentTemplate: [{ required: true, message: '请输入内容模板', trigger: 'blur' }],
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
    ElMessage.success('通知已加入发送队列')
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
  ElMessage.success('模板已保存')
  templateDialogVisible.value = false
  await loadTemplates()
}

async function removeTemplate(row: NotificationTemplateItem) {
  await ElMessageBox.confirm(`确认删除模板 ${row.code}？`, '确认删除')
  await deleteNotificationTemplate(row.id)
  ElMessage.success('模板已删除')
  await loadTemplates()
}

loadTemplates()
</script>

<template>
  <section class="page">
    <el-tabs v-model="activeTab">
      <el-tab-pane label="发送通知" name="send">
        <el-form :model="sendForm" :rules="rules" label-width="110px" style="max-width: 720px" @submit.prevent>
          <el-form-item label="类型">
            <el-select v-model="sendForm.type">
              <el-option label="系统" value="System" />
              <el-option label="安全" value="Security" />
              <el-option label="任务" value="Task" />
              <el-option label="审批" value="Approval" />
            </el-select>
          </el-form-item>
          <el-form-item label="标题" prop="title">
            <el-input v-model="sendForm.title" />
          </el-form-item>
          <el-form-item label="内容" prop="content">
            <el-input v-model="sendForm.content" type="textarea" :rows="5" />
          </el-form-item>
          <el-form-item label="链接">
            <el-input v-model="sendForm.linkUrl" />
          </el-form-item>
          <el-form-item label="载荷">
            <el-input v-model="sendForm.payload" type="textarea" :rows="3" />
          </el-form-item>
          <el-form-item>
            <el-button v-permission="'system:notification:send'" type="primary" :loading="sending" @click="submitNotification">
              发送
            </el-button>
          </el-form-item>
        </el-form>
      </el-tab-pane>

      <el-tab-pane label="模板" name="templates">
        <el-form class="toolbar" inline @submit.prevent>
          <el-form-item>
            <el-input v-model="templateQuery.keyword" clearable placeholder="编码 / 名称 / 标题" />
          </el-form-item>
          <el-form-item>
            <el-select v-model="templateQuery.type" clearable placeholder="类型" style="width: 140px">
              <el-option label="系统" value="System" />
              <el-option label="安全" value="Security" />
              <el-option label="任务" value="Task" />
              <el-option label="审批" value="Approval" />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-button v-permission="'system:notification-template:view'" type="primary" @click="loadTemplates">查询</el-button>
            <el-button v-permission="'system:notification-template:update'" @click="openCreateTemplate">新增</el-button>
          </el-form-item>
        </el-form>

        <el-table v-loading="templateLoading" :data="templateData" border>
          <el-table-column prop="code" label="编码" min-width="160" />
          <el-table-column prop="name" label="名称" min-width="160" />
          <el-table-column prop="type" label="类型" width="120">
            <template #default="{ row }">{{ $displayText(row.type) }}</template>
          </el-table-column>
          <el-table-column prop="titleTemplate" label="标题模板" min-width="220" show-overflow-tooltip />
          <el-table-column prop="status" label="状态" width="110">
            <template #default="{ row }">{{ $displayText(row.status) }}</template>
          </el-table-column>
          <el-table-column prop="sort" label="排序" width="90" />
          <el-table-column label="操作" width="140" fixed="right">
            <template #default="{ row }">
              <el-button v-permission="'system:notification-template:update'" link type="primary" @click="openEditTemplate(row)">编辑</el-button>
              <el-button v-permission="'system:notification-template:update'" link type="danger" @click="removeTemplate(row)">删除</el-button>
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

    <el-dialog v-model="templateDialogVisible" :title="editingTemplateId ? '编辑模板' : '新增模板'" width="720px">
      <el-form ref="templateFormRef" :model="templateForm" :rules="templateRules" label-width="130px">
        <el-form-item label="编码" prop="code">
          <el-input v-model="templateForm.code" :disabled="Boolean(editingTemplateId)" />
        </el-form-item>
        <el-form-item label="名称" prop="name">
          <el-input v-model="templateForm.name" />
        </el-form-item>
        <el-form-item label="类型">
          <el-select v-model="templateForm.type">
            <el-option label="系统" value="System" />
            <el-option label="安全" value="Security" />
            <el-option label="任务" value="Task" />
            <el-option label="审批" value="Approval" />
          </el-select>
        </el-form-item>
        <el-form-item label="标题模板" prop="titleTemplate">
          <el-input v-model="templateForm.titleTemplate" />
        </el-form-item>
        <el-form-item label="内容模板" prop="contentTemplate">
          <el-input v-model="templateForm.contentTemplate" type="textarea" :rows="5" />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="templateForm.status">
            <el-option label="启用" value="Enabled" />
            <el-option label="禁用" value="Disabled" />
          </el-select>
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="templateForm.sort" :min="0" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="templateForm.remark" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="templateDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveTemplate">保存</el-button>
      </template>
    </el-dialog>
  </section>
</template>
