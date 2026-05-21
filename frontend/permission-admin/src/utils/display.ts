const textMap: Record<string, string> = {
  Boolean: '布尔',
  Disabled: '禁用',
  Enabled: '启用',
  Failed: '失败',
  Female: '女',
  Healthy: '健康',
  High: '高',
  Json: 'JSON',
  Local: '本地',
  Low: '低',
  Male: '男',
  Minio: 'MinIO',
  Normal: '普通',
  Number: '数字',
  Online: '在线',
  Pending: '待发送',
  Plain: '明文',
  Processed: '已处理',
  Processing: '处理中',
  Published: '已发布',
  Revoked: '已撤销',
  Security: '安全',
  Skipped: '已跳过',
  String: '字符串',
  Succeeded: '成功',
  System: '系统',
  Task: '任务',
  Approval: '审批',
  Button: '按钮',
  Directory: '目录',
  Hangfire: 'Hangfire',
  Menu: '菜单',
  ScheduledTask: '定时任务',
  Unknown: '未知',
  Unhealthy: '不健康',
  Degraded: '降级',
}

export function displayText(value?: string | null) {
  if (!value) {
    return '-'
  }

  return textMap[value] ?? value
}

export function yesNo(value?: boolean | null) {
  return value ? '是' : '否'
}
