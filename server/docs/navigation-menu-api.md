# 分端菜单接口与配置

## HTTP 接口

菜单接口首版公开访问，不要求 Bearer Token：

```text
GET /api/v1/web/navigation/menus
GET /api/v1/client/navigation/menus
```

Web 响应使用 `WebNavigationMenuItemDto`，目标字段为 `routeKey`；Client 响应使用 `ClientNavigationMenuItemDto`，目标字段为 `viewKey`。两个 DTO 独立定义，但首版共享以下字段：`id`、`parentId`、`title`、`iconKey`、`order`、`isClosable`。

Web 示例：

```json
{
  "data": [
    {
      "id": "home",
      "parentId": null,
      "title": "首页",
      "routeKey": "home",
      "iconKey": "home",
      "order": 0,
      "isClosable": false
    }
  ],
  "traceId": "0HN..."
}
```

两个端点均由 `NavigationEndpoints` 暴露并调用同一个 `INavigationMenuService`。端点之间没有 HTTP 或 Handler 调用关系。

## appsettings 配置

```json
{
  "Navigation": {
    "Common": [],
    "Web": [],
    "Client": []
  }
}
```

内部配置项字段：

| 字段 | 说明 |
| --- | --- |
| `Id` | 稳定菜单 ID，匹配时不区分大小写 |
| `ParentId` | 父菜单 ID；根菜单省略 |
| `Title` | 显示标题 |
| `TargetKey` | 叶子页面的语义目标键；目录必须为空 |
| `IconKey` | 由各客户端自行映射的语义图标键 |
| `Order` | 排序值 |
| `IsVisible` | 是否可见，默认 `true` |
| `IsClosable` | 页签是否可关闭，默认 `true` |

合并顺序是 Common 后 Web/Client。端侧同 ID 项会完整替换公共项，因此覆盖时需要写出该菜单的全部必要字段。端侧可新增菜单；`IsVisible: false` 会隐藏该菜单及所有后代。

## 启动校验

服务启动时会分别组合并校验 Web 与 Client 菜单：

- 每个配置段内部 ID 不得重复。
- 父节点必须存在，父链不得成环。
- 有子节点的目录不能设置 `TargetKey`。
- 没有子节点的可见页面必须设置 `TargetKey`。
- 必须存在可见的根菜单 `home`，其 `TargetKey` 必须为 `home` 且 `IsClosable` 必须为 `false`。

任一端不满足约束时，Options 启动验证会阻止服务启动。修正 `Navigation` 配置后重新启动即可；该错误与数据库或 Redis 健康状态无关。

## 本地开发与 CORS

Development 配置允许 `http://localhost:5173` 和 `http://127.0.0.1:5173`。新增开发域名或端口时，更新 `Cors:AllowedOrigins`。生产配置默认不开放跨域，应通过同域反向代理或显式生产配置授权来源。

OpenAPI 地址为 `/openapi/v1.json`，Swagger UI 为 `/swagger`。
