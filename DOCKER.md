# FeeQuery Docker 部署指南

本文档介绍如何使用 Docker 部署 FeeQuery 应用程序。

## 📋 前置要求

- Docker Engine 20.10+ 或 Docker Desktop
- Docker Compose 2.0+（可选，用于 docker-compose 方式部署）
- 至少 2GB 可用磁盘空间

## 🚀 快速开始

### 方式一：使用自动化脚本（推荐）

**Windows 用户：**
```bash
docker-build-and-run.bat
```

**Linux/Mac 用户：**
```bash
chmod +x docker-build-and-run.sh
./docker-build-and-run.sh
```

脚本会自动完成构建、清理旧容器、启动新容器等操作。

### 方式二：使用 Docker Compose

```bash
# 构建并启动
docker-compose up -d

# 查看日志
docker-compose logs -f

# 停止服务
docker-compose down
```

### 方式三：手动 Docker 命令

```bash
# 1. 构建镜像
docker build -t feequery:latest .

# 2. 创建数据目录（持久化数据库）
mkdir -p ./data

# 3. 运行容器
docker run -d \
  --name feequery-web \
  -p 5000:80 \
  -v "$(pwd)/data:/app/data" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  --restart unless-stopped \
  feequery:latest

# 4. 访问应用
# 打开浏览器访问 http://localhost:5000
```

## 🔧 配置说明

### 数据目录权限（Windows 注意事项）

**Windows 用户注意：**
- 数据目录使用相对路径 `./data`，自动创建在项目根目录
- 确保 Docker Desktop 有权限访问项目目录
- 在 Docker Desktop 设置中，确认 `Settings -> Resources -> File Sharing` 已包含项目所在驱动器

**Linux/Mac 用户：**
- 如遇权限问题，可能需要调整目录权限：
```bash
chmod -R 755 ./data
```

### 端口映射

默认映射主机端口 `5000` 到容器端口 `80`。如需修改：

```bash
# 使用其他端口（例如 8080）
docker run -d --name feequery-web -p 8080:80 ...
```

或修改 `docker-compose.yml` 中的 `ports` 配置。

### 数据持久化

SQLite 数据库存储在项目根目录的 `./data` 目录中，容器重启或重建不会丢失数据。

**数据目录结构：**
```
FeeQuery/
├── data/
│   ├── feequery.db          # SQLite 数据库文件
│   ├── feequery.db-shm      # 共享内存文件
│   └── feequery.db-wal      # Write-Ahead Log
├── docker-compose.yml
└── Dockerfile
```

**备份数据库：**
```bash
# 方式1：直接复制本地文件（推荐）
cp -r ./data ./backup/data-$(date +%Y%m%d-%H%M%S)

# 方式2：从容器复制
docker cp feequery-web:/app/data/feequery.db ./backup/

# 方式3：压缩备份
tar czf feequery-backup-$(date +%Y%m%d-%H%M%S).tar.gz ./data
```

**恢复数据库：**
```bash
# 方式1：直接复制本地文件（推荐）
# 停止容器
docker stop feequery-web
# 恢复数据
rm -rf ./data
cp -r ./backup/data-20241211-120000 ./data
# 启动容器
docker start feequery-web

# 方式2：从备份文件解压
tar xzf feequery-backup-20241211-120000.tar.gz

# 方式3：从容器复制
docker stop feequery-web
docker cp ./backup/feequery.db feequery-web:/app/data/
docker start feequery-web
```

**迁移数据：**
```bash
# 将数据移动到其他位置
docker stop feequery-web
mv ./data /path/to/new/location/data
# 修改 docker-compose.yml 中的卷挂载路径
# volumes:
#   - /path/to/new/location/data:/app/data
docker start feequery-web
```

### 环境变量配置

可以通过环境变量自定义配置：

```bash
docker run -d \
  --name feequery-web \
  -p 5000:80 \
  -v "$(pwd)/data:/app/data" \
  -e ConnectionStrings__DefaultConnection="Data Source=/app/data/feequery.db" \
  -e DatabaseProvider=Sqlite \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Logging__LogLevel__Default=Information \
  -e TZ=Asia/Shanghai \
  feequery:latest
```

### 使用外部配置文件

如果需要更复杂的配置，可以挂载 `appsettings.Production.json`：

1. 创建配置文件：
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/app/data/feequery.db"
  },
  "DatabaseProvider": "Sqlite",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

2. 挂载到容器：
```bash
docker run -d \
  --name feequery-web \
  -p 5000:80 \
  -v "$(pwd)/data:/app/data" \
  -v "$(pwd)/appsettings.Production.json:/app/appsettings.Production.json:ro" \
  feequery:latest
```

