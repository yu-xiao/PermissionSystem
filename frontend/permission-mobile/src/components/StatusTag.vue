<script setup lang="ts">
withDefaults(defineProps<{ label: string; tone?: 'neutral' | 'primary' | 'success' | 'warning' | 'danger' }>(), { tone: 'neutral' })
</script>

<template><span class="tag" :class="`tag--${tone}`">{{ label }}</span></template>
