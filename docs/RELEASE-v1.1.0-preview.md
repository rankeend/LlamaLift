# LlamaLift v1.1.0-preview

> 本地模型，一键起飞。

v1.1 Preview 面向公开测试，重点改进本地服务接入信息、聊天模板配置、新手参数说明和全套弹窗体验，并将 Windows 安装包正式升级为“首次安装 / 原地更新”二合一包。

## 主要更新

- 服务首次就绪后自动显示连接信息，集中提供 Provider ID、API 协议、API 地址、API Key、完整模型名称和最大上下文。
- 每张连接信息卡片都可左键直接复制；API Key 默认隐藏，可通过眼睛按钮显隐。
- 模型配置新增聊天模板，支持 llama.cpp 内置模板名和 `.jinja` 模板文件。
- 运行参数名称增加一行式新手悬浮说明。
- 应用内提示、确认、输入、API Key 和连接信息弹窗统一为 LlamaLift 自绘 UI，并使用软件正式羊驼 Logo。

## 安装与更新

`LlamaLift-v1.1.0-preview-Setup.exe` 同时用于首次安装和更新：

- 新用户直接运行即可安装。
- v1.0 或更早安装版用户直接运行即可原地更新，无需卸载旧版。
- 更新复用原安装目录，不覆盖 `%LOCALAPPDATA%\LlamaLift`。
- 模型配置、API Key、运行时登记和日志会保留；外部 GGUF 与 llama.cpp 文件不会被移动或删除。

便携版仍通过 ZIP 分发，数据保存在便携目录的 `data/`。安装版与便携版的数据位置不同，不应使用安装器覆盖便携目录。

## 验证范围

- 115 项离线检查，覆盖聊天模板、连接信息、配置迁移、三协议、API Key、安全解压和服务生命周期。
- 33 个 UI/DPI 场景，覆盖七个主页面、四类弹窗、浅色/深色及 125%–200% 缩放。
- 安装器升级契约自动检查：固定 AppId、旧目录复用、用户数据隔离、便携标记隔离和全链路版本一致性。
- Inno Setup 安装包、便携 ZIP 内容和 SHA-256 摘要构建验证。

## 发布文件

- `LlamaLift-v1.1.0-preview-Setup.exe`
- `LlamaLift-v1.1.0-preview-portable-win-x64.zip`
- `LlamaLift-v1.1.0-preview-SHA256SUMS.txt`

## Preview 提示

- 当前构建尚未进行 Authenticode 数字签名，Windows SmartScreen 可能显示“未知发布者”。
- 建议升级前停止正在运行的本地模型服务；安装器会在替换程序文件前请求关闭 LlamaLift。
- llama.cpp 的聊天模板和 API 端点能力取决于用户安装的上游版本。

## 项目成员

- 作者与维护者：RankeeNd-Masen Hu
- 项目支持者：Hongbin Sun
