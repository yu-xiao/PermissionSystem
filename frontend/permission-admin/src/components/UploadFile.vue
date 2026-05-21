<script setup lang="ts">
import { Upload } from '@element-plus/icons-vue'
import { ElMessage, type UploadRequestOptions } from 'element-plus'
import { ref } from 'vue'
import { uploadFile, type FileResourceItem } from '../api/files'

const props = withDefaults(
  defineProps<{
    businessType?: string
    businessId?: string
    buttonText?: string
  }>(),
  {
    buttonText: 'Upload',
  },
)

const emit = defineEmits<{
  uploaded: [file: FileResourceItem]
}>()

const uploading = ref(false)

async function upload(options: UploadRequestOptions) {
  uploading.value = true
  try {
    const file = await uploadFile(options.file, props.businessType, props.businessId)
    ElMessage.success('Uploaded successfully')
    emit('uploaded', file)
    options.onSuccess(file)
  } catch (error) {
    ;(options.onError as (error: unknown) => void)(error)
  } finally {
    uploading.value = false
  }
}
</script>

<template>
  <el-upload :http-request="upload" :show-file-list="false">
    <el-button :icon="Upload" :loading="uploading">
      {{ buttonText }}
    </el-button>
  </el-upload>
</template>
