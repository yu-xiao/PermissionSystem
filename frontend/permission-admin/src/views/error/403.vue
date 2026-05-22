<template>
  <section class="error-page">
    <div class="error-page__code">403</div>
    <h1>暂无访问权限</h1>
    <p>当前账号没有访问该页面的权限，请联系管理员开通授权。</p>
    <el-button type="primary" @click="$router.replace('/dashboard')">返回首页</el-button>
  </section>
</template>
