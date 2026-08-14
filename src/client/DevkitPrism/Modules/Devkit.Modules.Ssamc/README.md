# Devkit.Modules.Ssamc

该项目由原 `Module.Ssamc`、`Ssamc.Core` 和 `SSAMC.DB` 三个项目合并而成。

运行 API 更新功能前，按界面目标配置数据库连接：

- `DEVKIT_SSAMC_SOURCE_PATH`
- `DEVKIT_SSAMC_DB_215_58_CONNECTION`
- `DEVKIT_SSAMC_DB_20_54_CONNECTION`

运行菜单树功能前，配置独立的 Oracle 菜单数据库连接：

- `DEVKIT_SSAMC_MENU_DB_CONNECTION`

菜单页面地址可按环境覆盖；未配置时使用内置的正式、测试、开发地址：

- `DEVKIT_SSAMC_PAGE_PRODUCTION_BASE_URL`（默认 `http://192.168.10.41`）
- `DEVKIT_SSAMC_PAGE_TEST_BASE_URL`（默认 `http://192.168.20.54`）
- `DEVKIT_SSAMC_PAGE_DEVELOPMENT_BASE_URL`（默认 `http://192.168.215.57`）

运行 Webapp 更新功能前，按目标配置共享目录；多个根目录使用分号分隔：

- `DEVKIT_SSAMC_WEBAPP_SOURCE_PATH`
- `DEVKIT_SSAMC_WEBAPP_215_22_ROOTS`、`DEVKIT_SSAMC_WEBAPP_215_22_USERNAME`、`DEVKIT_SSAMC_WEBAPP_215_22_PASSWORD`
- `DEVKIT_SSAMC_WEBAPP_20_53_ROOTS`、`DEVKIT_SSAMC_WEBAPP_20_53_USERNAME`、`DEVKIT_SSAMC_WEBAPP_20_53_PASSWORD`
- `DEVKIT_SSAMC_WEBAPP_PRODUCTION_ROOTS`、`DEVKIT_SSAMC_WEBAPP_PRODUCTION_USERNAME`、`DEVKIT_SSAMC_WEBAPP_PRODUCTION_PASSWORD`

所有连接串和凭据只从进程环境读取，不应写入仓库。

## Menu tree extension points

`MenuTreeView` 通过 `IMenuTreeDataSource` 从 Oracle 的 `SYS_MENU` 和
`SYS_MODULE` 获取层级菜单，只加载 `IS_DELETE = 0` 的记录，不过滤
`IS_STATE`。`IWebPageLauncher` 生成 Barcode 主框架深链，由主框架调用
`wrapper.refreshTab` 打开 `/Page/?moduleid=...` 选项卡。
