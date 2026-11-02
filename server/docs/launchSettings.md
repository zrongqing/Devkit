# launchSettings.json

`launchSettings.json` 用于本地开发时配置项目的启动方式。`profiles["IIS Express"]` 与 `iisSettings` 都位于该文件中，但作用不同：

| 配置 | 作用 |
| --- | --- |
| `profiles["IIS Express"]` | 定义启动配置，告诉 IDE 如何启动项目 |
| `iisSettings` | 定义 IIS / IIS Express 的服务器设置，例如认证方式、地址和端口 |

## 配置示例

```json
{
  "profiles": {
    "Devkit.Identity.Api": {
      "commandName": "Project",
      "launchBrowser": false,
      "launchUrl": "swagger",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "applicationUrl": "http://localhost:12512"
    },
    "IIS Express": {
      "commandName": "IISExpress",
      "launchBrowser": false,
      "launchUrl": "swagger",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  },
  "iisSettings": {
    "windowsAuthentication": false,
    "anonymousAuthentication": true,
    "iisExpress": {
      "applicationUrl": "http://localhost:64640/",
      "sslPort": 44399
    }
  }
}
```

以下说明以此示例为准，实际地址和端口以各项目的配置为准。

## profiles["IIS Express"]：启动时做什么

- `commandName: "IISExpress"`：使用 IIS Express 托管项目。
- `launchBrowser: false`：启动后不自动打开浏览器。
- `launchUrl: "swagger"`：自动打开浏览器时访问的相对路径；当前未开启自动打开，所以不会主动访问。
- `ASPNETCORE_ENVIRONMENT: "Development"`：以开发环境运行。

`"IIS Express"` 只是启动配置的名称，可以改名；真正决定启动方式的是 `commandName`。

## iisSettings：IIS Express 如何配置

- `windowsAuthentication: false`：关闭 Windows 身份验证。
- `anonymousAuthentication: true`：允许匿名请求进入应用；应用自己的认证、授权规则仍然有效。
- `iisExpress.applicationUrl`：HTTP 地址为 `http://localhost:64640/`。
- `iisExpress.sslPort`：HTTPS 端口为 `44399`。

选择 IIS Express 启动配置时，会配合这里的地址、端口和认证设置运行。

## 不同启动配置的效果

| 选择的启动配置 | 启动方式 | 地址 |
| --- | --- | --- |
| `Devkit.Identity.Api` | `commandName: "Project"`，使用项目自身的 Kestrel | `http://localhost:12512` |
| `IIS Express` | `commandName: "IISExpress"`，使用 IIS Express，并应用 `iisSettings` | HTTP：`http://localhost:64640/`；HTTPS：`https://localhost:44399/` |

这些是本地开发启动设置，不是生产环境的 IIS 部署配置。

