# LlamaLift v1.0.0-preview

> 本地模型，一键起飞。

这是进入公开测试前的最后一个私有内测预览版本。它把客户端协议选择、真实端点验证和本地模型实时状态收进统一工作流，同时保留 v0.4 已完成的运行时安装、自适应调参、命令预检、生命周期治理和性能监测能力。

## 主要更新

- 在模型配置中选择 Responses、Chat Completions 或 Anthropic Messages。
- 服务总览显示协议对应的 Base URL、完整端点与鉴权方式，并可测试当前或全部协议。
- 协议切换不修改启动参数，也不会丢失自定义命令。
- 侧栏以已关闭、加载中、已就绪、输出中、正在停止、外部服务或异常代替安装/便携模式标签。
- 新托管 Key 使用 `sk-llamalift-<64 位十六进制>` 格式。

## 验证范围

- 三种协议路由、JSON 负载和鉴权头的真实本机 HTTP 回环。
- 配置迁移、API Key、参数同步、自适应矩阵、运行时安全解压和进程生命周期故障注入。
- 22 个 UI 场景：940×600、1320×840、浅色/深色及 125%/150%/175%/200% DPI。
- 便携版 ZIP 内容、安装包构建和 SHA-256 摘要。

## 发布文件

- `LlamaLift-v1.0.0-preview-portable-win-x64.zip`
- `LlamaLift-v1.0.0-preview-Setup.exe`
- `LlamaLift-v1.0.0-preview-SHA256SUMS.txt`

## 内测提示

- 当前构建尚未进行 Authenticode 数字签名，Windows SmartScreen 可能显示未知发布者提示。
- 这是私有 Pre-release，不建议直接用于生产环境。
- llama.cpp 端点能力取决于所选上游版本；“测试全部”可确认该版本实际支持的协议。

## 项目成员

- 作者与维护者：RankeeNd-Masen Hu
- 项目支持者：Hongbin Sun
