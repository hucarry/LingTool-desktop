# LingTool Desktop

<p align="center">
  一个面向 Windows 的本地工具管理平台，用桌面应用的方式统一管理和运行常用脚本、命令与程序。
</p>

<p align="center">
  <a href="https://github.com/hucarry/LingTool-desktop"><img src="https://img.shields.io/github/stars/hucarry/LingTool-desktop?style=flat-square" alt="GitHub stars" /></a>
  <a href="https://github.com/hucarry/LingTool-desktop/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-MIT-green?style=flat-square" alt="MIT License" /></a>
  <img src="https://img.shields.io/badge/platform-Windows-0078D6?style=flat-square" alt="Platform Windows" />
  <img src="https://img.shields.io/badge/.NET-8-512BD4?style=flat-square" alt=".NET 8" />
  <img src="https://img.shields.io/badge/Vue-3-42B883?style=flat-square" alt="Vue 3" />
  <img src="https://img.shields.io/badge/Photino.NET-Desktop-4B5563?style=flat-square" alt="Photino.NET" />
</p>

> 把零散的 Python、Node.js、命令行、可执行程序和网址入口，收拢到一个统一的桌面工作台中。

LingTool Desktop 基于 Photino.NET 和 Vue 3 构建，不依赖本地 HTTP 服务，适合整理个人开发工具、自动化脚本、内部小工具和常用命令入口。

## 目录

