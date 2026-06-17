# MES

MES项目

个人的一些[开发过程文档](./doc)

## 解决方案结构

```
DevKit/
├── DevKitCore/                 # 核心层（领域层 Domain）
│   ├── Entities/                     # 实体/聚合根/值对象
│   ├── Abstractions/                 # 接口：仓储、领域服务、基础服务
│   ├── Specifications/               # 规范模式（查询条件）
│   ├── Events/                       # 领域事件（可选）
│   ├── Enums/                        # 枚举
│   ├── Errors/                       # 自定义异常 & 错误模型
│   └── Common/                       # 公共规范、常量、工具（不依赖 Infra）
│
├── DevKitInfrastructure/       # 基础设施层（EFCore + 外部访问）
│   ├── Persistence/                  # EFCore DbContext、配置文件
│   ├── Repositories/                 # 实现 Core.Abstractions 中的接口
│   ├── Migrations/                   # EFCore 数据迁移
│   ├── External/                     # 第三方服务（金蝶、MQ、API、PLC）
│   ├── Configurations/               # Fluent API 实体配置
│   └── Options/                      # 配置项类（AppSettings 映射）
|
├── DevKitApplication/          # 应用服务层（用例逻辑）
│   ├── Services/                     # 应用服务（业务 orchestrator）
│   ├── DTOs/                         # 输入输出模型（ViewModel用）
│   ├── Commands/                     # CQRS 命令
│   ├── Queries/                      # CQRS 查询
│   ├── Validators/                   # FluentValidation 校验器
│   └── Mapping/                      # AutoMapper 配置
│
├── DevKitAPI/                  # Web API 层（如果需要）
│   ├── Controllers/                  # API 控制器
│   ├── Middlewares/                  # 中间件（异常、日志、认证）
│   ├── Filters/                      # 过滤器
│   ├── Models/                       # API 专用模型
│
├── DevKitServer/               # Web 服务启动层
│   └── Program.cs                    # Bootstrap
│
├── DevKitDesktop/         # WPF/Blazor/ASP.NET 表现层（任选）
│   ├── Views/                        # UI 页面
│   ├── ViewModels/                   # MVVM 模型
│   ├── Controls/                     # 自定义控件
│   ├── Converters/                   # 值转换器
│   ├── Resources/                    # 样式、图片、国际化
│   ├── Extensions/                   # UI 特有扩展
│   └── App.xaml.cs                   # UI 程序入口
│
├── DevKitShared/               # 共享库（跨各层）
│   ├── Utilities/                    # 通用工具类
│   ├── Extensions/                   # 扩展方法
│   ├── Constants/                    # 全局常量
│   ├── Results/                      # 通用返回结果（Result<T>）
│   └── Logging/                      # 日志接口/适配器
│
└── DevKit.Tests/                # 测试项目
    ├── UnitTests/
    └── IntegrationTests/
```

Application 层负责：

- 用例编排（Use Case）
- 权限 / 事务 / 聚合协调
- 把 Domain Entity 转成 DTO

## 技术路线

- NET8
- [Obfuscar](https://zrongqing.github.io/posts/7f4ee469/)
- [NLog](https://github.com/NLog/NLog.Extensions.Logging/wiki/NLog-configuration-with-appsettings.json#loading-from-appsettingsjson)
- [ASP.NET Authentication](https://learn.microsoft.com/zh-cn/aspnet/core/security/authentication/?view=aspnetcore-8.0)

### 客户端

- NET10
- Syncfusion WPF UI框架
-

### 服务器

- EFCore

## 功能点