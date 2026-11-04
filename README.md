# ServerFlow 服务器排班

![应用图标](docs/app-icon.png)

基于 **WinUI 3 / Windows App SDK / .NET 8** 的轻量服务器排班日历。Mica 半透明界面、Windows 原生控件风格，数据仅保存在本机。

![主界面](docs/screenshot.png)

## 功能

- 月历视图，单击日期查看当天占用。
- 单次、每天、每周重复；可设置开始时间、占用小时数和重复截止日期。
- 前台操作 / 后台运行两种资源使用方式。
- 可选备注；无姓名、账号、登录或用户档案。
- 时间冲突阻止保存；覆盖跨午夜和长期周期规则。
- 本地 JSON 原子写入，断网可用。
- 网络仅用于：
  - 读取公开 HTTPS 响应的 `Date` 头校准时间；
  - 查询 GitHub Releases 最新版本。
- 不上传排班数据、不含遥测。

## 安装

从 [Releases](https://github.com/royluobox/ServerFlow/releases/latest) 下载全部资产并解压，运行 `Install-ServerFlow.cmd`。

> 当前 Release 使用项目自签名测试证书。首次安装需要一次 UAC 批准，把证书加入本机受信任根证书存储；应用运行时不需要管理员权限。使用商业代码签名证书或 Microsoft Store 分发可去掉这一限制。

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
3. 将 `dist/release/` 中文件上传到 GitHub Release，标签格式 `v1.0.2`。
4. 更新 `tools/installer-template/ServerFlow.appinstaller` 中的 `MainPackage Uri` 为本次版本化 MSIX 资产地址，并推送到 `main`；App Installer 通过仓库 raw URL 读取更新源。

## 数据位置

```text
%LOCALAPPDATA%\Packages\ServerFlow.Scheduler_pbhj1ej857fh2\LocalCache\Local\ServerFlow\schedules.json
```

卸载应用前请备份该文件。

## 许可

[MIT](LICENSE)
