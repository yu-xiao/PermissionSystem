<script setup lang="ts">
import { FullScreen, Refresh, Setting } from '@element-plus/icons-vue'
import { computed, ref } from 'vue'

export interface TableToolbarColumn {
  label: string
  prop: string
  visible?: boolean
}

const props = withDefaults(
  defineProps<{
    columns?: TableToolbarColumn[]
    fullscreenTarget?: string
  }>(),
  {
    columns: () => [],
    fullscreenTarget: '',
  },
)

const emit = defineEmits<{
  refresh: []
  densityChange: [value: string]
  columnChange: [columns: TableToolbarColumn[]]
}>()

const density = ref('default')
const selectedColumns = ref(props.columns.filter((item) => item.visible !== false).map((item) => item.prop))
const hasColumns = computed(() => props.columns.length > 0)

function handleDensityChange(value: string) {
  emit('densityChange', value)
}

function handleColumnChange() {
  emit(
    'columnChange',
    props.columns.map((item) => ({
      ...item,
      visible: selectedColumns.value.includes(item.prop),
    })),
  )
}

async function toggleFullscreen() {
  const target = props.fullscreenTarget ? document.querySelector<HTMLElement>(props.fullscreenTarget) : undefined
  const element = target ?? document.documentElement

  if (document.fullscreenElement) {
    await document.exitFullscreen()
    return
  }

  await element.requestFullscreen()
}
</script>

<template>
  <div class="table-toolbar">
    <slot />
    <div class="table-toolbar__spacer" />
    <el-tooltip content="刷新" placement="top">
      <el-button text :icon="Refresh" @click="emit('refresh')" />
    </el-tooltip>
    <el-segmented
      v-model="density"
      size="small"
      :options="[
        { label: '默认', value: 'default' },
        { label: '中等', value: 'middle' },
        { label: '紧凑', value: 'small' },
      ]"
      @change="handleDensityChange"
    />
    <el-popover v-if="hasColumns" placement="bottom-end" width="180" trigger="click">
      <template #reference>
        <el-button text :icon="Setting" />
      </template>
      <el-checkbox-group v-model="selectedColumns" class="table-toolbar__columns" @change="handleColumnChange">
        <el-checkbox v-for="column in columns" :key="column.prop" :label="column.prop">
          {{ column.label }}
        </el-checkbox>
      </el-checkbox-group>
    </el-popover>
    <el-tooltip content="全屏表格" placement="top">
      <el-button text :icon="FullScreen" @click="toggleFullscreen" />
    </el-tooltip>
  </div>
</template>
