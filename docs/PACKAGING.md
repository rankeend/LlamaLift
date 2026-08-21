# LlamaLift 安装与发布契约

从 `v1.1.0-preview` 起，LlamaLift 的 Windows `Setup.exe` 永远是“首次安装 / 原地更新”二合一包，不再另行发布只用于更新的补丁包。

## 用户承诺

- 未安装 LlamaLift 时，运行 `Setup.exe` 完成首次安装。
- 已安装旧版时，直接运行新版 `Setup.exe` 原地更新，不要求先卸载。
- 更新复用旧安装目录和快捷方式选择。
- 更新只替换程序载荷，不覆盖或删除模型配置、API Key、运行时登记、日志与模型文件。
- 安装版用户数据固定保存在 `%LOCALAPPDATA%\LlamaLift`；便携版数据保存在程序目录的 `data/`，两种模式互不迁移。

## 不可破坏的技术条件

1. Inno Setup `AppId` 永久保持 `{BDE1C8B1-4E9B-4F54-B2A7-7B82B7DF42A0}`。改变 AppId 会让新版被识别为另一款应用，禁止修改。
2. 安装模式、64 位模式和默认安装目录约定保持兼容，避免产生并排安装。
3. `UsePreviousAppDir`、`UsePreviousGroup` 和 `UsePreviousTasks` 必须开启。
4. 安装器载荷必须移除 `portable.flag`，确保安装版继续使用 `%LOCALAPPDATA%\LlamaLift`。
5. 安装脚本不得添加针对 `{localappdata}`、`{userappdata}` 或用户数据目录的写入、`[InstallDelete]`、`[UninstallDelete]` 规则。
6. 应用、程序集、构建脚本、安装器和发布文件名必须使用同一版本。
7. 升级前允许 Windows Restart Manager 安全关闭占用程序文件的 `LlamaLift.exe`，不得强制结束并冒险丢失未保存状态。

上述条件由 `installer/verify-upgrade-contract.ps1` 自动检查，并已接入 `test.ps1` 与 `release.ps1`。任何一项不满足都会阻止发布。

## 标准发布命令

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\release.ps1
```

该命令依次运行离线测试、完整 UI/DPI 回归、应用构建、升级契约检查、Inno Setup 编译、便携包内容检查和 SHA-256 生成。正式 Release 至少上传：

- `LlamaLift-v<版本>-Setup.exe`
- `LlamaLift-v<版本>-portable-win-x64.zip`
- `LlamaLift-v<版本>-SHA256SUMS.txt`

发布到 GitHub 前，应确认安装包未签名提示、Preview/Stable 渠道、Release Notes 和 Git 标签均与当前版本一致。
