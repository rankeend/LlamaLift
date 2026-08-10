Llama Server Manager v0.2.0-dev
===============================

状态：私有内测版，暂不公开发布或分发。

这是一个面向 Windows llama.cpp 用户的桌面服务管理器。它可以从 ggml-org/llama.cpp
官方 Release 安装 Windows 运行时，并根据本机硬件、GGUF 模型和快速/均衡/极限目标
生成推荐参数。软件不包含模型文件或 API Key。

快速开始
--------
1. 运行 LlamaServerManager.exe。
2. 打开“运行环境”，检测硬件并安装所需 llama.cpp 官方版本。
3. 打开“模型配置”，选择 GGUF 模型。
4. 选择快速、均衡或极限，生成并确认自适应方案。
5. 检测后端、保存配置，然后点击“启动服务”。

网络安全
--------
- 默认监听 127.0.0.1，仅允许本机访问。
- 若改为 0.0.0.0，请自行设置 Windows 防火墙并配置 API Key。
- 程序不会修改防火墙、网络类别、系统代理或模型文件。
- 程序不会读取、显示或上传 API Key 内容。
- llama.cpp 运行时只从 GitHub 官方 HTTPS 地址下载，并在安装前进行安全检查。

数据位置
--------
- 安装版：%LOCALAPPDATA%\LlamaServerManager
- 便携版：程序目录下的 data 文件夹
- 删除模型配置不会删除 GGUF、mmproj 或 llama.cpp 文件。

兼容性
------
- Windows 10/11 x64
- .NET Framework 4.8
- 联网安装需要能够访问 GitHub，也可手工选择已有 llama-server.exe
- 支持官方 Windows x64 CPU、CUDA、Vulkan、SYCL 和 HIP 构建

第三方组件和许可证信息见 THIRD-PARTY-NOTICES.txt。
