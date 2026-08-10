# Llama Server Manager

> 当前版本：`v0.2.0-dev`
> 发布状态：私有内测，暂不公开发布或分发。

Llama Server Manager 是一个面向 Windows 的本地大模型运行管理器。它可以从 `ggml-org/llama.cpp` 官方 Release 安装和切换 Windows 运行时，也可以根据本机硬件、GGUF 模型与目标模式生成可解释的启动参数。

## v0.2 开发功能

- 检测 CPU、系统内存、GPU、显存和驱动信息。
- 从 llama.cpp 官方 Release API 列出近期 Windows x64 CPU、CUDA、Vulkan、SYCL、HIP 构建。
- 根据硬件优先选择运行后端；CUDA 构建自动配对同版本 cudart 包。
- 流式下载、安全解压、官方摘要可用时执行 SHA-256 校验，并保存多个已安装版本。
- 把已安装运行时一键绑定到当前模型配置，同时保留手工选择 `llama-server.exe` 的离线方式。
- 读取 GGUF v2/v3 元数据，不加载模型权重即可识别架构、最大上下文、层数、嵌入维度、GQA 和量化类型。
- 提供“快速、均衡、极限”三档自适应方案，自动配置上下文、GPU 层、KV Cache 量化、Fit 余量、线程和批处理参数。
- 应用方案前展示资源依据、模型量化建议和风险提示，应用后所有参数仍可手工修改。

## 基础功能

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
- 运行时只从 GitHub 官方 HTTPS 地址下载；压缩包会进行路径穿越检查，校验和安装完成前不会登记为可用版本。
- 本地配置、日志、模型、密钥与构建产物均被排除在 Git 仓库之外。

监听地址设为 `0.0.0.0` 时，请自行限制 Windows 防火墙来源范围，并为 llama-server 配置 API Key。

## 运行要求

- Windows 10/11 x64
- .NET Framework 4.8
- 联网安装 llama.cpp 时需要能够访问 GitHub；也可离线手工选择已有构建
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
2. 打开“运行环境”，点击“检测并刷新版本”，选择推荐或指定的 llama.cpp 版本并安装。
3. 在“模型配置”中选择 GGUF 模型。
4. 选择“快速、均衡、极限”并点击“检测并生成方案”，确认后应用推荐参数。
5. 点击“检测后端”，保存配置并启动服务。

便携版通过程序目录中的 `portable.flag` 启用，配置保存在程序旁的 `data/`；安装版配置保存在 `%LOCALAPPDATA%\LlamaServerManager`。

## 内测范围

v0.2 当前重点验证：

- GitHub 网络不稳定、下载中断、摘要不匹配和异常压缩包的恢复行为。
- CPU、CUDA、Vulkan、SYCL、HIP 官方构建的安装与切换。
- NVIDIA、AMD、Intel、核显和纯 CPU 机器的后端推荐准确性。
- 不同架构、量化和分片 GGUF 的元数据识别。
- 快速、均衡、极限方案在不同内存/显存组合下的安全性和性能。
- 多模型配置、启停、重启和托盘流程。
- 本机与局域网 OpenAI 兼容 API 连接。
- 高 DPI、不同缩放比例与浅色/深色主题。
- 异常退出、端口冲突和配置损坏后的恢复行为。

已知问题和测试反馈请记录在私有仓库的 Issues 中。
