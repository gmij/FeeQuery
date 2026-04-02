# FeeQuery.Web Docker 镜像（多阶段构建）
# 无需本地安装 .NET SDK，直接 docker-compose up 即可

# 阶段 1：构建
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/FeeQuery.Web -c Release -o /app/publish

# 阶段 2：运行
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# 创建数据目录和密钥目录
RUN mkdir -p /app/data/keys

COPY --from=build /app/publish .

# 设置环境变量
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/feequery.db"

# 暴露端��
EXPOSE 80

ENTRYPOINT ["dotnet", "FeeQuery.Web.dll"]
