DevkitPrism/
├── DevkitPrism.slnx
├── Directory.Build.props            # 已有，保持
├── Directory.Packages.props         # 已有，保持
└── src/
    ├── Devkit/                      # 主程序壳（WinExe, net10.0-windows）
    │   ├── App.xaml(.cs)            # UI 启动入口
    │   ├── app.manifest
    │   ├── appsettings.json
    │   ├── Properties/
    │   ├── Styles/
    │   ├── Themes/                  # ← Syncfusion 主题资源集中（若从 Core.UI 下沉）
    │   ├── Services/                # ← 原 Servers/，修正笔误（ShellService.cs 等）
    │   ├── Triggers/                # ← 原 Trigger/，统一复数（TreeViewFilterTrigger 等）
    │   ├── Helpers/
    │   ├── Models/
    │   ├── ViewModels/
    │   └── Views/
    │
    ├── Devkit.Core/                 # 纯核心层（net10.0，无 WPF 依赖）
    │   ├── Contracts/               # 接口、抽象
    │   ├── Helpers/
    │   ├── RegionNames.cs
    │   └── SysViewKeys.cs
    │   # ← 删除空的 UI/Models/，UI 相关全部移到 Devkit.Core.UI
    │
    ├── Devkit.Core.UI/              # UI 基础设施（net10.0-windows，Syncfusion+MVVM）
    │   ├── Attributes/
    │   ├── Behaviors/
    │   ├── Contracts/
    │   │   ├── Enums/
    │   │   ├── Models/
    │   │   ├── Services/            # ← 原 Contracts/Servers/，修正笔误
    │   │   └── Views/
    │   ├── Helpers/
    │   ├── Mvvm/
    │   └── ValueConverters/
    │
    ├── Devkit.Prism/                # Prism + DryIoc 适配层
    │   ├── Events/
    │   └── Extensions/
    │
    ├── Modules/                     # 业务模块（每个模块独立 csproj）
    │   └── Devkit.Modules.ModuleName/
    │       ├── ViewModels/
    │       └── Views/
    │
    ├── Services/                    # 应用服务层
    │   ├── Devkit.Services.Interfaces/   # net10.0（原 netstandard2.1）
    │   │   ├── Files/
    │   │   └── Share/
    │   └── Devkit.Services/              # net10.0（原 netstandard2.1）
    │       └── Share/
    │
    └── Tests/                       # 测试，按被测项目镜像
        ├── Devkit.Modules.ModuleName.Tests/
        ├── Devkit.Core.Tests/       # ← 未来扩展
        └── Devkit.Services.Tests/   # ← 未来扩展