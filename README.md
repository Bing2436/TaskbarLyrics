# 任务栏歌词

一个 Windows 10/11 小工具：读取 QQ 音乐公开给 Windows 的系统媒体会话，
自动匹配同步歌词，并把当前歌词显示在任务栏区域。

## 下载使用

在 [Releases 页面](https://github.com/Bing2436/TaskbarLyrics/releases/latest) 下载
`TaskbarLyrics-v1.0.1-win-x64.zip`，完整解压后双击 `TaskbarLyrics.exe`。
便携版包含运行时，无需安装 .NET；请保留文件夹内的 DLL 等文件。
更新时先从系统托盘退出旧版，再解压新版到独立文件夹运行。

v1.0.1 修复切换应用、普通/无边框全屏时歌词被遮挡的问题，并改善任务栏自动隐藏时的歌词定位。

## 运行

要求 Windows 10 1903+，建议 Windows 11；开发环境需要 .NET 9 SDK。

```powershell
dotnet run --project .\TaskbarLyrics.csproj
```

发布单文件版本：

```powershell
dotnet publish .\TaskbarLyrics.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None
```

生成的程序位于：

`bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\TaskbarLyrics.exe`

## 使用说明

- 启动程序后，直接用 QQ 音乐播放歌曲。
- 右键系统托盘中的图标，可隐藏歌词、切换左/中/右位置、设置开机启动或退出。
- 歌词窗对鼠标点击是穿透的，不会挡住任务栏操作。
- 切换应用、最大化和普通/无边框全屏时会自动恢复歌词置顶；任务栏自动隐藏时，
  歌词仍保留在屏幕内。显示维护独立于歌词网络请求，不抢占输入焦点。
- 独占全屏游戏、受保护的视频画面和 Windows 安全桌面可能阻止外部窗口叠加，
  游戏中建议切换到无边框全屏模式。
- 同步歌词优先从 QQ 音乐曲库匹配，失败后回退到 LRCLIB；第一次播放某首歌时
  需要联网，命中后会缓存到本机。
- 匹配诊断记录位于 `%LOCALAPPDATA%\TaskbarLyrics\diagnostics.log`。
- 如果一直显示“QQ 音乐未播放”，请在 QQ 音乐设置中开启系统媒体控制/系统播放信息，
  并确认 Windows 音量面板能够显示当前歌曲。

## 全屏显示验证（开发者）

```powershell
dotnet run --project .\LyricsProbe\LyricsProbe.csproj -c Release -- --overlay
```

测试会短暂显示并自动关闭一个全屏窗口，检查遮挡后的置顶恢复、前台焦点、屏幕边界、
手动隐藏和显示，以及停止显示维护后的行为。
