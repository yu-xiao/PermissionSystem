<script setup lang="ts">
withDefaults(
  defineProps<{
    title?: string
    description?: string
    bordered?: boolean
  }>(),
  {
    title: '',
    description: '',
    bordered: true,
  },
)
</script>

<template>
  <section class="page-container" :class="{ 'is-borderless': !bordered }">
    <header v-if="title || description || $slots.actions" class="page-container__header">
      <div class="page-container__heading">
        <h1 v-if="title">{{ title }}</h1>
        <p v-if="description">{{ description }}</p>
      </div>
      <div v-if="$slots.actions" class="page-container__actions">
        <slot name="actions" />
      </div>
    </header>

    <div v-if="$slots.query" class="page-container__query">
      <slot name="query" />
    </div>

    <div class="page-container__body">
      <slot />
    </div>
  </section>
</template>
