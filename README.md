# QCmcl - Minecraft 启动器

一个基于 C#/WPF 的 Minecraft 启动器，支持原版、Forge、NeoForge、Fabric、OptiFine 整合包的下载与启动。

![QCmcl](src/MinecraftLauncher/Assets/icon-256.png)

## 功能

- **原版游戏**：在线下载安装任意正式版/快照版
- **整合包**：从 Modrinth 一键搜索下载安装（自动装 Fabric）
- **Mod / 光影**：Modrinth 搜索下载到对应目录
- **启动**：支持原版/Forge/NeoForge/Fabric/OptiFine 版本
- **Java 自动检测**：扫描系统所有 Java，按版本需求自动匹配
- **皮肤编辑器**：OpenGL 3D 模型上直接绘制 64x64 皮肤
- **主题**：深色/浅色 + 7 种主题色
- **下载进度浮窗**：任意页面可见、可拖动、可取消

## 运行要求

- Windows 10 / 11（x64）
- [.NET Framework 4.7.1+](https://dotnet.microsoft.com/download/dotnet-framework)
- WebView2 Runtime（Windows 11 自带，Win10 可从 [这里](https://developer.microsoft.com/microsoft-edge/webview2/) 安装）
- Java（启动器会自动检测）

## 直接使用

下载 `release/QCmcl/WCmcl.exe`（或自行编译），双击运行即可。

## 从源码编译

1. 用 Visual Studio 2022 打开 `src/MinecraftLauncher/MinecraftLauncher.csproj`
2. 依赖包需要放在 `packages/` 目录（见下方）
3. 编译 Release 配置，产物在 `bin/Release/QCmcl.exe`

### 依赖包

项目引用以下 NuGet 包（需手动下载到 `packages/`）：

```
packages/
├── webview2/          # Microsoft.Web.WebView2 1.0.2903.40
│   ├── lib/net462/
│   │   ├── Microsoft.Web.WebView2.Core.dll
│   │   └── Microsoft.Web.WebView2.Wpf.dll
│   └── runtimes/win-x64/native/WebView2Loader.dll
├── opentk/            # OpenTK 3.3.3
│   └── lib/net20/OpenTK.dll
└── glcontrol/         # OpenTK.GLControl 3.1.0
    └── lib/net20/OpenTK.GLControl.dll
```

获取方式：
```powershell
# NuGet 包下载后解压到对应目录
https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.2903.40
https://www.nuget.org/packages/OpenTK/3.3.3
https://www.nuget.org/packages/OpenTK.GLControl/3.1.0
```

## 项目结构

```
src/MinecraftLauncher/
├── App.xaml / App.xaml.cs        # 应用入口、主题资源
├── MainWindow.xaml / .cs         # 主窗口、导航
├── Assets/                       # 图标、皮肤编辑器 HTML
├── Models/                       # 数据模型
├── Services/                     # 核心服务
│   ├── ConfigService.cs          # 配置管理
│   ├── VersionService.cs         # 版本清单
│   ├── VersionInstallService.cs  # 版本安装
│   ├── VersionResolver.cs        # 版本继承合并
│   ├── LaunchService.cs          # 启动参数构建
│   ├── JavaService.cs            # Java 检测与匹配
│   ├── DownloadService.cs        # 下载（镜像、重试、TLS）
│   ├── ModrinthService.cs        # Modrinth API
│   ├── ModpackService.cs         # 整合包安装
│   ├── FabricService.cs          # Fabric 安装
│   ├── DownloadCenter.cs         # 全局下载进度
│   └── ...
└── Views/                        # 页面
    ├── HomePage.xaml             # 启动页
    ├── DownloadPage.xaml         # 下载页
    ├── AccountPage.xaml          # 账号页
    ├── ModPage.xaml              # Mod 管理
    ├── SkinEditorPage.xaml       # 皮肤编辑器
    └── SettingsPage.xaml         # 设置
```

## 已知限制

- 1.12.2 及更早版本需要 Java 8（启动器无法绕过 Java 兼容性限制）
- 正版登录需要自行注册 Microsoft Azure 应用（Client ID）
- 未实现 Forge 安装器自动安装（整合包中 Forge 需预先安装）

## License

MIT