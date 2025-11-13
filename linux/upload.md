# VacantRoom 部署命令

## 1. 上传应用文件

```bash
scp -r D:\Programing\C#\VacantRoomWeb\VacantRoomWeb\bin\Release\net8.0\publish\* root@downf.cn:/var/www/vacantroomweb/
```

## 1.1 设置权限（如果 logs 被覆盖）

sudo chown -R nginx:nginx /var/www/vacantroomweb/logs

## 2. 上传配置文件（直接覆盖）

```bash
# nginx 配置（直接覆盖原文件）
scp D:\Programing\C#\VacantRoomWeb\linux\v.downf.cn.conf root@downf.cn:/etc/nginx/conf.d/v.downf.cn.conf

# 上传 systemd 服务配置
scp D:\Programing\C#\VacantRoomWeb\linux\vacantroomweb.service root@downf.cn:/etc/systemd/system/vacantroomweb.service
```



## 3. 配置应用（在服务器上执行）

```bash
# 重载 nginx
sudo nginx -t && sudo systemctl reload nginx

# 重启服务
sudo systemctl daemon-reload
sudo systemctl restart vacantroomweb
sudo systemctl status vacantroomweb
```