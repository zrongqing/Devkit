# DevkitPrism 可动态卸载模块改造

## Summary

- 外部模块改为“每个 DLL 一个可回收 `AssemblyLoadContext` 和独立 DryIoc 子容器”，不再加载到默认上下文或主容器。
- 在 `Devkit` 项目内新增内置 Prism“模块管理”模块，展示已加载、未加载、失败和文件缺失的模块，并支持刷新、选择 DLL、加载、卸载。
- 默认自动加载新发现模块；用户主动卸载后记录为禁用，下次启动只展示、不自动加载；手动加载后恢复自动加载。
- 卸载仅释放内存、容器和文件占用，不删除或移动模块文件。

## Implementation Changes

### 构建与模块输出

- 重构 `Directory.Build.props` 和 `Module.Build.props`，建立统一的“主程序已提供程序集”清单。
- 模块保留必要的编译引用，但不复制 `Devkit.*`、Prism、DryIoc、Syncfusion、CommunityToolkit、Microsoft.Extensions、SQLite、Newtonsoft、Mapster 等主程序已有 DLL。
- 从 `Module.Build.props` 移除当前对所有模块无差别添加的业务依赖；Demo、ModuleName、Barcode2 分别声明自己直接使用的包。
- Barcode2 目录仅保留入口 DLL、`.deps.json`、私有依赖及其本机/资源文件，例如 HandyControl、Roslyn、EF Core、Oracle；Demo 和 ModuleName 不再携带 Barcode2 的重型依赖。
- 在模块构建复制阶段过滤主程序程序集，并增加构建后校验；若模块目录仍包含主程序已有 DLL，则构建失败，避免静默回归。

### 动态加载与卸载

- 替换当前 `AddModulesFromDirectory` 默认上下文加载方式；`IModuleCatalog` 只注册内置模块管理模块，外部模块由运行时管理器加载。
- 每个模块入口 DLL使用独立、可回收的 `AssemblyLoadContext`：
  - WPF、Prism、Devkit 契约和主程序依赖统一从默认上下文共享，保证类型身份一致。
  - 模块私有托管和本机依赖通过 `AssemblyDependencyResolver` 从 DLL 所在目录解析。
  - 同一程序集可包含多个 `IModule`，统一注册、初始化并按相反顺序卸载。
- 每个模块创建 DryIoc 子容器；模块的导航、ViewModel 和业务服务只注册到子容器。卸载时释放子容器，避免主容器缓存模块类型。
- 加载过程具备事务性：程序集验证、注册或初始化失败时，撤销菜单、释放子容器并启动加载上下文回收，只在管理页保留字符串错误信息。
- 卸载顺序固定为：确认并关闭模块页签 → 取消并等待页面任务 → 调用模块卸载钩子 → 删除模块菜单 → 释放模块实例和子容器 → 清空强引用 → 调用 `Unload()` 并用弱引用验证回收。
- 卸载是 .NET 的协作式行为；若 GC 后仍有外部引用，状态显示“等待释放/卸载失败”，拒绝重复加载并提供诊断，而不误报成功。[Microsoft 的卸载说明](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability)
- 应用退出时卸载全部模块，但不把它们写入禁用列表。

### 模块管理和界面联动

- 在 `Devkit/Modules/ModuleManagement` 下新增内置模块、管理 View/ViewModel 和运行时服务，在“模块”菜单下增加“模块管理”入口。
- 管理页显示名称、版本、入口路径、来源、启用状态、运行状态和最近错误，提供刷新、选择 DLL、加载和卸载命令。
- 固定目录扫描 `AppContext.BaseDirectory/modules`；同时读取用户曾选择的外部 DLL 路径。外部 DLL 原地加载，其依赖从同目录解析，不复制文件。
- 使用程序集简单名称作为模块标识，并禁止两个不同路径同时注册同名模块。
- 通过 `IModuleStorage` 将 `disabledModuleIds` 和 `externalModulePaths` 保存到模块管理状态文件：
  - 固定目录中新出现的模块默认启用并在下次启动自动加载。
  - 主动卸载后加入禁用集合，但继续显示在管理页。
  - 手动加载会移出禁用集合。
  - 外部路径即使卸载或文件缺失仍保留并显示，便于恢复。
  - 状态文件缺失使用空状态；损坏时备份/忽略并提示，不阻止程序启动。
- 菜单模型和页签增加模块所有者 ID；菜单注册表提供变更通知和按模块注销，菜单树在加载/卸载后立即刷新。
- 页签宿主跟踪初始化任务。卸载存在打开页签或运行中任务的模块时先确认；确认后强制取消、等待并销毁所有相关内容，包括不可手动关闭或固定的页签。
- 将 `IShellService` 实现移到 `Devkit/Services`，由它根据菜单所有者选择主容器或对应模块子容器解析内容。

### 现有模块生命周期

- 在 `Devkit.Prism` 增加 `IUnloadableModule.OnUnloading(IContainerProvider)` 契约；所有现有外部模块实现该钩子。
- `IMenuRegistry` 增加模块所有者注册、`UnregisterByModule` 和变更通知；`MenuItemModel`、`TabItemModel` 增加 `ModuleId`。
- Demo、ModuleName、Barcode2 在初始化时为全部菜单标记模块 ID，卸载时注销本模块菜单和其他显式资源。
- 模块视图移除全局 `ViewModelLocator.AutoWireViewModel`，改为在子容器注册 ViewModel，并通过 View 构造函数注入，防止 Prism 静态定位器或主容器持有模块类型。
- 模块页面销毁时调用 `IDestructible.Destroy()`，取消正在运行的异步操作；模块单例和 `IDisposable` 服务由子容器统一释放。
- Barcode2 的 SQLite 源码缓存关闭连接池，避免操作完成或模块卸载后继续占用数据库文件；这也修复当前 3 个 `source-cache.db` 基线测试失败。

## Test Plan

- 清理后构建解决方案，验证三个模块目录与主程序根目录的 DLL 文件名交集为空，且 Barcode2 私有托管、本机和资源依赖仍完整。
- 增加动态加载集成测试：共享契约来自默认上下文、私有依赖来自模块上下文、多模块类型初始化成功、重复名称被拒绝。
- 验证卸载后菜单和页签消失、页面任务被取消并等待、`Destroy` 与模块卸载钩子执行、子容器单例被释放、加载上下文弱引用被回收，入口 DLL 可被替换。
- 验证加载失败回滚：无效 DLL、没有 `IModule`、缺失依赖、初始化异常和文件路径失效均不留下菜单、容器注册或半加载状态。
- 验证状态持久化：首次默认加载、主动卸载后重启不自动加载、手动加载恢复启用、外部路径保留、状态文件缺失或损坏时安全降级。
- 验证管理页确认逻辑、菜单实时刷新、卸载后重新加载，以及应用退出不修改启用状态。
- 执行 `dotnet build src/client/DevkitPrism/DevkitPrism.slnx` 和 `dotnet test src/client/DevkitPrism/DevkitPrism.slnx`；保留现有警告治理边界，但要求测试全部通过。

## Assumptions

- “不带主程序已有 DLL”指不在模块输出目录重复部署运行时 DLL；模块仍必须保留对 Prism 和 Devkit 契约的编译时引用。
- WPF、Prism 和共享契约必须留在默认加载上下文，模块私有依赖才进入可回收上下文。[Microsoft 的 AssemblyLoadContext 指南](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
- 当前模块没有跨模块二进制依赖；第一版要求每个模块除主程序共享程序集外自行携带全部私有依赖。
- 卸载不删除磁盘文件；只有弱引用确认加载上下文已回收后才标记为完全卸载。
