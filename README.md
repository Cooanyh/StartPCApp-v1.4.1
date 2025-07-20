# StartPCApp v1.4.1

一个功能强大的Windows系统管理工具，支持全局热键快速关机、定时关机和界面缩放调整。

## ✨ 主要功能

- **Alt+Q**: 一键关机（需确认）
- **Alt+W**: 定时关机（1-1440分钟可选）
- **Alt+E**: 界面缩放调整（75%-200%，6种比例）
- **Alt+R**: 自定义网页URL设置（支持记忆功能）
- **ESC**: 退出程序

## 🚀 快速开始

### 下载安装

#### 方式一：从源码构建（推荐）
1. 克隆本仓库：
   ```bash
   git clone https://github.com/Cooanyh/StartPCApp-v1.4.1.git
   cd StartPCApp-v1.4.1
   ```

2. 确保已安装 [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)

3. 构建应用：
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true -o publish
   ```

4. 运行 `publish\StartPCApp.exe`

#### 方式二：下载预编译版本
请查看 [Releases](https://github.com/Cooanyh/StartPCApp-v1.4.1/releases) 获取最新版本下载信息。

### 使用方法

1. **启动应用**：双击运行 `StartPCApp.exe`
2. **全局热键**：应用启动后，热键在全局范围内生效
3. **系统托盘**：应用会最小化到系统托盘
4. **退出程序**：按 ESC 键或右键托盘图标选择退出

## 📋 系统要求

- **操作系统**: Windows 7/8/8.1/10/11 (x64)
- **内存**: 最低 512MB RAM
- **存储**: 约 100MB 可用空间
- **运行时**: 支持 WebView2 的系统
- **权限**: 管理员权限（用于关机功能）

## 🔧 技术特性

- **.NET 6**: 现代化的跨平台框架
- **WebView2**: 内嵌现代浏览器引擎
- **全局热键**: 系统级热键监听
- **高DPI支持**: 适配高分辨率显示器
- **安全设计**: 关机操作需要确认

## 🐛 故障排除

### 常见问题

1. **热键不响应**
   - 确保以管理员权限运行
   - 检查是否有其他程序占用相同热键

2. **关机失败**
   - 确认具有管理员权限
   - 检查系统是否有阻止关机的进程

3. **界面显示异常**
   - 尝试使用 Alt+E 调整缩放比例
   - 检查系统DPI设置

4. **WebView2错误**
   - 安装最新版本的 Microsoft Edge
   - 或下载 WebView2 Runtime

### 获取帮助

如果遇到问题，请：
1. 搜索现有的 [Issues](https://github.com/Cooanyh/StartPCApp-v1.4.1/issues)
2. 创建新的 Issue 描述问题

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

1. Fork 本仓库
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建 Pull Request

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 🏷️ 版本历史

- **v1.4.1** (最新)
  - 修复 WebView2 初始化错误 (拒绝访问问题)
  - 修复热键注册失败问题
  - 改进错误处理和用户提示
  - 添加 WebView2 运行时检测
  - 优化程序启动流程

- **v1.4**
  - 新增 Alt+R 自定义网页功能
  - 新增设置自动保存功能
  - 优化界面缩放记忆功能
  - 改进用户体验

- **v1.3**
  - 新增 Alt+E 缩放调整功能
  - 高DPI优化
  - WebView缩放同步
  - 界面显示改进

- **v1.2**
  - 定时关机功能优化
  - 稳定性改进

- **v1.1**
  - 添加定时关机功能
  - 系统托盘支持

- **v1.0**
  - 基础关机功能
  - 全局热键支持

## 📞 联系方式

- **项目主页**: [GitHub Repository](https://github.com/Cooanyh/StartPCApp-v1.4.1)
- **问题反馈**: [GitHub Issues](https://github.com/Cooanyh/StartPCApp-v1.4.1/issues)
- **开发团队**: StartPCApp Team

---

**如果这个项目对你有帮助，请给它一个 ⭐️！**