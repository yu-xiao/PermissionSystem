# EF Core Migration Guide

本文档说明在修改领域实体、EF Core 配置或 `AppDbContext` 后，如何正确生成、检查和验证迁移文件。

## 适用范围

需要生成迁移的常见变更：

- 新增、删除实体或 `DbSet`
- 新增、删除、重命名实体属性
- 修改字段类型、长度、是否必填、默认值
- 新增、删除、修改索引或唯一约束
- 修改表名、列名、外键关系
- 修改全局过滤、审计字段相关的实体映射

只修改 DTO、Controller、Application Service、前端代码时，通常不需要生成迁移。

## 推荐流程

1. 修改模型

   优先修改以下位置：

   - `PermissionSystem.Domain/Entities`
   - `PermissionSystem.Infrastructure/Configurations`
   - `PermissionSystem.Infrastructure/Data/AppDbContext.cs`

2. 生成迁移

   在 `backend` 目录执行：

   ```powershell
   dotnet ef migrations add AddYourChangeName `
     --project .\PermissionSystem.Infrastructure\PermissionSystem.Infrastructure.csproj `
     --startup-project .\PermissionSystem.Api\PermissionSystem.Api.csproj `
     --context AppDbContext `
     --output-dir Data\Migrations
   ```

   迁移名称应表达业务含义，例如：

   ```powershell
   dotnet ef migrations add AddUserIsBuiltin `
     --project .\PermissionSystem.Infrastructure\PermissionSystem.Infrastructure.csproj `
     --startup-project .\PermissionSystem.Api\PermissionSystem.Api.csproj `
     --context AppDbContext `
     --output-dir Data\Migrations
   ```

3. 如果提示找不到 `dotnet ef`

   安装或更新 EF Core CLI 工具：

   ```powershell
   dotnet tool install --global dotnet-ef --version 10.*
   ```

   如果已经安装过：

   ```powershell
   dotnet tool update --global dotnet-ef --version 10.*
   ```

4. 检查生成结果

   每次迁移至少应包含：

   - `yyyyMMddHHmmss_MigrationName.cs`
   - `yyyyMMddHHmmss_MigrationName.Designer.cs`
   - 更新后的 `AppDbContextModelSnapshot.cs`

   不要只提交 `.cs` 主迁移文件而漏掉 `.Designer.cs` 或快照文件。

5. 检查迁移内容

   重点检查：

   - `Up` 是否只包含本次模型变更
   - `Down` 是否能撤销本次变更
   - 是否误删表、误删列、误改已有数据
   - 字段长度、默认值、nullable 是否符合业务预期
   - 索引、唯一约束、外键删除行为是否正确
   - 涉及历史数据时是否需要 `migrationBuilder.Sql(...)` 做数据修复

6. 构建验证

   ```powershell
   dotnet build .\PermissionSystem.sln
   ```

7. 启动验证

   本项目在 `Development` 和 `Docker` 环境启动时会执行：

   ```csharp
   await dbContext.Database.MigrateAsync();
   ```

   因此本地可以直接运行：

   ```powershell
   .\start-backend.bat
   ```

   或：

   ```powershell
   dotnet run --project .\PermissionSystem.Api\PermissionSystem.Api.csproj --launch-profile http
   ```

   启动成功后检查：

   - `http://localhost:5264/swagger/index.html`
   - `http://localhost:5264/health`

## 重要规则

- 优先使用 `dotnet ef migrations add` 自动生成迁移，不要手写完整迁移。
- 不要直接修改已经被团队或环境使用过的历史迁移，除非明确是在修复尚未合并、尚未发布的本地迁移。
- 不要手工修改数据库表结构来代替迁移。
- 不要删除 `AppDbContextModelSnapshot.cs`。
- 不要只改实体不生成迁移，否则运行时可能出现 `列名无效`、`对象名无效` 等数据库错误。
- 不要把敏感配置写入迁移文件。
- 涉及删除列、缩短字段长度、改字段类型时，要先评估历史数据是否会丢失。

## 手写迁移的最低要求

只有在 EF 自动生成结果无法表达业务需求时，才允许手写或补充迁移逻辑。手写迁移必须包含 EF Core 迁移元数据。

文件示例：

```csharp
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260608120000_AddExampleColumn")]
public partial class AddExampleColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Example",
            table: "Users",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Example",
            table: "Users");
    }
}
```

如果缺少 `[Migration("...")]`，EF Core 可能发现不了该迁移，启动时会误判数据库已经是最新状态。

## 常见问题

### 启动时报 `列名 'Xxx' 无效`

通常原因：

- 实体新增了属性，但没有生成迁移
- 迁移文件没有执行到目标数据库
- 手写迁移缺少 `[Migration]` 元数据
- 迁移历史表 `__EFMigrationsHistory` 与实际表结构不一致

处理方式：

1. 检查是否存在对应迁移文件。
2. 检查迁移是否包含 `.Designer.cs` 和快照更新。
3. 启动 API，让 `MigrateAsync()` 执行迁移。
4. 如果数据库历史状态异常，先备份数据库，再评估是否需要补救迁移。

### 生成迁移后内容为空

通常表示 EF Core 当前模型快照与代码模型一致。可能原因：

- 模型没有真正变化
- 只改了非持久化 DTO
- 配置没有被 `ApplyConfigurationsFromAssembly` 扫描到
- 上一次已经生成过迁移但没有注意到

### 想改已经生成过的迁移

如果迁移还没有提交、没有被其他环境执行，可以删除后重新生成：

```powershell
dotnet ef migrations remove `
  --project .\PermissionSystem.Infrastructure\PermissionSystem.Infrastructure.csproj `
  --startup-project .\PermissionSystem.Api\PermissionSystem.Api.csproj `
  --context AppDbContext
```

如果迁移已经提交或已经被数据库执行，应新增一个修正迁移，不要修改历史迁移。

## 提交前检查清单

- 已生成迁移文件和 Designer 文件
- `AppDbContextModelSnapshot.cs` 已更新
- `dotnet build .\PermissionSystem.sln` 通过
- 本地启动 API 成功
- Swagger 或 Health 接口可访问
- 没有提交本地连接串、密码、Token 等敏感信息
- 没有混入无关文件改动
