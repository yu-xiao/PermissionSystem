<script setup lang="ts">
defineOptions({
  name: 'SystemDict',
})

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import {
  createDictionaryItem,
  createDictionaryType,
  deleteDictionaryItem,
  deleteDictionaryType,
  getDictionaryItems,
  getDictionaryTypes,
  updateDictionaryItem,
  updateDictionaryType,
  type DictionaryItem,
  type DictionaryStatus,
  type DictionaryTypeItem,
} from '../../../api/dictionaries'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.currentUser?.tenantId ?? '')

const statusOptions: DictionaryStatus[] = ['Enabled', 'Disabled']
const typeLoading = ref(false)
const itemLoading = ref(false)
const typeData = ref<DictionaryTypeItem[]>([])
const itemData = ref<DictionaryItem[]>([])
const selectedType = ref<DictionaryTypeItem>()
const typeTotal = ref(0)
const itemTotal = ref(0)
const typeFormRef = ref<FormInstance>()
const itemFormRef = ref<FormInstance>()
const typeDialogVisible = ref(false)
const itemDialogVisible = ref(false)
const editingTypeId = ref('')
const editingItemId = ref('')
const editingType = ref<DictionaryTypeItem | null>(null)
const editingItem = ref<DictionaryItem | null>(null)

const typeQuery = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  status: undefined as DictionaryStatus | undefined,
})

const itemQuery = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  status: undefined as DictionaryStatus | undefined,
})

const typeForm = reactive({
  code: '',
  name: '',
  description: '',
  status: 'Enabled' as DictionaryStatus,
  sort: 0,
})

const itemForm = reactive({
  label: '',
  value: '',
  color: '',
  cssClass: '',
  isDefault: false,
  status: 'Enabled' as DictionaryStatus,
  sort: 0,
  remark: '',
})

const typeRules: FormRules = {
  code: [{ required: true, message: '请输入类型编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入类型名称', trigger: 'blur' }],
}

const itemRules: FormRules = {
  label: [{ required: true, message: '请输入字典项标签', trigger: 'blur' }],
  value: [{ required: true, message: '请输入字典项值', trigger: 'blur' }],
}

async function loadTypes() {
  typeLoading.value = true
  try {
    const result = await getDictionaryTypes(typeQuery)
    typeData.value = result.items
    typeTotal.value = result.totalCount

    if (selectedType.value && !typeData.value.some((item) => item.id === selectedType.value?.id)) {
      selectedType.value = undefined
    }

    if (!selectedType.value && typeData.value.length > 0) {
      selectedType.value = typeData.value[0]
      itemQuery.pageIndex = 1
      await loadItems()
    }
  } finally {
    typeLoading.value = false
  }
}

async function loadItems() {
  if (!selectedType.value) {
    itemData.value = []
    itemTotal.value = 0
    return
  }

  itemLoading.value = true
  try {
    const result = await getDictionaryItems({
      ...itemQuery,
      typeCode: selectedType.value.code,
    })
    itemData.value = result.items
    itemTotal.value = result.totalCount
  } finally {
    itemLoading.value = false
  }
}

function selectType(row: DictionaryTypeItem) {
  selectedType.value = row
  itemQuery.pageIndex = 1
  loadItems()
}

function openCreateType() {
  editingTypeId.value = ''
  editingType.value = null
  Object.assign(typeForm, {
    code: '',
    name: '',
    description: '',
    status: 'Enabled',
    sort: 0,
  })
  typeDialogVisible.value = true
}

function openEditType(row: DictionaryTypeItem) {
  editingTypeId.value = row.id
  editingType.value = row
  Object.assign(typeForm, {
    code: row.code,
    name: row.name,
    description: row.description ?? '',
    status: row.status,
    sort: row.sort,
  })
  typeDialogVisible.value = true
}

