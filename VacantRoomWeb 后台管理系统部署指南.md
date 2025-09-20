# VacantRoomWeb 后台管理系统部署指南

## 系统要求
- 安装了 ASP.NET Core 运行时的 IIS 服务器
- DNS 配置权限（用于配置子域名）
- 已生成的管理员凭据

## 详细部署步骤

### 1. 生成管理员凭据
```bash
# 运行密码生成器
dotnet run --project GenerateAdminPassword.cs
# 或编译并运行 GenerateAdminPassword 工具
```

### 2. 更新配置文件
- 在 `appsettings.json` 中替换生成的哈希值和盐值
- 设置安全的 SecretKey 用于身份验证令牌

### 3. DNS 配置
- 添加 A 记录：`admin.origami7023.cn` → 服务器 IP 地址
- 验证 DNS 传播：`nslookup admin.origami7023.cn`

### 4. IIS 配置
1. 打开 IIS 管理器
2. 右键点击网站 → 编辑绑定
3. 添加新绑定：
   - 类型：http（或 https）
   - 主机名：`admin.origami7023.cn`
   - 端口：80（或 443）

### 5. SSL 证书配置（推荐）
- 为 `*.origami7023.cn` 或 `admin.origami7023.cn` 安装 SSL 证书
- 更新 IIS 绑定以使用 HTTPS

### 6. 构建和部署
```bash
dotnet publish -c Release -o ./publish
# 将 ./publish 内容复制到 IIS wwwroot 目录
```

### 7. 设置文件权限
- 确保 IIS_IUSRS 对 Logs 文件夹有读写权限
- 授予创建日志文件的权限

## 系统测试

### 基础功能测试
1. 访问 `https://origami7023.cn` → 应显示主站
2. 访问 `https://admin.origami7023.cn` → 应重定向到登录页
3. 访问 `https://origami7023.cn/admin` → 应返回 404

### 安全功能测试
1. **登录保护**：尝试 5 次错误密码 → IP 应被封禁
2. **频率限制**：快速发起请求 → 应触发 DDoS 保护
3. **面板锁定**：触发 10 次暴力破解 → 管理面板锁定

### 日志验证
- 检查 `Logs/access-YYYY-MM-DD.log` 文件是否创建
- 验证安全事件是否被记录
- 确认 IP 封禁功能正常工作

## 系统监控

### 关键日志位置
- 访问日志：`./Logs/access-*.log`
- IIS 日志：`C:\inetpub\logs\LogFiles\`
- 应用程序日志：事件查看器 → Windows 日志 → 应用程序

### 仪表板功能
- 实时连接数统计
- 安全事件监控
- 被封禁 IP 追踪
- 系统状态概览

## 故障排除

### 常见问题
1. **管理子域名无法访问**：检查 DNS 和 IIS 绑定
2. **正确密码登录失败**：验证 appsettings.json 中的凭据
3. **日志文件未创建**：检查 IIS_IUSRS 的文件夹权限
4. **安全中间件错误**：确保所有服务已在 Program.cs 中注册

### 日志分析
```bash
# 搜索安全事件
findstr "SECURITY_" Logs\access-*.log

# 检查被封禁的 IP
findstr "IP_BANNED" Logs\access-*.log

# 监控登录尝试
findstr "LOGIN" Logs\access-*.log
```

## 安全注意事项
- 更改默认管理员用户名
- 使用强密码（12+ 字符）
- 为管理子域名启用 HTTPS
- 定期监控安全日志
- 考虑为管理访问设置 IP 白名单

## 本地开发访问
### 开发环境配置
在开发环境中，可以通过修改 `DomainRoutingMiddleware.cs` 允许本地访问：
```csharp
// 在开发环境中允许 localhost 管理访问
if (host.Contains("localhost") && path.StartsWith("/admin"))
{
    // 允许在 localhost 上直接访问管理页面
    await _next(context);
    return;
}
```

然后访问：`https://localhost:端口号/admin/login`

## 邮件通知（未来功能）
系统包含邮件通知结构，用于：
- 暴力破解攻击警报
- DDoS 攻击尝试
- 管理面板锁定通知
- 系统安全警报

需要在 EmailService.cs 中配置 SMTP 实现。

## 系统架构说明
- **多层攻击防护**：暴力破解、DDoS、系统锁定
- **文件日志系统**：适用于 IIS 环境，无控制台依赖
- **IP 封禁机制**：自动过期和解封
- **安全认证**：加密 Cookie 和令牌验证
- **域名隔离**：子域名路由保护管理入口