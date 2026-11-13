# Devkit.Modules.Barcode2

该项目由原 `Module.Barcode2`、`Barcode2.Core` 和 `Barcode2.DB` 三个项目合并而成。

## 本地配置

Barcode2 的 Oracle 环境、页面/后端基址、Webapp 发布目标和本地源码目录统一在应用内
“Barcode2 → 配置”页面维护。配置保存在当前 Windows 用户目录下的：

```text
%LocalAppData%\Devkit\Config\devkit-settings.db
```

数据库账号、数据库密码、共享账号和共享密码经 Windows DPAPI `CurrentUser` 保护后写入 SQLite；
应用不会记录这些字段的内容。同一份数据库复制给其他 Windows 用户后，受保护字段无法解密，
需要在配置页面重新填写。

首次读取配置时只创建开发、测试、正式环境和对应发布目标的空白语义骨架，不在代码中内置
数据库地址、页面地址、共享目录或账号密码。实际值由用户在配置页面录入并保存到 SQLite。
配置允许分步保存；执行 API 更新、菜单查询、页面打开或 Webapp 发布时，仅校验本次操作使用的
环境和目标，并在缺少配置时给出明确提示。

正式、测试和开发环境各维护一套 Oracle 数据源与凭据，API 更新和菜单检索共用对应环境配置。
配置页面以明文展示账号密码，但写入 SQLite 时仍使用 DPAPI 保护。每个 Webapp 发布目录独立保存
账号密码，因此正式环境的三个目录可以分别配置。发布时仍会优先尝试当前 Windows 身份或已有
SMB 会话；对应目录的共享凭据仅作为访问失败后的回退。

“API 更新”页面的“扫码路径”可直接编辑，打开页面时以配置中的源码目录作为初始值。
扫描、预览和更新均使用操作开始时页面中填写的路径，不会重新读取配置覆盖输入。
页面内修改的路径仅在当前页面生效，不回写配置；重新打开页面时仍从配置初始化。

“前端更新”页面可直接修改 `webapp path`，读取文件夹和发布均使用当前输入的源码目录，
文件相对路径以该目录为基准。该路径独立保存，与配置页面的“Webapp 源码目录”互不影响，
尚未保存时默认为空，不从配置页面回填。修改源码目录后需重新读取并选择文件。
点击“保存路径”会将源码目录和可选的“Webapp 发布路径”保存到本地 SQLite，重新打开页面时恢复。
发布路径留空时沿用配置页面的各环境发布目录；填写 UNC 共享路径后，开发、测试、正式发布
均使用该路径覆盖所选环境的发布目录，共享账号密码继续使用所选环境的配置，原发布目录配置不变。

## Menu tree extension points

`MenuTreeView` 通过 `IMenuTreeDataSource` 从 Oracle 的 `SYS_MENU`、
`SYS_MODULE` 和 `SYS_PAGE` 获取层级菜单及模块主页面，只加载 `IS_DELETE = 0` 的记录，
不额外过滤 `IS_STATE`。`IWebPageLauncher` 使用模块主页面 ID 生成 Barcode 主框架深链：

```text
/Page/?moduleid=<main-page-id>
```
