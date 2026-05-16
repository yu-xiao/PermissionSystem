<template>
  <section class="dashboard-page">
    <div class="dashboard-page__heading">
      <h1>仪表盘</h1>
      <p>欢迎使用权限管理系统。</p>
    </div>

    <el-row :gutter="16">
      <el-col :xs="24" :sm="12" :lg="8">
        <el-card shadow="never">
          <template #header>系统状态</template>
          <el-tag type="success">运行中</el-tag>
        </el-card>
      </el-col>
      <el-col :xs="24" :sm="12" :lg="8">
        <el-card shadow="never">
          <template #header>认证服务</template>
          <el-tag>OpenIddict</el-tag>
        </el-card>
      </el-col>
      <el-col :xs="24" :sm="12" :lg="8">
        <el-card shadow="never">
          <template #header>授权模型</template>
          <el-tag type="warning">角色权限控制</el-tag>
        </el-card>
      </el-col>
    </el-row>
  </section>
</template>

<style scoped>
.dashboard-page__heading {
  margin-bottom: 20px;
}

.dashboard-page__heading h1 {
  margin: 0;
  color: #111827;
  font-size: 24px;
  line-height: 1.25;
}

.dashboard-page__heading p {
  margin: 6px 0 0;
  color: #64748b;
}
</style>
