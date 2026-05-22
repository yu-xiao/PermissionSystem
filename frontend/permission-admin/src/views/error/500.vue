<template>
  <section class="error-page">
    <div class="error-page__code">500</div>
    <h1>系统异常</h1>
    <p>页面加载时发生异常，请稍后重试或联系系统管理员。</p>
    <el-button type="primary" @click="$router.replace('/dashboard')">返回首页</el-button>
  </section>
</template>
