# Devkit.Modules.Ssamc

该项目由原 `Module.Ssamc`、`Ssamc.Core` 和 `SSAMC.DB` 三个项目合并而成。

运行 API 更新功能前，按界面目标配置数据库连接：

- `DEVKIT_SSAMC_SOURCE_PATH`
- `DEVKIT_SSAMC_DB_215_58_CONNECTION`
- `DEVKIT_SSAMC_DB_20_54_CONNECTION`

菜单树根据页面中选择的正式、测试或开发环境连接对应的 Oracle 数据库。可在应用目录的
`Modules/ssamc/menu-tree.json` 中覆盖连接串：

```json
{
  "PageEnvironmentKey": "development",
  "DatabaseConnections": {
    "production": "data source=<host>/<service>;user id=<user>;password=<password>",
    "test": "data source=<host>/<service>;user id=<user>;password=<password>",
    "development": "data source=<host>/<service>;user id=<user>;password=<password>"
  }
}
```

配置文件优先；也可使用环境变量覆盖内置默认值：

- `DEVKIT_SSAMC_MENU_DB_PRODUCTION_CONNECTION`
- `DEVKIT_SSAMC_MENU_DB_TEST_CONNECTION`
- `DEVKIT_SSAMC_MENU_DB_DEVELOPMENT_CONNECTION`
- `DEVKIT_SSAMC_MENU_DB_CONNECTION`（兼容旧版，作为三个环境的统一覆盖）

菜单页面地址可按环境覆盖；未配置时使用内置的正式、测试、开发地址：

- `DEVKIT_SSAMC_PAGE_PRODUCTION_BASE_URL`（默认 `http://192.168.10.41`）
- `DEVKIT_SSAMC_PAGE_TEST_BASE_URL`（默认 `http://192.168.20.54`）
- `DEVKIT_SSAMC_PAGE_DEVELOPMENT_BASE_URL`（默认 `http://192.168.215.57`）

运行 Webapp 更新功能前，按目标配置共享目录；多个根目录使用分号分隔：

- `DEVKIT_SSAMC_WEBAPP_SOURCE_PATH`
- `DEVKIT_SSAMC_WEBAPP_215_22_ROOTS`、`DEVKIT_SSAMC_WEBAPP_215_22_USERNAME`、`DEVKIT_SSAMC_WEBAPP_215_22_PASSWORD`
- `DEVKIT_SSAMC_WEBAPP_20_53_ROOTS`、`DEVKIT_SSAMC_WEBAPP_20_53_USERNAME`、`DEVKIT_SSAMC_WEBAPP_20_53_PASSWORD`
- `DEVKIT_SSAMC_WEBAPP_PRODUCTION_ROOTS`、`DEVKIT_SSAMC_WEBAPP_PRODUCTION_USERNAME`、`DEVKIT_SSAMC_WEBAPP_PRODUCTION_PASSWORD`

API 更新和 Webapp 更新所需凭据只从进程环境读取。菜单数据库连接也可以写入上述应用本地
配置文件，但包含真实凭据的 `menu-tree.json` 不应写入仓库。

## Menu tree extension points

`MenuTreeView` 通过 `IMenuTreeDataSource` 从 Oracle 的 `SYS_MENU`、
`SYS_MODULE` 和 `SYS_PAGE` 获取层级菜单及模块主页面，只加载菜单中
`IS_DELETE = 0` 的记录，不过滤 `IS_STATE`。`IWebPageLauncher` 生成 Barcode
主框架深链，使用模块主页面 ID 作为 `moduleid` 的值打开
`/Page/?moduleid=...` 选项卡。
