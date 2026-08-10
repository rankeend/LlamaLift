# Llama Server Manager

> 当前版本：`v0.1.0-internal`
> 发布状态：私有内测，暂不公开发布或分发。

Llama Server Manager 是一个面向 Windows 的 `llama-server.exe` 桌面管理器。它负责保存多套模型配置、生成启动参数、管理服务进程、检查 API 状态并记录运行日志，不捆绑 llama.cpp、模型文件或用户密钥。

## v0.1 功能

- 新建、复制、删除和保存多套模型配置。
- 配置 `llama-server.exe`、GGUF、可选 mmproj、API Key 文件和常用运行参数。
- 启动、停止、重启服务，并支持托盘驻留。
- 识别端口占用与外部 llama.cpp 服务。
- 查看实时日志、PID、健康状态、预填充速度和生成速度。
- 测试 `/v1/responses` 与 `/v1/chat/completions` 接口。
- 支持浅色、深色和跟随 Windows 的界面主题。
- 提供安装版与便携版构建流程。

## 安全默认值

- 新配置默认监听 `127.0.0.1`，不会自动向局域网开放。
- 程序不会读取、显示或上传 API Key 内容，只把密钥文件路径传给 llama-server。
- 程序不会自动修改 Windows 防火墙、网络类别或系统代理。
- 本地配置、日志、模型、密钥与构建产物均被排除在 Git 仓库之外。

监听地址设为 `0.0.0.0` 时，请自行限制 Windows 防火墙来源范围，并为 llama-server 配置 API Key。

## 运行要求

- Windows 10/11 x64
- .NET Framework 4.8
- 一份可运行的 llama.cpp Windows 构建
- 至少一个兼容的 GGUF 模型

GPU、驱动和运行后端由所选 llama.cpp 构建决定，可使用 NVIDIA CUDA、AMD Vulkan/ROCm、Intel 或纯 CPU 构建。

## 构建与测试

项目使用 Windows 自带的 .NET Framework C# 编译器构建。仓库内保留构建所需的 AntdUI 2.4.4 `net48` 运行库，许可证信息见 `THIRD-PARTY-NOTICES.txt`。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ui-test.ps1
```

安装包使用 Inno Setup 6 编译：

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" .\installer\LlamaServerManager.iss
```

构建结果会写入 `dist/`、`dist-installer/`、`release/` 和 `test-output/`；这些目录不会提交到仓库。

## 快速使用

1. 运行 `LlamaServerManager.exe`。
2. 在“模型配置”中选择 `llama-server.exe` 和 GGUF 模型。
3. 根据显存、内存与模型能力设置上下文、GPU 层数、KV Cache 等参数。
4. 点击“检测后端”，确认 llama.cpp 可以运行。
5. 保存配置并启动服务。

便携版通过程序目录中的 `portable.flag` 启用，配置保存在程序旁的 `data/`；安装版配置保存在 `%LOCALAPPDATA%\LlamaServerManager`。

## 内测范围

v0.1 主要验证：

- 不同 llama.cpp Windows 构建的参数兼容性。
- 多模型配置、启停、重启和托盘流程。
- 本机与局域网 OpenAI 兼容 API 连接。
- 高 DPI、不同缩放比例与浅色/深色主题。
- 异常退出、端口冲突和配置损坏后的恢复行为。

已知问题和测试反馈请记录在私有仓库的 Issues 中。
