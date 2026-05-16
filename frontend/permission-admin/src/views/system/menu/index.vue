<script setup lang="ts">
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import { createMenu, deleteMenu, getMenuTree, updateMenu, type MenuItem } from '../../../api/menus'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.currentUser?.tenantId ?? '')
const loading = ref(false)
const tableData = ref<MenuItem[]>([])
const formRef = ref<FormInstance>()
const dialogVisible = ref(false)
const editingId = ref('')
const form = reactive({
  parentId: undefined as string | undefined,
  name: '',
  path: '',
  component: '',
  redirect: '',
  icon: '',
  sort: 0,
  visible: true,
  keepAlive: false,
  menuType: 'Menu',
  permissionCode: '',
})

const rules: FormRules = {
  name: [{ required: true, message: '请输入菜单名称', trigger: 'blur' }],
  menuType: [{ required: true, message: '请选择菜单类型', trigger: 'change' }],
}

async function loadData() {
  loading.value = true
  try {
    tableData.value = await getMenuTree(tenantId.value)
  } finally {
    loading.value = false
  }
}

function openCreate(parent?: MenuItem) {
  editingId.value = ''
  Object.assign(form, {
    parentId: parent?.id,
    name: '',
    path: '',
    component: '',
    redirect: '',
    icon: '',
    sort: 0,
    visible: true,
    keepAlive: false,
    menuType: 'Menu',
    permissionCode: '',
  })
  dialogVisible.value = true
}

function openEdit(row: MenuItem) {
  editingId.value = row.id
  Object.assign(form, {
    parentId: row.parentId,
    name: row.name,
    path: row.path ?? '',
    component: row.component ?? '',
    redirect: row.redirect ?? '',
    icon: row.icon ?? '',
    sort: row.sort,
    visible: row.visible,
    keepAlive: row.keepAlive,
    menuType: row.menuType,
    permissionCode: row.permissionCode ?? '',
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  const payload = { ...form, parentId: form.parentId || undefined }
  if (editingId.value) {
    await updateMenu(editingId.value, payload)
  } else {
    await createMenu({ tenantId: tenantId.value, ...payload })
  }
  ElMessage.success('保存成功')
  dialogVisible.value = false
  await loadData()
}

async function remove(row: MenuItem) {
  await ElMessageBox.confirm(`确定删除菜单「${row.name}」吗？`, '确认删除')
  await deleteMenu(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

loadData()
</script>

<template>
  <section class="page">
    <div class="toolbar">
      <el-button type="primary" @click="loadData">刷新</el-button>
      <el-button v-permission="'system:menu:create'" @click="openCreate()">新增</el-button>
    </div>

    <el-table v-loading="loading" :data="tableData" row-key="id" border default-expand-all>
      <el-table-column prop="name" label="菜单名称" min-width="180" />
      <el-table-column prop="path" label="路由路径" min-width="180" />
      <el-table-column prop="component" label="组件路径" min-width="180" />
      <el-table-column prop="menuType" label="类型" width="100" />
      <el-table-column prop="permissionCode" label="权限标识" min-width="180" />
      <el-table-column prop="sort" label="排序" width="80" />
      <el-table-column label="操作" width="220" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:menu:create'" link @click="openCreate(row)">新增子级</el-button>
          <el-button v-permission="'system:menu:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button v-permission="'system:menu:delete'" link type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑菜单' : '新增菜单'" width="560px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="120px">
        <el-form-item label="上级菜单">
          <el-tree-select v-model="form.parentId" :data="tableData" clearable node-key="id" :props="{ label: 'name', children: 'children' }" class="full-width" />
        </el-form-item>
        <el-form-item label="菜单名称" prop="name"><el-input v-model="form.name" /></el-form-item>
        <el-form-item label="路由路径"><el-input v-model="form.path" /></el-form-item>
        <el-form-item label="组件路径"><el-input v-model="form.component" /></el-form-item>
        <el-form-item label="重定向"><el-input v-model="form.redirect" /></el-form-item>
        <el-form-item label="图标"><el-input v-model="form.icon" /></el-form-item>
        <el-form-item label="菜单类型" prop="menuType">
          <el-select v-model="form.menuType" class="full-width">
            <el-option label="目录" value="Directory" />
            <el-option label="菜单" value="Menu" />
            <el-option label="按钮" value="Button" />
          </el-select>
        </el-form-item>
        <el-form-item label="权限标识"><el-input v-model="form.permissionCode" /></el-form-item>
        <el-form-item label="排序"><el-input-number v-model="form.sort" :min="0" /></el-form-item>
        <el-form-item label="显示"><el-switch v-model="form.visible" /></el-form-item>
        <el-form-item label="缓存"><el-switch v-model="form.keepAlive" /></el-form-item>
      </el-form>
      <template #footer><el-button @click="dialogVisible = false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>
  </section>
</template>
