<script setup lang="ts">
import { Download, Delete } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import UploadFile from '../../../components/UploadFile.vue'
import { deleteFile, downloadFile, getFiles, type FileResourceItem } from '../../../api/files'

const loading = ref(false)
const tableData = ref<FileResourceItem[]>([])
const total = ref(0)

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  businessType: '',
  businessId: '',
  storageProvider: '',
  extension: '',
})

async function loadData() {
  loading.value = true
  try {
    const result = await getFiles({
      ...query,
      businessId: query.businessId || undefined,
    })
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function remove(row: FileResourceItem) {
  await ElMessageBox.confirm(`Delete file ${row.originalName}?`, 'Confirm delete')
  await deleteFile(row.id)
  ElMessage.success('Deleted successfully')
  await loadData()
}

async function download(row: FileResourceItem) {
  const response = await downloadFile(row.id)
  const url = URL.createObjectURL(response.data)
  const link = document.createElement('a')
  link.href = url
  link.download = row.originalName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    businessType: '',
    businessId: '',
    storageProvider: '',
    extension: '',
  })
  loadData()
}

function onUploaded() {
  query.pageIndex = 1
  loadData()
}

function formatSize(size: number) {
  if (size < 1024) {
    return `${size} B`
  }

  if (size < 1024 * 1024) {
    return `${(size / 1024).toFixed(1)} KB`
  }

  return `${(size / 1024 / 1024).toFixed(1)} MB`
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadData()
</script>

<template>
  <section class="page">
    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="Name / MD5 / business" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.businessType" clearable placeholder="Business type" style="width: 150px" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.businessId" clearable placeholder="Business ID" style="width: 220px" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.extension" clearable placeholder=".pdf" style="width: 100px" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.storageProvider" clearable placeholder="Storage" style="width: 130px">
          <el-option label="Local" value="Local" />
          <el-option label="Minio" value="Minio" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:file:view'" type="primary" @click="loadData">Search</el-button>
        <el-button @click="resetQuery">Reset</el-button>
        <UploadFile v-permission="'system:file:upload'" @uploaded="onUploaded" />
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="originalName" label="Original Name" min-width="220" show-overflow-tooltip />
      <el-table-column prop="extension" label="Ext" width="80" />
      <el-table-column prop="contentType" label="Content Type" min-width="160" show-overflow-tooltip />
      <el-table-column prop="size" label="Size" width="110">
        <template #default="{ row }">{{ formatSize(row.size) }}</template>
      </el-table-column>
      <el-table-column prop="storageProvider" label="Storage" width="100" />
      <el-table-column prop="bucketName" label="Bucket" width="120" show-overflow-tooltip />
      <el-table-column prop="businessType" label="Business Type" width="140" show-overflow-tooltip />
      <el-table-column prop="businessId" label="Business ID" min-width="220" show-overflow-tooltip />
      <el-table-column prop="md5" label="MD5" min-width="220" show-overflow-tooltip />
      <el-table-column prop="createdAt" label="Created At" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="Actions" width="150" fixed="right">
        <template #default="{ row }">
          <el-button
            v-permission="'system:file:download'"
            :icon="Download"
            link
            type="primary"
            @click="download(row)"
          >
            Download
          </el-button>
          <el-button
            v-permission="'system:file:delete'"
            :icon="Delete"
            link
            type="danger"
            @click="remove(row)"
          >
            Delete
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-model:current-page="query.pageIndex"
      v-model:page-size="query.pageSize"
      class="pager"
      background
      layout="total, sizes, prev, pager, next"
      :total="total"
      @change="loadData"
    />
  </section>
</template>
