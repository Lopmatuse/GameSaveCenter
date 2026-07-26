# Windows 构建、安装与首次配置

## 一、前置条件

建议使用 Windows 10/11 x64，并安装：

1. Playnite 10.56 或兼容的 Playnite 10 稳定版；
2. Visual Studio 2022（含“.NET 桌面开发”）或 .NET 8 SDK 8.0.420；
3. .NET Framework 4.6.2 Developer Pack（项目也引用 reference assemblies 包）；
4. Ludusavi 最新稳定版；
5. Rclone 最新稳定版（仅云端复制需要）。

Ludusavi 和 Rclone 不应放入 Git 仓库。建议目录：

```text
D:\GameSaveCenterTools\Ludusavi\ludusavi.exe
D:\GameSaveCenterTools\Rclone\rclone.exe
D:\GameSaveCenterData\Saves
D:\GameSaveCenterData\Media
```

## 二、构建与测试

在仓库根目录打开 PowerShell：

```powershell
Set-ExecutionPolicy -Scope Process Bypass
./scripts/build.ps1 -Configuration Release
```

脚本会依次执行：

```text
dotnet restore
dotnet build
dotnet test
```

任何错误都应先修复，不能跳过后直接测试真实存档。

也可以双击或运行：

```cmd
scripts\build.cmd
```

## 三、生成 Playnite 扩展包

```powershell
./scripts/package.ps1 -Configuration Release
```

输出：

```text
artifacts/GameSaveCenter_66e9f2d7-67bb-43ef-b62a-b8e60734fcec/
artifacts/GameSaveCenter-0.1.0-playnite.zip
artifacts/GameSaveCenter-0.1.0.pext
```

Worker 默认按 `win-x64` 自包含发布，不要求目标电脑额外安装 .NET 8 Runtime。

## 四、安装

优先尝试双击 `.pext`。若 Playnite 不接受普通 ZIP 攒成的 `.pext`，使用开发安装脚本：

```powershell
./scripts/install-dev.ps1 -BuildFirst
```

便携版 Playnite 需要显式提供扩展目录：

```powershell
./scripts/install-dev.ps1 -PlayniteExtensionsPath "D:\Playnite\Extensions"
```

安装后完全退出并重启 Playnite。

## 五、配置插件

在 Playnite 的扩展设置中填写：

- Worker：通常会自动指向扩展目录下 `Worker\GameSaveCenter.Worker.exe`；
- Ludusavi：`ludusavi.exe` 的绝对路径；
- Ludusavi 存档备份目录；
- Rclone：`rclone.exe` 的绝对路径；
- Rclone 目标，例如 `myremote:GameSaveCenter`；
- 媒体归档目录；
- 默认备份间隔，建议 30 分钟；
- 外部进程侦测：开启；
- 媒体同步：开启；
- 云端上传：先关闭，完成本地测试后再开启。

Rclone 凭据只使用 `rclone config` 配置，不写入插件设置、日志或仓库。

## 六、首次安全测试

严格按 `WINDOWS_TEST_PLAN.md`：

1. 先用无重要存档的 Steam 游戏测试扫描和备份；
2. 验证退出后备份和 30 分钟任务；
3. 验证 Steam 截图只增量复制一次；
4. 再测试 Game Bar 或其他平台截图；
5. 本地稳定后才启用 Rclone `copy`；
6. 最后才使用可丢弃的存档测试恢复与撤销；
7. Xbox WGS 恢复不作为首轮测试。

## 七、生成含 Git 历史的源码包

```powershell
./scripts/package-source.ps1
```

该脚本会包含隐藏的 `.git`，并排除构建输出、数据库、日志和常见密钥文件。
