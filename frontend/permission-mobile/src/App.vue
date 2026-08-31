<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterView } from 'vue-router'

onMounted(() => {
  // Keep the viewport stable on mobile browsers and expose the app shell to PWA tooling.
  document.documentElement.style.setProperty('--mobile-vh', `${window.innerHeight}px`)
})
</script>

<template><RouterView /></template>