async function saveType() {
  await typeFormRef.value?.validate()

  if (editingTypeId.value) {
    await updateDictionaryType(editingTypeId.value, {
      name: typeForm.name,
      description: typeForm.description,
      status: typeForm.status,
      sort: typeForm.sort,
      concurrencyToken: editingType.value?.concurrencyToken,
    })
  } else {
    await createDictionaryType({
      tenantId: tenantId.value,
      code: typeForm.code,
      name: typeForm.name,
      description: typeForm.description,
      status: typeForm.status,
      sort: typeForm.sort,
    })
  }

  ElMessage.success('保存成功')
  typeDialogVisible.value = false
  await loadTypes()
}

async function removeType(row: DictionaryTypeItem) {
  await ElMessageBox.confirm(`确认删除字典类型 ${row.name}？`, '确认删除')
  await deleteDictionaryType(row.id)
  if (selectedType.value?.id === row.id) {
    selectedType.value = undefined
  }
  ElMessage.success('删除成功')
  await loadTypes()
  await loadItems()
}

async function toggleTypeStatus(row: DictionaryTypeItem) {
  await updateDictionaryType(row.id, {
    name: row.name,
    description: row.description,
    status: row.status === 'Enabled' ? 'Disabled' : 'Enabled',
    sort: row.sort,
    concurrencyToken: row.concurrencyToken,
  })
  ElMessage.success('状态已更新')
  await loadTypes()
  await loadItems()
}

function resetTypeQuery() {
  Object.assign(typeQuery, {
    pageIndex: 1,
    keyword: '',
    status: undefined,
  })
  loadTypes()
}

function openCreateItem() {
  if (!selectedType.value) {
    return
  }

  editingItemId.value = ''
  editingItem.value = null
  Object.assign(itemForm, {
    label: '',
    value: '',
    color: '',
    cssClass: '',
    isDefault: false,
    status: 'Enabled',
    sort: 0,
    remark: '',
  })
  itemDialogVisible.value = true
}

function openEditItem(row: DictionaryItem) {
  editingItemId.value = row.id
  editingItem.value = row
  Object.assign(itemForm, {
    label: row.label,
    value: row.value,
    color: row.color ?? '',
    cssClass: row.cssClass ?? '',
    isDefault: row.isDefault,
    status: row.status,
    sort: row.sort,
    remark: row.remark ?? '',
  })
  itemDialogVisible.value = true
}

async function saveItem() {
  await itemFormRef.value?.validate()
  if (!selectedType.value) {
    return
  }

  const payload = {
    label: itemForm.label,
    value: itemForm.value,
    color: itemForm.color,
    cssClass: itemForm.cssClass,
    isDefault: itemForm.isDefault,
    status: itemForm.status,
    sort: itemForm.sort,
    remark: itemForm.remark,
    concurrencyToken: editingItem.value?.concurrencyToken,
  }

  if (editingItemId.value) {
    await updateDictionaryItem(editingItemId.value, payload)
  } else {
    await createDictionaryItem({
      tenantId: tenantId.value,
      typeCode: selectedType.value.code,
      ...payload,
    })
  }

  ElMessage.success('保存成功')
  itemDialogVisible.value = false
  await loadItems()
}

async function removeItem(row: DictionaryItem) {
  await ElMessageBox.confirm(`确认删除字典项 ${row.label}？`, '确认删除')
  await deleteDictionaryItem(row.id)
  ElMessage.success('删除成功')
  await loadItems()
}

async function toggleItemStatus(row: DictionaryItem) {
  await updateDictionaryItem(row.id, {
    label: row.label,
    value: row.value,
    color: row.color,
    cssClass: row.cssClass,
    isDefault: row.isDefault,
    status: row.status === 'Enabled' ? 'Disabled' : 'Enabled',
    sort: row.sort,
    remark: row.remark,
    concurrencyToken: row.concurrencyToken,
  })
  ElMessage.success('状态已更新')
  await loadItems()
}

function resetItemQuery() {
  Object.assign(itemQuery, {
    pageIndex: 1,
    keyword: '',
    status: undefined,
  })
  loadItems()
}

function statusTagType(status: DictionaryStatus) {
  return status === 'Enabled' ? 'success' : 'info'
}

loadTypes()
</script>

