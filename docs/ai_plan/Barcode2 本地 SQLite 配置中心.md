# Barcode2 本地 SQLite 配置中心

## 目标

- Barcode2 的 Oracle 环境、页面地址、Webapp 发布目标和本地源码目录由应用配置页统一管理。
- 源码只保留 `development`、`test`、`production` 三个稳定环境键，不包含实际网络地址或凭据。
- 配置保存在当前 Windows 用户的 `%LocalAppData%\Devkit\Config\devkit-settings.db`。
- 账号和密码通过 Windows DPAPI `CurrentUser` 保护后写入 SQLite。

## 初始化与保存

- 首次运行创建三个环境和五个发布目标的空白语义骨架：开发一个、测试一个、正式三个。
- 不导入其他模块的环境变量、JSON 文件或 SQLite scope。
- 配置页允许部分保存，便于按需逐步录入环境和发布目标。
- 数据库、页面地址和发布目录在对应操作执行前校验；空白配置会得到明确的缺失提示。
- Webapp 发布忽略未填写共享路径的槽位，所选环境至少需要一个有效 UNC 目标。

## 验证

- 配置测试覆盖空白初始化、部分保存、DPAPI 字段持久化和无效地址。
- 消费端测试覆盖成功、网络或服务器失败、配置缺失三类场景。
- 测试网络值使用 `example.invalid` 保留域名，不使用真实环境地址。
