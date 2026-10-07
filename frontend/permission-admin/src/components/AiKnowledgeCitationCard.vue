<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { getKnowledgeSource, type AiKnowledgeHit } from '../api/aiKnowledge'

const props = defineProps<{ hits: AiKnowledgeHit[] }>()
const source = ref<AiKnowledgeHit>()
const loading = ref(false)
const unavailable = ref(false)
let generation = 0
function clear() {
  generation++
  source.value = undefined
  loading.value = false
  unavailable.value = false
}
watch(() => props.hits, clear)
onBeforeUnmount(clear)
async function open(hit: AiKnowledgeHit) {
  clear()
  const requestId = generation
  loading.value = true
  try {
    const result = await getKnowledgeSource(hit.reference)
    if (requestId === generation) source.value = result
  } catch {
    if (requestId === generation) unavailable.value = true
  } finally {
    if (requestId === generation) loading.value = false
  }
}
</script>

<template>
  <section aria-label="文档知识引用" :aria-busy="loading">
    <p v-if="!hits.length">未找到可靠依据，请调整关键词重新查询。</p>
    <article
      v-for="hit in unavailable ? [] : hits"
      :key="hit.reference.chunkId"
      class="knowledge-hit"
    >
      <strong>{{ hit.title }} · v{{ hit.versionNumber }}</strong>
      <p>
        原文第 {{ hit.startLine }}–{{ hit.endLine }} 行；有效期 {{ hit.validFrom }} 至
        {{ hit.validUntil }}
      </p>
      <pre>{{ hit.content }}</pre>
      <el-button :loading="loading" @click="open(hit)">重新核验来源</el-button>
    </article>
    <p v-if="unavailable" role="status">来源已不可用，请重新查询。</p>
    <article v-if="source" class="knowledge-hit" aria-label="已核验源片段">
      <strong>已核验来源：{{ source.title }} · v{{ source.versionNumber }}</strong>
      <pre>{{ source.content }}</pre>
      <el-button @click="clear">关闭来源</el-button>
    </article>
  </section>
</template>

<style scoped>
.knowledge-hit {
  padding: 12px 0;
  border-bottom: 1px solid var(--el-border-color-light);
}
pre {
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  font: inherit;
}
p {
  color: var(--el-text-color-secondary);
}
</style>
