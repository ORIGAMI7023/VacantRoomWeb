# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

这是一个 **ASP.NET Core 8.0 Blazor Server** 应用，用于校园教室空闲查询系统。支持奉贤、徐汇两个校区的教室查询，包含完善的安全防护和管理后台。

## 构建与运行

```bash
# 构建项目
dotnet build VacantRoomWeb.sln

# 运行开发服务器
dotnet run --project VacantRoomWeb/VacantRoomWeb.csproj

# 发布生产版本
dotnet publish VacantRoomWeb/VacantRoomWeb.csproj -c Release

# 生成管理员密码哈希（工具项目）
dotnet run --project GenerateAdminPassword/GenerateAdminPassword.csproj
```

## 核心架构

### 三层架构设计

```
Components (Blazor UI)
    ↓ 调用
Services (业务逻辑)
    ↓ 访问
Data (Excel 数据源)
```

### 关键服务层

所有服务通过依赖注入注册为 **Singleton**：

- **VacantRoomService**: 核心业务服务，处理教室查询逻辑
  - 使用 IMemoryCache 缓存 Excel 数据（24小时绝对过期，1小时滑动过期）
  - FileSystemWatcher 监控 `Data/schedule.xlsx` 变化并自动刷新缓存
  - 包含智能查询日志防止日志膨胀

- **SecurityService**: 多层安全防护
  - DDoS 防护：120次/分钟触发封禁15分钟
  - 暴力破解防护：5分钟内8次失败封禁30分钟
  - 系统锁定：10分钟内5个IP暴力破解锁定15分钟
  - 维护动态 IP 封禁列表（ConcurrentDictionary）

- **AdminAuthService**: 管理员认证
  - SHA256 + Salt 密码哈希
  - HMAC-SHA256 签名认证令牌
  - Cookie 存储（7天有效期）

- **EmailService**: 安全警报邮件通知
  - 集成 NotifyHub API
  - 15分钟邮件冷却时间防止轰炸
  - 支持 DDoS、暴力破解、系统锁定等安全事件通知

- **EnhancedLoggingService**: 日志系统
  - 按日期分片存储：`Logs/access-{yyyy-MM-dd}.log`
  - 内存缓存最近日志用于快速访问
  - 支持日志过滤、导出CSV、30天自动清理

- **ConfigurationService**: 配置管理
  - **环境变量优先于 appsettings.json**
  - 生产环境敏感配置通过 `web.config` 注入环境变量

### 中间件管道

1. **SecurityMiddleware**: 请求安全检查、速率限制、IP 封禁
2. **DomainRoutingMiddleware**: 域名路由（主域名、localhost）

### 数据模型

- Excel 数据源：`Data/schedule.xlsx`（课程表）
- 硬编码教室列表：A/B/C/D/E 楼（位于 VacantRoomService）

## 配置管理

### 配置优先级

```
环境变量 (web.config) > appsettings.json > User Secrets (开发)
```

### 关键配置

**web.config**（生产环境 - 最高优先级）：
- 管理员凭据：`ADMIN_USERNAME`, `ADMIN_PASSWORD_HASH`, `ADMIN_SALT`, `ADMIN_SECRET_KEY`
- 邮件配置：`EMAIL_API_URL`, `EMAIL_API_KEY`, `EMAIL_RECIPIENT`

**appsettings.json**（开发环境回退）：
- Security: DDoS/暴力破解阈值
- Email: 冷却时间、重试次数
- System: 刷新间隔、最大连接跟踪数

**User Secrets ID**: `f95f1e35-3762-4afe-a399-92a85704da9f`

## 路由约定

- `/` - 教室查询主页（VacantRoom.razor）
- `/admin/login` - 管理员登录
- `/admin` 或 `/admin/dashboard` - 管理后台
- `/admin/email-test` - 邮件测试页面

## 依赖注入顺序（Program.cs）

**必须遵循以下顺序**：

1. `HttpContextAccessor`（SecurityService 等依赖）
2. `MemoryCache`（VacantRoomService 依赖）
3. `ConfigurationService`（其他服务依赖其配置）
4. `HttpClient` + `EmailService`
5. 日志服务（`EnhancedLoggingService`）
6. 安全服务（`SecurityService`, `AdminAuthService`）
7. 业务服务（`VacantRoomService`）
8. 其他服务

## 重要约定

### 安全最佳实践

- 敏感配置必须通过环境变量注入，**不要硬编码**
- 管理员密码使用 `GenerateAdminPassword` 工具生成哈希和 Salt
- 所有认证令牌使用 HMAC-SHA256 签名防篡改
- Cookie 设置：`HttpOnly=true, Secure=true, SameSite=Strict`

### 性能优化

- Excel 数据必须缓存，避免每次查询都读取文件
- 日志记录使用智能频率控制，避免重复日志
- 静态资源请求（`/_framework/`, `/css/`, `/js/`）不记录日志

### 并发安全

- IP 封禁列表使用 `ConcurrentDictionary`
- Excel 缓存刷新使用 `lock` 保护临界区
- 日志写入使用 `lock` 保证线程安全

## Excel 数据结构

`Data/schedule.xlsx` 包含以下列：
- 校区（奉贤/徐汇）
- 星期
- 节次（1-2节、3-4节等）
- 周次（如 "1-18"、"单周1-18"）
- 教室（如 "A-101"）
- 课程名称
- 教师姓名

**重要**: Excel 文件变化会被 FileSystemWatcher 监控并自动刷新缓存。

## 部署架构

- **IIS 托管**: OutOfProcess 模式（AspNetCoreModuleV2）
- **反向代理**: 支持 Nginx（已配置 ForwardedHeaders 获取真实客户端 IP）
- **环境变量注入**: 通过 web.config 的 `<environmentVariables>` 配置

## 常见修改场景

### 添加新教学楼

在 `VacantRoomService.cs` 中修改硬编码的教室列表：
```csharp
private static readonly List<string> _allRoomsByBuilding = new()
{
    // 添加新楼栋的教室...
};
```

### 修改安全阈值

编辑 `appsettings.json` 的 `Security` 配置节，或在生产环境通过环境变量覆盖。

### 添加新的邮件通知类型

在 `EmailService.cs` 中添加新的通知方法，遵循现有的模板格式和冷却时间机制。

## 技术栈版本

- .NET: 8.0
- Blazor: Server 模式（InteractiveServer 渲染）
- ClosedXML: 0.105.0
- 部署: IIS + ASP.NET Core Module V2
