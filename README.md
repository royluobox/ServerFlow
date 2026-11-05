# ServerFlow 服务器排班

![应用图标](docs/app-icon.png)

基于 **WinUI 3 / Windows App SDK / .NET 8** 的轻量服务器排班日历。Mica 半透明界面、Windows 原生控件风格，数据仅保存在本机。

![主界面](docs/screenshot.png)

## 功能

- 月历视图，单击日期查看当天占用。
- 单次、每天、每周重复；可设置开始时间、占用小时数和重复截止日期。
- 每个日期格带 24 小时纵向时间轴：占用按真实起止时间涂成色块，长排班跨天连续填充（50 小时 = 15h + 24h + 11h）。
- 添加排班时实时预览结束时间与跨越的日历日数。
- 前台操作 / 后台运行两种资源使用方式。
- 可选备注；无姓名、账号、登录或用户档案。
- 前台与后台互不冲突，可在同一时段并存并分两条泳道显示；同一使用方式之间仍阻止重叠，覆盖跨午夜、跨多日与长期周期。
- 本地 JSON 原子写入，断网可用。
- 网络仅用于：
  - 读取公开 HTTPS 响应的 `Date` 头校准时间；
  - 查询 GitHub Releases 最新版本。
- 不上传排班数据、不含遥测。

## 安装

下载 **[ServerFlow-Setup.exe](https://github.com/royluobox/ServerFlow/releases/latest/download/ServerFlow-Setup.exe)**，双击，在 UAC 窗口点“是”。

单文件安装器已内嵌应用包与签名证书，会自动完成证书信任、应用安装和启动。不需要解压、不需要提前信任证书、不需要改系统设置。

> 安装器使用项目自签名测试证书，签名的 EXE 与 MSIX 共用同一张证书。Windows 只在首次运行已签名 EXE 时可能提示“未知发布者”；证书信任后不再提示。商业代码签名证书可完全消除该提示。

其他下载项：`ServerFlow_<版本>_x64.msix`（应用包本体，证书已受信任时可直接双击安装）、`ServerFlow-Local.cer`、`Install-ServerFlow.cmd`。

不要双击 `ServerFlow.appinstaller`：它是自动更新源配置，不是安装入口，双击会报 `Error in parsing the app package`。
应用通过 `ServerFlow.appinstaller` 安装，以注册仓库 raw 文件中的更新源。

## 自动更新

- 底部有“检查更新”按钮，调用 GitHub Releases API 比对版本。
- App Installer 启动时按 24 小时间隔检查更新。
- Windows 后台任务约每 8 小时检查一次，且不会弹窗。
- 更新源是仓库 `main` 分支中的 `tools/installer-template/ServerFlow.appinstaller`，通过 raw.githubusercontent.com 访问。
- 每个版本使用带版本号的 Release 资产地址，避免同名 CDN 缓存；发布新版时同步更新 raw 文件中的 `MainPackage Uri`。

## 构建

要求：Windows 10 1809+，x64；.NET 8 SDK；NuGet 可访问。

```powershell
dotnet restore ServerScheduler/ServerScheduler.csproj -r win-x64
dotnet build ServerScheduler/ServerScheduler.csproj -c Release -r win-x64 -p:Platform=x64
dotnet run --project ServerScheduler.Tests/ServerScheduler.Tests.csproj -c Release
```

构建签名 MSIX 与 Release 资产：

```bash
bash tools/build_msix.sh
```

输出位于 `dist/ServerFlow-Installer/` 与 `dist/release/`。签名证书属于发布者私钥，**不在本仓库中**；`tools/signing/` 已被忽略。发布新版需准备 `ServerFlow-Local-Windows.pfx` 与同名 `.cer`。

## 发布

1. 更新 `Package.appxmanifest`、`.csproj` 和 `tools/installer-template/ServerFlow.appinstaller` 中的版本。
2. 运行 `tools/build_msix.sh`。
3. 将 `dist/release/` 中文件上传到 GitHub Release，标签格式 `v1.0.3`。
4. 更新 `tools/installer-template/ServerFlow.appinstaller` 中的 `MainPackage Uri` 为本次版本化 MSIX 资产地址，并推送到 `main`；App Installer 通过仓库 raw URL 读取更新源。

## 数据位置

```text
%LOCALAPPDATA%\Packages\ServerFlow.Scheduler_pbhj1ej857fh2\LocalCache\Local\ServerFlow\schedules.json
```

卸载应用前请备份该文件。

## 许可

[MIT](LICENSE)
