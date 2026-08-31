<script setup lang="ts">
export interface AttachmentItem {
  id: string
  name?: string
  fileName?: string
  size?: number
  contentType?: string
  downloadUrl?: string
  url?: string
}

withDefaults(defineProps<{ items: AttachmentItem[]; uploading?: boolean; canUpload?: boolean; canRemove?: boolean }>(), { canUpload: true, canRemove: true })
const emit = defineEmits<{ upload: [file: File]; remove: [id: string]; open: [item: AttachmentItem] }>()

function formatSize(size?: number) {
  if (!size) return ''
  if (size < 1024) return `${size} B`
  if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`
  return `${(size / (1024 * 1024)).toFixed(1)} MB`
}

function chooseFile(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (file) emit('upload', file)
  input.value = ''
}
</script>

<template>
  <div class="attachment-list">
    <div v-if="items.length" class="attachment-list__items">
      <div v-for="item in items" :key="item.id" class="attachment-item">
        <button class="attachment-item__main" type="button" @click="emit('open', item)">
          <span class="attachment-item__icon" aria-hidden="true">□</span>
          <span class="attachment-item__name">{{ item.name || item.fileName || '附件' }}<small v-if="item.size">{{ formatSize(item.size) }}</small></span>
        </button>
        <button v-if="canRemove" class="icon-button attachment-item__remove" type="button" aria-label="删除附件" title="删除附件" @click="emit('remove', item.id)">×</button>
      </div>
    </div>
    <div v-else class="attachment-list__empty">暂无附件</div>
    <label v-if="canUpload" class="button button--secondary attachment-upload" :class="{ 'attachment-upload--loading': uploading }">
      <span aria-hidden="true">＋</span>{{ uploading ? '上传中…' : '添加附件' }}
      <input type="file" :disabled="uploading" @change="chooseFile" />
    </label>
  </div>
</template>

<style scoped>
.attachment-list { display: grid; gap: 10px; }
.attachment-list__items { display: grid; gap: 7px; }
.attachment-item { display: flex; align-items: center; gap: 4px; min-height: 44px; padding: 3px 3px 3px 9px; border: 1px solid var(--mobile-border); border-radius: 8px; }
.attachment-item__main { display: flex; min-width: 0; flex: 1; align-items: center; gap: 8px; padding: 6px 0; border: 0; color: var(--mobile-text); background: transparent; text-align: left; }
.attachment-item__icon { color: var(--mobile-primary); font-size: 18px; }
.attachment-item__name { min-width: 0; overflow: hidden; font-size: 13px; text-overflow: ellipsis; white-space: nowrap; }
.attachment-item__name small { display: block; margin-top: 2px; color: var(--mobile-text-muted); font-size: 10px; }
.attachment-item__remove { width: 33px; height: 33px; font-size: 18px; }
.attachment-list__empty { padding: 8px 0; color: var(--mobile-text-muted); font-size: 12px; }
.attachment-upload { width: max-content; position: relative; }
.attachment-upload input { position: absolute; width: 1px; height: 1px; overflow: hidden; opacity: 0; }
</style>
