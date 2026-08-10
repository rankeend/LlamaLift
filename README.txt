Llama Server Manager v0.1.0-internal
====================================

状态：私有内测版，暂不公开发布或分发。

这是一个面向 Windows llama.cpp 用户的桌面服务管理器。它只负责管理用户自行提供的
llama-server.exe、GGUF 模型、启动参数、API 检测和运行日志，不包含 llama.cpp、
CUDA、模型文件或 API Key。

快速开始
--------
1. 运行 LlamaServerManager.exe。
2. 打开“模型配置”，依次选择 llama-server.exe 和 GGUF 模型。
3. 可按需要选择 mmproj、API Key 文件并调整上下文、GPU 层数等参数。
4. 点击“检测后端”，确认 llama.cpp 可以运行。
5. 保存配置，然后点击“启动服务”。

网络安全
--------
- 默认监听 127.0.0.1，仅允许本机访问。
- 若改为 0.0.0.0，请自行设置 Windows 防火墙并配置 API Key。
- 程序不会修改防火墙、网络类别、系统代理或模型文件。
- 程序不会读取、显示或上传 API Key 内容。

数据位置
--------
- 安装版：%LOCALAPPDATA%\LlamaServerManager
- 便携版：程序目录下的 data 文件夹
- 删除模型配置不会删除 GGUF、mmproj 或 llama.cpp 文件。

兼容性
------
- Windows 10/11 x64
- .NET Framework 4.8
- 支持对应命令行参数的 llama.cpp / llama-server 版本
- NVIDIA、AMD、Intel 和 CPU 能力由用户选择的 llama.cpp 构建决定

第三方组件和许可证信息见 THIRD-PARTY-NOTICES.txt。
