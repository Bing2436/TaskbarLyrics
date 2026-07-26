# 任务栏歌词

一个 Windows 10/11 小工具：读取 QQ 音乐公开给 Windows 的系统媒体会话，
自动匹配同步歌词，并把当前歌词显示在任务栏区域。

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
- 同步歌词优先从 QQ 音乐曲库匹配，失败后回退到 LRCLIB；第一次播放某首歌时
  需要联网，命中后会缓存到本机。
- 匹配诊断记录位于 `%LOCALAPPDATA%\TaskbarLyrics\diagnostics.log`。
- 如果一直显示“QQ 音乐未播放”，请在 QQ 音乐设置中开启系统媒体控制/系统播放信息，
  并确认 Windows 音量面板能够显示当前歌曲。
