<script setup lang="ts">
defineOptions({
  name: 'SystemFile',
})

import { Download, Delete } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import UploadFile from '../../../components/UploadFile.vue'
import { deleteFile, downloadFile, getFiles, type FileResourceItem } from '../../../api/files'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

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
  await ElMessageBox.confirm(`确认删除文件 ${row.originalName}？`, '确认删除')
  await deleteFile(row.id)
  ElMessage.success('删除成功')
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
  <PageContainer title="文件管理" description="查看文件资源、存储位置、业务关联并执行下载或删除。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="名称 / MD5 / 业务" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.businessType" clearable placeholder="业务类型" style="width: 150px" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.businessId" clearable placeholder="业务ID" style="width: 220px" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.extension" clearable placeholder=".pdf" style="width: 100px" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.storageProvider" clearable placeholder="存储" style="width: 130px">
          <el-option label="本地" value="Local" />
          <el-option label="MinIO" value="Minio" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:file:view'" type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
        <UploadFile v-permission="'system:file:upload'" @uploaded="onUploaded" />
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="originalName" label="原始文件名" min-width="220" show-overflow-tooltip />
      <el-table-column prop="extension" label="扩展名" width="80" />
      <el-table-column prop="contentType" label="内容类型" min-width="160" show-overflow-tooltip />
      <el-table-column prop="size" label="大小" width="110">
        <template #default="{ row }">{{ formatSize(row.size) }}</template>
      </el-table-column>
      <el-table-column prop="storageProvider" label="存储" width="100">
        <template #default="{ row }">{{ $displayText(row.storageProvider) }}</template>
      </el-table-column>
      <el-table-column prop="bucketName" label="存储桶" width="120" show-overflow-tooltip />
      <el-table-column prop="businessType" label="业务类型" width="140" show-overflow-tooltip />
      <el-table-column prop="businessId" label="业务ID" min-width="220" show-overflow-tooltip />
      <el-table-column prop="md5" label="MD5" min-width="220" show-overflow-tooltip />
      <el-table-column prop="createdAt" label="创建时间" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="{ row }">
          <el-button
            v-permission="'system:file:download'"
            :icon="Download"
            link
            type="primary"
            @click="download(row)"
          >
            下载
          </el-button>
          <el-button
            v-permission="'system:file:delete'"
            :icon="Delete"
            link
            type="danger"
            @click="remove(row)"
          >
            删除
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
  </PageContainer>
</template>
