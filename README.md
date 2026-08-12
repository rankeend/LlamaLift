# LlamaLift

> **本地模型，一键起飞。**

> 当前版本：`v0.4.0-dev`
> 发布状态：私有内测，暂不公开发布或分发。

LlamaLift 是一个面向 Windows 的本地大模型运行管理器。它可以从 `ggml-org/llama.cpp` 官方 Release 安装和切换 Windows 运行时，根据本机硬件、GGUF 模型与目标模式生成可解释的启动参数，并在统一仪表盘中实时观察系统与推理性能。

## v0.4 开发功能

- 全局品牌升级为 `LlamaLift — 本地模型，一键起飞。`，窗口、托盘、安装包与便携版统一命名。
- 新增“性能监测”页面，每 2 秒可视化 CPU、系统内存、GPU 引擎、独占/共享显存、llama-server CPU/内存/GPU、磁盘与网络吞吐；离开该页面后自动降频。
- 接入 llama-server `/metrics` 与 `/slots`，展示预填充/生成速度、累计 tokens、处理/排队请求、并发槽位、上下文用量与高水位、推测解码槽及运行时长。
- 图表保留最近 90 个采样点，支持悬停读取采样值和暂停/继续；监测完全在本机完成。
- 新增“参数工作台”，可直接编辑完整 `llama-server` 命令，并把识别到的参数反向同步至简易配置表单。
- 支持长短参数、`--参数=值`、带空格路径、跨行命令和常见布尔写法；保存时自动检查语法、文件、端口、参数组合和鉴权风险。
- 检测结果会给出具体修改建议，但不会强制阻止保存；确认后原始自定义命令会原样保存并用于启动。
- 未识别的 llama.cpp 参数会原样保留到“自定义参数”，避免进阶参数在双向转换时丢失。
- 内置“预设 1 / 2 / 3”参数槽，可应用当前预设、把当前参数保存到槽位并自定义名称。
- 内置 API Key 管理器，可创建、生成、导入、脱敏查看并为当前模型选择托管密钥文件。
- 启动前会真实检查 API Key 文件是否可读；遇到 Ollama 等无法读取中文参数路径的 llama-server 构建时，会自动使用不含密钥内容的临时兼容路径。
- 参数预设只覆盖性能与高级参数，不会误改模型路径、程序路径、监听地址或端口。
- 整体界面重构为浅色优先、深色完整适配的 Apple 式极简桌面设计：Windows 原生窗口行为、低饱和侧栏、分组卡片、单主操作层级，以及始终保持深色的命令与日志区域。
- 全部 UI 资源离线可用；中文使用 Microsoft YaHei UI，英文与指标使用 Segoe UI，代码区优先使用 Cascadia Mono（未安装时回退 Cascadia Code/Consolas）。

## v0.2 开发功能

- 检测 CPU、系统内存、GPU、显存和驱动信息。
- 从 llama.cpp 官方 Release API 列出近期 Windows x64 CPU、CUDA、Vulkan、SYCL、HIP 构建。
- 根据硬件优先选择运行后端；CUDA 构建自动配对同版本 cudart 包。
- 流式下载、安全解压、官方摘要可用时执行 SHA-256 校验，并保存多个已安装版本。
- 把已安装运行时一键绑定到当前模型配置，同时保留手工选择 `llama-server.exe` 的离线方式。
- 模型配置中的“检测”按钮会自动搜索已登记运行时、PATH、LlamaLift 数据目录和常见安装目录，确认后填写程序路径；未找到或结果不正确时，可手动选择 llama.cpp 安装目录或精确选择 EXE。
- 读取 GGUF v2/v3 元数据，不加载模型权重即可识别架构、最大上下文、层数、嵌入维度、GQA 和量化类型。
- 提供“快速、均衡、极限”三档自适应方案，固定预设目标为 32K/64K/128K，并按硬件、GGUF 上限和运行时真实 KV 能力自动配置上下文、GPU 层、KV Cache、Fit 余量、线程和批处理参数；手工改参后会显示“自定义”。
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
- 程序不会上传或写入日志 API Key。管理器只在用户主动管理密钥时读取本地文件，默认脱敏显示，显式点击后才展示明文。
- 为兼容 llama.cpp，托管 Key 以本地文本文件保存在 `data/api-keys` 或当前用户应用数据目录，并继承所在目录权限；请勿共享便携版的 `data/api-keys`、把密钥打包发布或放入公共目录。
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
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" .\installer\LlamaLift.iss
```

构建结果会写入 `dist/`、`dist-installer/`、`release/` 和 `test-output/`；这些目录不会提交到仓库。

## 快速使用

1. 运行 `LlamaLift.exe`。
2. 打开“运行环境”，点击“检测并刷新版本”，选择推荐或指定的 llama.cpp 版本并安装。
3. 在“模型配置”中选择 GGUF 模型。
4. 选择“快速、均衡、极限”并点击“检测并生成方案”，确认后应用推荐参数。
5. 如需高级控制，打开“参数工作台”编辑完整命令并点击“校验并保存”；根据建议修改，或确认风险后按原文保存。
6. 点击“检测后端”，保存配置并启动服务；随后可在“性能监测”中观察系统与推理状态。

便携版通过程序目录中的 `portable.flag` 启用，配置保存在程序旁的 `data/`；新安装版配置保存在 `%LOCALAPPDATA%\LlamaLift`。已有内测配置会继续从旧目录读取，不会因品牌升级丢失。

## 内测范围

v0.4 当前重点验证：

- GitHub 网络不稳定、下载中断、摘要不匹配和异常压缩包的恢复行为。
- CPU、CUDA、Vulkan、SYCL、HIP 官方构建的安装与切换。
- NVIDIA、AMD、Intel、核显和纯 CPU 机器的后端推荐准确性。
- 不同架构、量化和分片 GGUF 的元数据识别。
- 快速、均衡、极限方案在不同内存/显存组合下的安全性和性能。
- 命令编辑后的双向同步、未知参数保留、错误阻断与完整命令往返一致性。
- 三个参数预设槽的应用、覆盖保存、重命名，以及对模型/网络字段的隔离保护。
- 多模型配置、启停、重启和托盘流程。
- 本机与局域网 OpenAI 兼容 API 连接。
- 940×600 最小窗口、1320×840 标准窗口、125%/150%/175%/200% DPI、七页上下滚动状态与浅色/深色主题。
- 异常退出、端口冲突和配置损坏后的恢复行为。

已知问题和测试反馈请记录在私有仓库的 Issues 中。