- [项目亮点](#项目亮点)
- [技术栈](#技术栈)
- [适用场景](#适用场景)
- [快速开始](#快速开始)
- [一键开发脚本](#一键开发脚本)
- [开发命令](#开发命令)
- [tools.json 配置](#toolsjson-配置)
- [主要功能](#主要功能)
- [项目结构](#项目结构)
- [架构说明](#架构说明)
- [打包发布](#打包发布)
- [常见问题](#常见问题)
- [后续方向](#后续方向)

## 项目亮点

- 支持多种工具类型：`python`、`node`、`command`、`executable`、`url`
- 通过 `tools.json` 统一管理工具定义、参数模板、工作目录和运行时路径
- 支持动态参数输入，可根据 `argsTemplate` 自动生成表单
- 内置终端面板，支持多会话、分屏和运行输出查看
- 支持 Python 包管理，可查看、安装和卸载当前解释器的包
- 支持默认 Python / Node 运行时设置，减少每个工具重复配置
- 所有工具都在本机运行，适合本地开发与个人效率场景

## 技术栈

- .NET 8
- Photino.NET
- Vue 3
- Vite
- TypeScript
- Pinia
- xterm.js

## 适用场景

- 把零散的 Python / Node / EXE / 命令行工具集中管理
- 为常用脚本提供统一入口，减少手动切目录和敲命令
- 给内部工具或个人工具做一个轻量桌面壳
- 在同一个界面里管理运行状态、终端输出和 Python 依赖

## 快速开始

### 环境要求

- Windows 10 / 11
- .NET SDK 8.x
- Node.js 18+
- npm 9+

### 1. 安装前端依赖并构建

```powershell
cd frontend
npm install
npm run build
```

### 2. 初始化本地工具配置

仓库中提交的是公开示例配置 [`tools.example.json`](./tools.example.json)，首次使用前请复制为本地私有配置：

```powershell
cd ..
Copy-Item tools.example.json tools.json
```

`tools.json` 已加入 `.gitignore`，适合保存你自己的本机路径、解释器路径和命令参数，不会上传到 GitHub。

### 3. 启动桌面应用

```powershell
dotnet run --project ToolHub.App/ToolHub.App.csproj
```

如果根目录不存在 `tools.json`，应用会优先尝试从 `tools.example.json` 自动生成一份默认配置。

## 一键开发脚本

根目录提供 [`build.ps1`](./build.ps1)，用于快速完成前端构建、后端编译和应用启动：

```powershell
./build.ps1
```

它会执行：

1. `cd frontend && npm install && npm run build`
2. `dotnet build ToolHub.App/ToolHub.App.csproj`
3. `dotnet run --project ToolHub.App/ToolHub.App.csproj`

只构建不启动：

```powershell
./build.ps1 -NoRun
```

## 开发命令

### 前端开发

```powershell
cd frontend
npm install
npm run dev
```

### 前端构建

```powershell
cd frontend
npm run build
```

### 前端测试

```powershell
cd frontend
npm test
```

### 后端运行

```powershell
dotnet run --project ToolHub.App/ToolHub.App.csproj
```

## tools.json 配置

应用运行时读取根目录 `tools.json` 作为工具注册表。建议以 [`tools.example.json`](./tools.example.json) 为起点，按你的本机环境修改。

最小示例：

```json
{
  "tools": [
    {
      "id": "demo_py",
      "name": "项目内 Python 示例",
      "type": "python",
      "path": "Tools/testdemo.py",
      "cwd": "Tools",
      "argsTemplate": "",
      "tags": ["示例", "Python"]
    }
  ]
}
```

### 字段说明

- `id`：工具唯一标识，建议使用字母、数字、`.`、`_`、`-`
- `name`：工具显示名称
- `type`：工具类型，支持 `python`、`node`、`command`、`executable`、`url`
- `path`：脚本路径、命令名、可执行文件路径或 URL
- `runtimePath`：`python` / `node` 工具可选，指定解释器或运行时
- `cwd`：工具启动时的工作目录
- `argsTemplate`：参数模板，支持 `{placeholder}` 占位符
- `tags`：工具标签
- `description`：工具描述

### 类型说明

- `python`：运行 Python 脚本，可选指定 `runtimePath`
- `node`：运行 Node 脚本，可选指定 `runtimePath`
- `command`：运行系统命令，例如 `npm`、`git`、`docker`
- `executable`：运行本地可执行文件
- `url`：打开网页链接，仅支持 `http://` 和 `https://`

### 参数模板示例

```json
{
  "id": "report",
  "name": "日报生成",
  "type": "python",
  "path": "Tools/report.py",
  "cwd": "Tools",
  "argsTemplate": "--date {date} --mode {mode}"
}
```

上面的模板会在前端生成 `date` 和 `mode` 两个输入项，运行时自动拼接为命令参数。

## 主要功能

### 工具管理

- 查看工具列表和状态
- 新增、编辑、删除工具
- 支持 URL 工具直接调用系统浏览器
- 支持从文件选择器选择脚本、运行时和可执行文件

### 运行与终端

- 直接运行工具并查看输出
- 在终端中运行工具，保留交互能力
- 支持多终端会话
- 支持分屏终端
- 支持停止运行与清空输出

### Python 环境管理

- 选择 Python 解释器
- 查看已安装包
- 安装 / 卸载包
- 支持使用系统 Python

### 设置

- 切换浅色 / 深色主题
- 配置默认 Python 路径
- 配置默认 Node 路径

## 项目结构

```text
.
├─ ToolHub.sln
├─ tools.example.json
├─ build.ps1
├─ publish.ps1
├─ README.md
├─ frontend/
│  ├─ package.json
│  ├─ vite.config.ts
│  ├─ scripts/
│  └─ src/
│     ├─ components/
│     ├─ composables/
│     ├─ locales/
│     ├─ router/
│     ├─ stores/
│     └─ views/
└─ ToolHub.App/
   ├─ Program.cs
   ├─ MessageRouter.cs
   ├─ ToolRegistry.cs
   ├─ ProcessManager.cs
   ├─ PythonPackageManager.cs
   ├─ TerminalManager.cs
   ├─ Models/
   ├─ Utils/
   └─ wwwroot/
```

## 架构说明

- 前端通过 `window.external.sendMessage(JSON.stringify(msg))` 向 Photino 宿主发送消息
- 后端通过 `RegisterWebMessageReceivedHandler` 接收消息并交由 `MessageRouter` 分发
- 工具注册由 `ToolRegistry` 负责，配置文件来源于根目录 `tools.json`
- 工具进程由 `ProcessManager` 管理
- 终端会话由 `TerminalManager` 管理
- Python 包管理由 `PythonPackageManager` 处理
- 前端构建产物输出到 `ToolHub.App/wwwroot`

## 打包发布

项目提供 [`publish.ps1`](./publish.ps1) 用于一键发布桌面应用。

### 默认发布

```powershell
./publish.ps1
```

### 常用参数

```powershell
./publish.ps1 -Runtime win-x64 -Configuration Release
./publish.ps1 -NoFrontendInstall
./publish.ps1 -NoFrontendBuild
./publish.ps1 -OpenOutput
```

### 发布说明

- 默认输出目录为 `publish/win-x64`
- 脚本会优先构建前端，再执行 `dotnet publish`
- 可选打包便携版 Python 和 pip
- 发布目录中请保留你自己的 `tools.json`

## 常见问题

### 启动时提示找不到 `wwwroot/index.html`

先执行前端构建：

```powershell
cd frontend
npm install
npm run build
```

### 工具显示“路径异常”

检查 `tools.json` 中的 `path`、`cwd`、`runtimePath` 是否存在且可访问。

### 为什么仓库里没有 `tools.json`

因为它通常包含本机路径和私有运行环境，不适合公开提交。仓库只保留 `tools.example.json` 作为示例模板。

### 停止运行后状态显示 `failed`

部分进程被强制终止时会返回非 0 退出码；当前实现会优先按“主动停止”标记为 `stopped`，但底层进程可能仍返回失败码。

## 后续方向

- 更完整的多语言文案整理
- 更细的工具分类与搜索能力
- 更多运行时支持
- 更完善的发布产物说明与自动化发布流程
