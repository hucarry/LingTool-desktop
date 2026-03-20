# ToolHub Desktop

本项目是一个面向 Windows 的本地工具管理平台，用于统一管理和运行 Python、Node、命令行、可执行文件与 URL 工具。桌面宿主基于 `Photino.NET`，前端基于 `Vue 3 + Vite + Pinia`，工具配置通过根目录 `tools.json` 管理。

## 项目定位

- 统一管理本地开发工具、脚本和常用命令入口
- 提供桌面化运行入口，而不是依赖单独的本地 Web 服务
- 支持直接运行与终端运行两种模式
- 支持 Python 包管理与内置终端工作区

## 技术栈

- `.NET 8`
- `Photino.NET`
- `Vue 3`
- `TypeScript`
- `Pinia`
- `Vite`
- `xterm.js`

## 功能概览

- 支持工具类型：`python`、`node`、`command`、`executable`、`url`
- 支持通过 `tools.json` 维护工具定义
- 支持 `argsTemplate` 动态参数模板
- 支持直接运行工具并查看输出日志
- 支持在内置终端中运行工具并保留交互
- 支持多终端、分屏终端与终端状态管理
- 支持选择默认 Python 路径与默认 Node 路径
- 支持查看、安装、卸载 Python 包

## 环境要求

- Windows 10 或 Windows 11
- `.NET SDK 8.x`
- `Node.js 18+`
- `npm 9+`

## 快速开始

### 1. 安装前端依赖并构建

```powershell
cd frontend
npm install
npm run build
```

### 2. 初始化本地工具配置

仓库中提交的是示例配置 [`tools.example.json`](./tools.example.json)。首次使用时复制为本地配置：

```powershell
Copy-Item .\tools.example.json .\tools.json
```

`tools.json` 已加入 `.gitignore`，适合保存本机路径和私有工具配置。

### 3. 启动桌面应用

```powershell
dotnet run --project .\ToolHub.App\ToolHub.App.csproj
```

## 一键构建

根目录提供了 [`build.ps1`](./build.ps1)，默认会执行以下流程：

1. 前端安装依赖并构建
2. 后端编译
3. 宿主回归检查
4. 启动桌面应用

直接执行：

```powershell
.\build.ps1
```

仅构建，不启动应用：

```powershell
.\build.ps1 -NoRun
```

跳过前端安装：

```powershell
.\build.ps1 -NoFrontendInstall
```

跳过宿主回归：

```powershell
.\build.ps1 -NoHostRegression
```

## 宿主回归测试

项目现在包含两套宿主测试入口：

- 标准测试工程：[`ToolHub.App.Tests/ToolHub.App.Tests.csproj`](./ToolHub.App.Tests/ToolHub.App.Tests.csproj)
- 离线回归入口：[`ToolHub.App.Tests/run-tests.ps1`](./ToolHub.App.Tests/run-tests.ps1)

其中离线脚本会编译当前 `ToolHub.App` 源码并执行回归检查，当前已覆盖：

- 协议常量稳定性
- 运行时覆盖解析
- `ToolRegistry` 配置投影
- `RunnableTool` 投影边界
- 工具运行时解析
- `MessageRouter` 错误边界
- `ProcessManager.BuildStartInfo` 命令拼装
- `TerminalManager.BuildRunCommand` Shell 拼装

