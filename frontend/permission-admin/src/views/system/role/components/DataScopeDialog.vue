<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { getDepartmentTree, type DepartmentItem } from '../../../../api/departments'
import { DataScopeType, type DataScopeType as DataScopeTypeValue } from '../../../../api/roles'

const props = defineProps<{
  modelValue: boolean
  menuName?: string
  tenantId?: string
  scopeType?: DataScopeTypeValue
  departmentIds?: string[]
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  save: [value: { scopeType: DataScopeTypeValue; departmentIds: string[] }]
}>()

const departments = ref<DepartmentItem[]>([])
const loading = ref(false)
const localScopeType = ref<DataScopeTypeValue>(DataScopeType.All)
const localDepartmentIds = ref<string[]>([])

const visible = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value),
})

watch(
  () => props.modelValue,
  async (value) => {
    if (!value) {
      return
    }

    localScopeType.value = props.scopeType ?? DataScopeType.All
    localDepartmentIds.value = [...(props.departmentIds ?? [])]

    if (localScopeType.value === DataScopeType.CustomDepartments) {
      await loadDepartments()
    }
  },
)

watch(localScopeType, async (value) => {
  if (value === DataScopeType.CustomDepartments) {
    await loadDepartments()
  } else {
    localDepartmentIds.value = []
  }
})

async function loadDepartments() {
  if (departments.value.length > 0 || loading.value) {
    return
  }

  loading.value = true
  try {
    departments.value = await getDepartmentTree(props.tenantId)
  } finally {
    loading.value = false
  }
}

function save() {
  emit('save', {
    scopeType: localScopeType.value,
    departmentIds:
      localScopeType.value === DataScopeType.CustomDepartments ? [...localDepartmentIds.value] : [],
  })
  visible.value = false
}
</script>

<template>
  <el-dialog v-model="visible" :title="`数据范围 - ${menuName || ''}`" width="640px" append-to-body>
    <el-form label-width="130px">
      <el-form-item label="范围">
        <el-radio-group v-model="localScopeType" class="scope-options">
          <el-radio :value="DataScopeType.All">全部数据</el-radio>
          <el-radio :value="DataScopeType.CurrentUser">本人数据</el-radio>
          <el-radio :value="DataScopeType.CurrentDepartment">本部门数据</el-radio>
          <el-radio :value="DataScopeType.CurrentDepartmentAndChildren">本部门及下级</el-radio>
          <el-radio :value="DataScopeType.CustomDepartments">自定义部门</el-radio>
        </el-radio-group>
      </el-form-item>
      <el-form-item v-if="localScopeType === DataScopeType.CustomDepartments" label="部门">
        <el-tree-select
          v-model="localDepartmentIds"
          v-loading="loading"
          :data="departments"
          multiple
          show-checkbox
          node-key="id"
          :props="{ label: 'name', children: 'children' }"
          class="full-width"
        />
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="visible = false">取消</el-button>
      <el-button type="primary" @click="save">确定</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.scope-options {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px 16px;
}

.full-width {
  width: 100%;
}
</style>
