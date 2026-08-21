LlamaLift v1.1.0-preview
=====================

本地模型，一键起飞。

状态：Preview 公开测试版。

这是一个面向 Windows llama.cpp 用户的桌面服务管理器。它可以从 ggml-org/llama.cpp
官方 Release 安装 Windows 运行时，并根据本机硬件、GGUF 模型和快速/均衡/极限目标
生成推荐参数。软件不包含模型文件或 API Key。

快速开始
--------
1. 运行 LlamaLift.exe。
2. 打开“运行环境”，检测硬件并安装所需 llama.cpp 官方版本。
3. 打开“模型配置”，选择 GGUF 模型。
4. 选择快速、均衡或极限，生成并确认自适应方案。
5. 进阶用户可在“参数工作台”编辑完整命令；保存时会自动预检并给出建议，风险提示不会强制阻止保存。
6. 可在“模型配置”或“外观与设置”中管理 API Key，列表默认只显示脱敏摘要。
7. 检测后端、保存配置，然后点击“启动服务”。“性能监测”页面可实时查看系统与模型指标。
8. 根据接入软件选择 Responses、Chat Completions 或 Anthropic Messages；可测试当前协议或一次检查全部协议。

协议与状态
----------
- Responses：Base URL 以 /v1 结尾，请求端点 /v1/responses，使用 Bearer 鉴权。
- Chat Completions：Base URL 以 /v1 结尾，请求端点 /v1/chat/completions，使用 Bearer 鉴权。
- Anthropic Messages：Base URL 使用主机根地址，请求端点 /v1/messages，使用 x-api-key 鉴权。
- 切换协议不会改变 llama-server 启动命令；侧栏会实时显示已关闭、加载中、已就绪、输出中或异常。
- 新生成的托管密钥格式为 sk-llamalift- 加 64 位十六进制随机内容。

实时监测
--------
- 系统：CPU、内存、GPU、独占/共享显存、服务进程、磁盘与网络吞吐。
- 模型：预填充/生成速度、累计 tokens、并发、排队、槽位、上下文和运行时长。
- 简易配置默认启用 llama-server --metrics；图表数据只在本机读取和显示。

网络安全
--------
- 默认监听 127.0.0.1，仅允许本机访问。
- 若改为 0.0.0.0，请自行设置 Windows 防火墙并配置 API Key。
- 程序不会修改防火墙、网络类别、系统代理或模型文件。
- 程序不会上传或记录 API Key；只有在用户主动管理时才读取本地文件，默认脱敏，点击“显示”后才展示明文。
- 托管 Key 以 llama.cpp 所需的本地文本文件保存并继承所在目录权限；请勿共享 data/api-keys 或把密钥打包发布。含中文的 Key 路径会在启动时自动使用临时兼容路径。
- llama.cpp 运行时只从 GitHub 官方 HTTPS 地址下载，并在安装前进行安全检查。

数据位置
--------
- 新安装版：%LOCALAPPDATA%\LlamaLift（旧内测配置目录仍可继续读取）
- 便携版：程序目录下的 data 文件夹
- 安装包同时支持首次安装和原地更新；更新无需卸载旧版，并会保留上述配置、API Key 和运行时登记。
- 删除模型配置不会删除 GGUF、mmproj 或 llama.cpp 文件。

兼容性
------
- Windows 10/11 x64
- .NET Framework 4.8
- 联网安装需要能够访问 GitHub，也可手工选择已有 llama-server.exe
- 支持官方 Windows x64 CPU、CUDA、Vulkan、SYCL 和 HIP 构建

第三方组件和许可证信息见 THIRD-PARTY-NOTICES.txt。

项目成员
--------
- 作者与维护者：RankeeNd-Masen Hu
- 项目支持者：Hongbin Sun
- 完整信息见 AUTHORS.md