## 🔄 切换到其他数据库

### 使用 PostgreSQL

1. 修改 `docker-compose.yml`，添加 PostgreSQL 服务：

```yaml
services:
  postgres:
    image: postgres:16
    container_name: feequery-postgres
    environment:
      POSTGRES_DB: feequery
      POSTGRES_USER: feequery
      POSTGRES_PASSWORD: your_password
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - feequery-network

  feequery-web:
    # ... 其他配置
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=feequery;Username=feequery;Password=your_password
      - DatabaseProvider=PostgreSQL
    depends_on:
      - postgres

volumes:
  postgres-data:
```

### 使用 MySQL

```yaml
services:
  mysql:
    image: mysql:8.0
    container_name: feequery-mysql
    environment:
      MYSQL_DATABASE: feequery
      MYSQL_USER: feequery
      MYSQL_PASSWORD: your_password
      MYSQL_ROOT_PASSWORD: root_password
    volumes:
      - mysql-data:/var/lib/mysql
    networks:
      - feequery-network

  feequery-web:
    # ... 其他配置
    environment:
      - ConnectionStrings__DefaultConnection=Server=mysql;Database=feequery;User=feequery;Password=your_password
      - DatabaseProvider=MySQL
    depends_on:
      - mysql

volumes:
  mysql-data:
```

## 📊 常用运维命令

### 容器管理

```bash
# 查看容器状态
docker ps -a

# 查看实时日志
docker logs -f feequery-web

# 查看最近 100 行日志
docker logs --tail 100 feequery-web

# 进入容器内部（调试用）
docker exec -it feequery-web /bin/bash

# 重启容器
docker restart feequery-web

# 停止容器
docker stop feequery-web

# 启动容器
docker start feequery-web

# 删除容器
docker rm -f feequery-web
```

### 镜像管理

```bash
# 查看镜像
docker images | grep feequery

# 删除镜像
docker rmi feequery:latest

# 清理未使用的镜像
docker image prune -a
```

### 性能监控

```bash
# 查看容器资源使用情况
docker stats feequery-web

# 查看容器详细信息
docker inspect feequery-web
```

## 🐛 故障排查

### 容器启动失败

1. 查看容器日志：
```bash
docker logs feequery-web
```

2. 检查端口是否被占用：
```bash
# Windows
netstat -ano | findstr :5000

# Linux/Mac
lsof -i :5000
```

3. 检查数据卷权限：
```bash
docker exec -it feequery-web ls -la /app/data
```

### 数据库连接失败

1. 确认数据库文件存在：
```bash
docker exec -it feequery-web ls -la /app/data/feequery.db
```

2. 检查环境变量配置：
```bash
docker exec -it feequery-web env | grep ConnectionStrings
```

3. 手动运行数据库迁移：
```bash
docker exec -it feequery-web dotnet ef database update
```

### 应用无法访问

1. 检查容器是否运行：
```bash
docker ps | grep feequery-web
```

2. 检查端口映射：
```bash
docker port feequery-web
```

3. 测试容器内部应用：
```bash
docker exec -it feequery-web curl http://localhost:80
```

## 🔐 安全建议

1. **修改默认端口**：避免使用默认的 5000 端口
2. **使用 HTTPS**：生产环境建议配置反向代理（Nginx/Traefik）并启用 HTTPS
3. **限制容器权限**：避免使用 `--privileged` 标志
4. **定期更新**：及时更新基础镜像和依赖包
5. **环境变量保护**：敏感信息使用 Docker Secrets 或环境变量文件
6. **网络隔离**：使用自定义网络限制容器间通信

## 📝 生产环境部署建议

### 使用反向代理（Nginx）

```nginx
server {
    listen 80;
    server_name your-domain.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### 使用 Docker Compose 多环境配置

创建 `docker-compose.prod.yml`：
```yaml
version: '3.8'

services:
  feequery-web:
    image: feequery:latest
    restart: always
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 2G
        reservations:
          cpus: '0.5'
          memory: 512M
```

启动：
```bash
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

## 🎯 下一步

- 配置云厂商账号
- 设置费用预警规则
- 配置通知渠道（邮件、钉钉等）
- 查看[完整文档](./CLAUDE.md)了解更多功能

## 📞 获取帮助

如遇到问题，请检查：
1. Docker 日志：`docker logs feequery-web`
2. 应用日志：在容器内查看 `/app/logs` 目录
3. 项目文档：`CLAUDE.md`

---

**祝使用愉快！** 🎉
