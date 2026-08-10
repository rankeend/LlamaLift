# Changelog

## v0.2.0-dev - 2026-08-10

私有开发版本，尚未发布。

### Added

- llama.cpp 官方 Release 发现、Windows x64 构建筛选和硬件后端推荐。
- CPU、CUDA 12/13、Vulkan、SYCL、HIP 运行时下载、校验、安全解压、登记与切换。
- CPU、内存、GPU、显存和 NVIDIA 驱动检测。
- GGUF v2/v3 关键元数据解析与模型量化识别。
- 快速、均衡、极限三档参数自适应。
- 线程、batch、ubatch 命令生成与手工覆盖控件。
- 官方 API 联网集成测试和合成 GGUF 元数据测试。

### Security

- 下载来源限制为 GitHub HTTPS 域名。
- 支持 GitHub Release `sha256:` 摘要验证。
- 采用临时目录安装，并阻止 ZIP 路径穿越；验证 `llama-server.exe` 后才登记运行时。
- GitHub API 和资产下载提供三次有限重试，失败后不破坏现有配置。

## v0.1.0-internal - 2026-08-10

首个私有内测基线。

### Added

- 多套 llama.cpp 模型配置管理。
- llama-server 进程启动、停止、重启与托盘驻留。
- OpenAI 兼容 API 健康检查和双接口测试。
- 实时日志、运行状态与速度指标。
- 浅色、深色、系统主题和多种强调色。
- 便携版与 Inno Setup 安装版构建流程。
- 离线逻辑测试和多 DPI UI 冒烟测试。

### Security

- 默认仅监听 `127.0.0.1`。
- 仓库不包含模型、密钥、用户配置、运行日志或真实服务器地址。
- 移除早期专用 RTX 3090/Qwen 配置和机器相关路径。
