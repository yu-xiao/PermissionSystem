<script setup lang="ts">
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
  code: [{ required: true, message: 'Please enter type code', trigger: 'blur' }],
  name: [{ required: true, message: 'Please enter type name', trigger: 'blur' }],
}

const itemRules: FormRules = {
  label: [{ required: true, message: 'Please enter item label', trigger: 'blur' }],
  value: [{ required: true, message: 'Please enter item value', trigger: 'blur' }],
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

  ElMessage.success('Saved successfully')
  typeDialogVisible.value = false
  await loadTypes()
}

async function removeType(row: DictionaryTypeItem) {
  await ElMessageBox.confirm(`Delete dictionary type ${row.name}?`, 'Confirm delete')
  await deleteDictionaryType(row.id)
  if (selectedType.value?.id === row.id) {
    selectedType.value = undefined
  }
  ElMessage.success('Deleted successfully')
  await loadTypes()
  await loadItems()
}

async function toggleTypeStatus(row: DictionaryTypeItem) {
  await updateDictionaryType(row.id, {
    name: row.name,
    description: row.description,
    status: row.status === 'Enabled' ? 'Disabled' : 'Enabled',
    sort: row.sort,
  })
  ElMessage.success('Status updated')
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

  ElMessage.success('Saved successfully')
  itemDialogVisible.value = false
  await loadItems()
}

