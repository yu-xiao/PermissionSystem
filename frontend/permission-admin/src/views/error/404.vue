<template>
  <section class="error-page">
    <div class="error-page__code">404</div>
    <h1>页面不存在</h1>
    <p>你访问的页面不存在，或已被移动到其他位置。</p>
    <el-button type="primary" @click="$router.replace('/dashboard')">返回首页</el-button>
  </section>
</template>