手动执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ToolHub.App.Tests\run-tests.ps1
```

标准 `dotnet test`：
```powershell
dotnet test .\ToolHub.App.Tests\ToolHub.App.Tests.csproj
```

## 前端开发与验证

启动前端开发环境：

```powershell
cd frontend
npm install
npm run dev
```

运行前端测试：

```powershell
cd frontend
npm test
```

执行前端生产构建：

```powershell
cd frontend
npm run build
```

前端构建产物会输出到 [`ToolHub.App/wwwroot`](./ToolHub.App/wwwroot)。

## 桥接协议常量生成

前端桥接协议常量不再手工维护两份，当前以宿主侧 [`ToolHub.App/Models/Messages.cs`](./ToolHub.App/Models/Messages.cs) 为单一来源，由生成脚本自动产出前端常量文件。

生成入口：

```powershell
cd frontend
npm run generate:bridge-types
```

生成文件：

- [`frontend/src/types/bridgeMessageTypes.generated.ts`](./frontend/src/types/bridgeMessageTypes.generated.ts)

说明：

- `npm run dev`
- `npm test`
- `npm run build`

以上命令都会先自动执行一次协议常量生成。

维护约束：

- 如需新增或修改宿主桥接消息，请优先修改 [`ToolHub.App/Models/Messages.cs`](./ToolHub.App/Models/Messages.cs)
- 不要直接手改 [`frontend/src/types/bridgeMessageTypes.generated.ts`](./frontend/src/types/bridgeMessageTypes.generated.ts)
- [`frontend/src/types/bridgeMessageTypes.ts`](./frontend/src/types/bridgeMessageTypes.ts) 仅保留前端本地常量与生成结果的统一导出

## 发布

根目录提供了 [`publish.ps1`](./publish.ps1)，默认流程如下：

1. 前端安装与构建
2. 后端 `Debug` 编译
3. 宿主回归检查
4. `dotnet publish`
5. 可选打包便携 Python 与 pip

直接发布：

```powershell
.\publish.ps1
```

常用参数：

```powershell
.\publish.ps1 -Runtime win-x64 -Configuration Release
.\publish.ps1 -NoFrontendInstall
.\publish.ps1 -NoFrontendBuild
.\publish.ps1 -NoHostRegression
.\publish.ps1 -OpenOutput
.\publish.ps1 -NoBundlePortablePython
.\publish.ps1 -NoBundlePortablePip
```

## tools.json 配置

应用运行时读取根目录 `tools.json` 作为工具注册表。

最小示例：

```json
{
  "tools": [
    {
      "id": "demo_py",
      "name": "Python 示例",
      "type": "python",
      "path": "Tools/testdemo.py",
      "cwd": "Tools",
      "argsTemplate": "--date {date}",
      "tags": ["demo", "python"]
    }
  ]
}
```

字段说明：

- `id`：工具唯一标识
- `name`：显示名称
- `type`：工具类型，支持 `python`、`node`、`command`、`executable`、`url`
- `path`：脚本路径、命令名、可执行文件路径或 URL
- `runtimePath`：`python` / `node` 工具的可选运行时路径
- `cwd`：工作目录
- `argsTemplate`：参数模板，支持 `{placeholder}` 占位符
- `tags`：标签列表
- `description`：可选说明

说明：

- 相对路径会按项目根目录或配置基目录解析
- `url` 类型仅支持 `http://` 和 `https://`
- `argsTemplate` 中的占位符会在前端自动生成输入项

## 目录结构

```text
.
├─ ToolHub.sln
├─ build.ps1
├─ publish.ps1
├─ tools.example.json
├─ frontend/
│  ├─ package.json
│  ├─ vite.config.ts
│  └─ src/
├─ ToolHub.App/
│  ├─ Program.cs
│  ├─ MessageRouter.cs
│  ├─ MessageRouting/
│  ├─ ToolRegistry.cs
│  ├─ ProcessManager.cs
│  ├─ TerminalManager.cs
│  ├─ PythonPackageManager.cs
│  ├─ Models/
│  ├─ Utils/
│  └─ wwwroot/
└─ ToolHub.App.Tests/
   ├─ HostRegressionTests.cs
   ├─ HostRegressionSuiteTests.cs
   ├─ ToolHub.App.Tests.csproj
   ├─ ToolHub.GlobalUsings.cs
   └─ run-tests.ps1
```

## 架构说明

- 前端通过 `window.external.sendMessage(...)` 与 Photino 宿主通信
- 宿主通过 `MessageRouter` 和 `MessageRouting/*` 处理消息分发
- `ToolRegistry` 负责工具配置读写、规范化与视图投影
- `ProcessManager` 负责直接运行工具
- `TerminalManager` 负责内置终端与终端运行工具
- `PythonPackageManager` 负责 Python 包查询与安装卸载

当前宿主模型边界为：

- `ToolDefinition`：持久化配置模型
- `ToolItem`：前端回显与校验模型
- `RunnableTool`：运行时最小模型

## 常见问题

### 启动时提示找不到 `wwwroot/index.html`

先执行前端构建：

```powershell
cd frontend
npm install
npm run build
```

### 为什么仓库里没有提交 `tools.json`

因为该文件通常包含本机路径、解释器路径和私有工具配置，不适合直接提交到仓库。

### 工具显示路径异常或不可用

优先检查：

- `path` 是否存在
- `cwd` 是否存在
- `runtimePath` 是否存在且可用
- 是否使用了错误的相对路径基准

## 当前工程化状态

目前项目已具备以下基础门禁：

- 前端测试
- 前端生产构建
- 宿主离线回归
- 构建脚本门禁
- 发布脚本门禁
- GitHub Actions 最小 CI
- 标准 `dotnet test` 工程
- 离线回归兜底入口

下一阶段更适合继续补的是测试分层和更细粒度的宿主单元测试。