async function removeItem(row: DictionaryItem) {
  await ElMessageBox.confirm(`Delete dictionary item ${row.label}?`, 'Confirm delete')
  await deleteDictionaryItem(row.id)
  ElMessage.success('Deleted successfully')
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
  })
  ElMessage.success('Status updated')
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
  <section class="page dict-page">
    <div class="dict-layout">
      <section class="dict-panel type-panel">
        <el-form class="toolbar compact-toolbar" inline @submit.prevent>
          <el-form-item>
            <el-input v-model="typeQuery.keyword" clearable placeholder="Code / name" />
          </el-form-item>
          <el-form-item>
            <el-select v-model="typeQuery.status" clearable placeholder="Status" style="width: 128px">
              <el-option v-for="status in statusOptions" :key="status" :label="status" :value="status" />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-button v-permission="'system:dict:view'" type="primary" @click="loadTypes">Search</el-button>
            <el-button @click="resetTypeQuery">Reset</el-button>
            <el-button v-permission="'system:dict:create'" @click="openCreateType">Create</el-button>
          </el-form-item>
        </el-form>

        <el-table
          v-loading="typeLoading"
          :data="typeData"
          border
          highlight-current-row
          @row-click="selectType"
        >
          <el-table-column prop="code" label="Code" min-width="120" show-overflow-tooltip />
          <el-table-column prop="name" label="Name" min-width="140" show-overflow-tooltip />
          <el-table-column prop="status" label="Status" width="100">
            <template #default="{ row }">
              <el-tag :type="statusTagType(row.status)">{{ row.status }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="sort" label="Sort" width="72" />
          <el-table-column label="Actions" width="180" fixed="right">
            <template #default="{ row }">
              <el-button v-permission="'system:dict:update'" link type="primary" @click.stop="openEditType(row)">
                Edit
              </el-button>
              <el-button v-permission="'system:dict:update'" link type="primary" @click.stop="toggleTypeStatus(row)">
                {{ row.status === 'Enabled' ? 'Disable' : 'Enable' }}
              </el-button>
              <el-button v-permission="'system:dict:delete'" link type="danger" @click.stop="removeType(row)">
                Delete
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
          <strong>{{ selectedType?.name ?? 'No dictionary type selected' }}</strong>
          <span v-if="selectedType">{{ selectedType.code }}</span>
        </div>

        <el-form class="toolbar compact-toolbar" inline @submit.prevent>
          <el-form-item>
            <el-input v-model="itemQuery.keyword" clearable placeholder="Label / value / remark" />
          </el-form-item>
          <el-form-item>
            <el-select v-model="itemQuery.status" clearable placeholder="Status" style="width: 128px">
              <el-option v-for="status in statusOptions" :key="status" :label="status" :value="status" />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-button v-permission="'system:dict:view'" type="primary" :disabled="!selectedType" @click="loadItems">
              Search
            </el-button>
            <el-button :disabled="!selectedType" @click="resetItemQuery">Reset</el-button>
            <el-button v-permission="'system:dict:create'" :disabled="!selectedType" @click="openCreateItem">
              Create
            </el-button>
          </el-form-item>
        </el-form>

        <el-table v-loading="itemLoading" :data="itemData" border>
          <el-table-column prop="label" label="Label" min-width="140" show-overflow-tooltip />
          <el-table-column prop="value" label="Value" min-width="140" show-overflow-tooltip />
          <el-table-column prop="color" label="Color" width="100">
            <template #default="{ row }">
              <span class="color-cell">
                <span class="color-dot" :style="{ backgroundColor: row.color || '#dcdfe6' }" />
                {{ row.color || '-' }}
              </span>
            </template>
          </el-table-column>
          <el-table-column prop="cssClass" label="Class" width="110" show-overflow-tooltip />
          <el-table-column prop="isDefault" label="Default" width="92">
            <template #default="{ row }">
              <el-tag v-if="row.isDefault" type="success">Yes</el-tag>
              <span v-else>-</span>
            </template>
          </el-table-column>
          <el-table-column prop="status" label="Status" width="100">
            <template #default="{ row }">
              <el-tag :type="statusTagType(row.status)">{{ row.status }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="sort" label="Sort" width="72" />
          <el-table-column prop="remark" label="Remark" min-width="160" show-overflow-tooltip />
          <el-table-column label="Actions" width="180" fixed="right">
            <template #default="{ row }">
              <el-button v-permission="'system:dict:update'" link type="primary" @click="openEditItem(row)">
                Edit
              </el-button>
              <el-button v-permission="'system:dict:update'" link type="primary" @click="toggleItemStatus(row)">
                {{ row.status === 'Enabled' ? 'Disable' : 'Enable' }}
              </el-button>
              <el-button v-permission="'system:dict:delete'" link type="danger" @click="removeItem(row)">
                Delete
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

    <el-dialog v-model="typeDialogVisible" :title="editingTypeId ? 'Edit Dictionary Type' : 'Create Dictionary Type'" width="560px">
      <el-form ref="typeFormRef" :model="typeForm" :rules="typeRules" label-width="120px">
        <el-form-item label="Code" prop="code">
          <el-input v-model="typeForm.code" :disabled="Boolean(editingTypeId)" />
        </el-form-item>
        <el-form-item label="Name" prop="name">
          <el-input v-model="typeForm.name" />
        </el-form-item>
        <el-form-item label="Description">
          <el-input v-model="typeForm.description" type="textarea" />
        </el-form-item>
        <el-form-item label="Status">
          <el-select v-model="typeForm.status">
            <el-option v-for="status in statusOptions" :key="status" :label="status" :value="status" />
          </el-select>
        </el-form-item>
        <el-form-item label="Sort">
          <el-input-number v-model="typeForm.sort" :min="0" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="typeDialogVisible = false">Cancel</el-button>
        <el-button type="primary" @click="saveType">Save</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="itemDialogVisible" :title="editingItemId ? 'Edit Dictionary Item' : 'Create Dictionary Item'" width="600px">
      <el-form ref="itemFormRef" :model="itemForm" :rules="itemRules" label-width="120px">
        <el-form-item label="Type">
          <el-input :model-value="selectedType?.code" disabled />
        </el-form-item>
        <el-form-item label="Label" prop="label">
          <el-input v-model="itemForm.label" />
        </el-form-item>
        <el-form-item label="Value" prop="value">
          <el-input v-model="itemForm.value" />
        </el-form-item>
        <el-form-item label="Color">
          <el-color-picker v-model="itemForm.color" />
        </el-form-item>
        <el-form-item label="Css Class">
          <el-input v-model="itemForm.cssClass" />
        </el-form-item>
        <el-form-item label="Default">
          <el-switch v-model="itemForm.isDefault" />
        </el-form-item>
        <el-form-item label="Status">
          <el-select v-model="itemForm.status">
            <el-option v-for="status in statusOptions" :key="status" :label="status" :value="status" />
          </el-select>
        </el-form-item>
        <el-form-item label="Sort">
          <el-input-number v-model="itemForm.sort" :min="0" />
        </el-form-item>
        <el-form-item label="Remark">
          <el-input v-model="itemForm.remark" type="textarea" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="itemDialogVisible = false">Cancel</el-button>
        <el-button type="primary" @click="saveItem">Save</el-button>
      </template>
    </el-dialog>
  </section>
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
