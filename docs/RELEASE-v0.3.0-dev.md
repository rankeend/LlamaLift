# LlamaLift v0.3.0-dev

> 本地模型，一键起飞。

这是 LlamaLift 的私有内测预发布版本。本版本完成产品品牌升级，并加入系统与 llama-server 实时性能监测、启动命令预检、API Key 管理和 Apple 风格桌面界面。

## 主要更新

- 应用、窗口、托盘、日志、便携包和安装包统一使用 LlamaLift 品牌。
- 新增实时系统监测：CPU、内存、GPU、显存、磁盘、网络以及 llama-server 进程资源。
- 新增实时模型监测：预填充/生成速度、累计 Token、处理/排队请求、并发槽位、上下文使用量与高水位。
- 接入 llama.cpp `/metrics` 与 `/slots`，默认启用 `--metrics`，旧配置自动迁移。
- 自定义启动命令支持双向同步、未知参数保留和保存前非阻断式预检。
- 新增 API Key 创建、生成、导入、脱敏预览、选择和删除功能。
- 完成浅色/深色 Apple 风格 UI、响应式布局与 Per-Monitor V2 高 DPI 适配。

## 验证

- 59 项离线功能测试通过。
- 21 个 UI 场景通过，覆盖 940×600、1320×840、浅色/深色及 125%/150%/175%/200% DPI。
- 便携版和 Inno Setup 安装版构建通过。

## 发布文件

- `LlamaLift-v0.3.0-dev-portable-win-x64.zip`
- `LlamaLift-v0.3.0-dev-Setup.exe`

## 内测提示

- 当前构建尚未进行 Authenticode 数字签名，Windows SmartScreen 可能显示未知发布者提示。
- 这是私有内测预发布版本，不建议直接用于生产环境。
- 升级时会兼容读取旧版 LlamaServerManager 配置目录。
