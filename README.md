# Checkmate

LibCheck的C#后端。
它基于Postgres（Npgsql，ADO）和gRPC，支持实时通讯。

## 开发指南

使用最新的Rider（2024.1.2）或者Visual Studio（安装了.NET Core）来开发和调试这个项目。
开发方式没有什么特殊的地方。

## 构建指南

特别指出，如果不实用集成环境，需要安装dotnet 8.0开发套件，然后运行：

```shell
cd checkmate
dotnet build #dotnet run 来运行
```