<template>
  <PageContainer class="dict-page" title="字典管理" description="维护字典类型和字典项，统一系统枚举展示。">
    <template #actions>
      <TableToolbar @refresh="loadTypes" />
    </template>

    <div class="dict-layout">
      <section class="dict-panel type-panel">
        <el-form class="toolbar compact-toolbar" inline @submit.prevent>
          <el-form-item>
            <el-input v-model="typeQuery.keyword" clearable placeholder="编码 / 名称" />
          </el-form-item>
          <el-form-item>
            <el-select v-model="typeQuery.status" clearable placeholder="状态" style="width: 128px">
              <el-option v-for="status in statusOptions" :key="status" :label="$displayText(status)" :value="status" />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-button v-permission="'system:dict:view'" type="primary" @click="loadTypes">查询</el-button>
            <el-button @click="resetTypeQuery">重置</el-button>
            <el-button v-permission="'system:dict:create'" @click="openCreateType">新增</el-button>
          </el-form-item>
        </el-form>

        <el-table
          v-loading="typeLoading"
          :data="typeData"
          border
          highlight-current-row
          @row-click="selectType"
        >
          <el-table-column prop="code" label="编码" min-width="120" show-overflow-tooltip />
          <el-table-column prop="name" label="名称" min-width="140" show-overflow-tooltip />
          <el-table-column prop="status" label="状态" width="100">
            <template #default="{ row }">
              <el-tag :type="statusTagType(row.status)">{{ $displayText(row.status) }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="sort" label="排序" width="72" />
          <el-table-column label="操作" width="180" fixed="right">
            <template #default="{ row }">
              <el-button v-permission="'system:dict:update'" link type="primary" @click.stop="openEditType(row)">
                编辑
              </el-button>
              <el-button v-permission="'system:dict:update'" link type="primary" @click.stop="toggleTypeStatus(row)">
                {{ row.status === 'Enabled' ? '禁用' : '启用' }}
              </el-button>
              <el-button v-permission="'system:dict:delete'" link type="danger" @click.stop="removeType(row)">
                删除
              </el-button>
            </template>
          </el-table-column>
        </el-table>

        <el-pagination
          v-model:current-page="typeQuery.pageIndex"
          v-model:page-size="typeQuery.pageSize"
          class="pager"
          background
          layout="total, prev, pager, next"
          :total="typeTotal"
          @change="loadTypes"
        />
      </section>

      <section class="dict-panel item-panel">
        <div class="selected-type">
          <strong>{{ selectedType?.name ?? '未选择字典类型' }}</strong>
          <span v-if="selectedType">{{ selectedType.code }}</span>
        </div>

        <el-form class="toolbar compact-toolbar" inline @submit.prevent>
          <el-form-item>
            <el-input v-model="itemQuery.keyword" clearable placeholder="标签 / 值 / 备注" />
          </el-form-item>
          <el-form-item>
            <el-select v-model="itemQuery.status" clearable placeholder="状态" style="width: 128px">
              <el-option v-for="status in statusOptions" :key="status" :label="$displayText(status)" :value="status" />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-button v-permission="'system:dict:view'" type="primary" :disabled="!selectedType" @click="loadItems">
              查询
            </el-button>
            <el-button :disabled="!selectedType" @click="resetItemQuery">重置</el-button>
            <el-button v-permission="'system:dict:create'" :disabled="!selectedType" @click="openCreateItem">
              新增
            </el-button>
          </el-form-item>
        </el-form>

        <el-table v-loading="itemLoading" :data="itemData" border>
          <el-table-column prop="label" label="标签" min-width="140" show-overflow-tooltip />
          <el-table-column prop="value" label="值" min-width="140" show-overflow-tooltip />
          <el-table-column prop="color" label="颜色" width="100">
            <template #default="{ row }">
              <span class="color-cell">
                <span class="color-dot" :style="{ backgroundColor: row.color || '#dcdfe6' }" />
                {{ row.color || '-' }}
              </span>
            </template>
          </el-table-column>
          <el-table-column prop="cssClass" label="样式类" width="110" show-overflow-tooltip />
          <el-table-column prop="isDefault" label="默认" width="92">
            <template #default="{ row }">
              <el-tag v-if="row.isDefault" type="success">是</el-tag>
              <span v-else>-</span>
            </template>
          </el-table-column>
          <el-table-column prop="status" label="状态" width="100">
            <template #default="{ row }">
              <el-tag :type="statusTagType(row.status)">{{ $displayText(row.status) }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="sort" label="排序" width="72" />
          <el-table-column prop="remark" label="备注" min-width="160" show-overflow-tooltip />
          <el-table-column label="操作" width="180" fixed="right">
            <template #default="{ row }">
              <el-button v-permission="'system:dict:update'" link type="primary" @click="openEditItem(row)">
                编辑
              </el-button>
              <el-button v-permission="'system:dict:update'" link type="primary" @click="toggleItemStatus(row)">
                {{ row.status === 'Enabled' ? '禁用' : '启用' }}
              </el-button>
              <el-button v-permission="'system:dict:delete'" link type="danger" @click="removeItem(row)">
                删除
              </el-button>
            </template>
          </el-table-column>
        </el-table>

        <el-pagination
          v-model:current-page="itemQuery.pageIndex"
          v-model:page-size="itemQuery.pageSize"
          class="pager"
          background
          layout="total, sizes, prev, pager, next"
          :total="itemTotal"
          @change="loadItems"
        />
      </section>
    </div>

    <el-dialog v-model="typeDialogVisible" :title="editingTypeId ? '编辑字典类型' : '新增字典类型'" width="560px">
      <el-form ref="typeFormRef" :model="typeForm" :rules="typeRules" label-width="120px">
        <el-form-item label="编码" prop="code">
          <el-input v-model="typeForm.code" :disabled="Boolean(editingTypeId)" />
        </el-form-item>
        <el-form-item label="名称" prop="name">
          <el-input v-model="typeForm.name" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="typeForm.description" type="textarea" />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="typeForm.status">
            <el-option v-for="status in statusOptions" :key="status" :label="$displayText(status)" :value="status" />
          </el-select>
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="typeForm.sort" :min="0" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="typeDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveType">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="itemDialogVisible" :title="editingItemId ? '编辑字典项' : '新增字典项'" width="600px">
      <el-form ref="itemFormRef" :model="itemForm" :rules="itemRules" label-width="120px">
        <el-form-item label="类型">
          <el-input :model-value="selectedType?.code" disabled />
        </el-form-item>
        <el-form-item label="标签" prop="label">
          <el-input v-model="itemForm.label" />
        </el-form-item>
        <el-form-item label="值" prop="value">
          <el-input v-model="itemForm.value" />
        </el-form-item>
        <el-form-item label="颜色">
          <el-color-picker v-model="itemForm.color" />
        </el-form-item>
        <el-form-item label="CSS 类">
          <el-input v-model="itemForm.cssClass" />
        </el-form-item>
        <el-form-item label="默认">
          <el-switch v-model="itemForm.isDefault" />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="itemForm.status">
            <el-option v-for="status in statusOptions" :key="status" :label="$displayText(status)" :value="status" />
          </el-select>
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="itemForm.sort" :min="0" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="itemForm.remark" type="textarea" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="itemDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveItem">保存</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.dict-page {
  min-width: 0;
}

.dict-layout {
  display: grid;
  grid-template-columns: minmax(420px, 0.9fr) minmax(540px, 1.1fr);
  gap: 16px;
  min-width: 0;
}

.dict-panel {
  min-width: 0;
  padding: 16px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  background: var(--el-bg-color);
}

.compact-toolbar {
  gap: 8px;
}

.selected-type {
  display: flex;
  align-items: center;
  gap: 10px;
  min-height: 32px;
  margin-bottom: 12px;
}

.selected-type span {
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.color-cell {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.color-dot {
  width: 12px;
  height: 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 50%;
}

@media (max-width: 1180px) {
  .dict-layout {
    grid-template-columns: 1fr;
  }
}
</style>
