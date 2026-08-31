<script setup lang="ts">
import { useRouter } from 'vue-router'
const router = useRouter()
</script>

<template><section class="error-page surface"><div class="error-page__code">404</div><h2>页面不存在</h2><p>请检查链接是否正确。</p><button class="button button--primary" type="button" @click="router.replace('/home')">返回工作台</button></section></template>

<style scoped>
.error-page { display: grid; margin-top: 18vh; padding: 30px 18px; place-items: center; text-align: center; }
.error-page__code { color: var(--mobile-primary); font-size: 48px; font-weight: 800; line-height: 1; }
.error-page h2 { margin: 15px 0 7px; font-size: 19px; }
.error-page p { margin: 0 0 20px; color: var(--mobile-text-secondary); font-size: 13px; }
</style>
