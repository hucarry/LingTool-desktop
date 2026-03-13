# ToolHub - 本地工具管理平台 (Photino.NET + Vue3)

一个本地桌面工具管理平台：在一个窗口中展示并运行本机工具，当前支持 `python`、`node`、`command`、`executable`、`url` 五类工具，支持动态参数输入、并发运行，以及交互式终端。

## 技术栈

- .NET 8
- Photino.NET（WebView 桌面壳，不启动本地 HTTP 端口）
- Vue 3 + Vite + TypeScript
- JS <-> C# 通信：Photino WebMessage

## 项目结构

```text
.
├─ ToolHub.sln
├─ tools.json
├─ build.ps1
├─ README.md
├─ frontend/
│  ├─ package.json
│  ├─ vite.config.ts
│  └─ src/
│     ├─ main.ts
│     ├─ App.vue
│     ├─ style.css
│     ├─ types.ts
│     ├─ services/
│     │  └─ bridge.ts
│     └─ components/
│        ├─ ToolList.vue
│        ├─ ToolRunner.vue
│        ├─ PythonPackagePanel.vue
│        └─ TerminalPanel.vue
└─ ToolHub.App/
   ├─ ToolHub.App.csproj
   ├─ Program.cs
   ├─ ToolRegistry.cs
   ├─ ProcessManager.cs
   ├─ PythonPackageManager.cs
   ├─ TerminalManager.cs
   ├─ Models/
   │  ├─ ToolItem.cs
   │  ├─ RunStatus.cs
   │  ├─ PythonPackage.cs
   │  ├─ TerminalInfo.cs
   │  └─ Messages.cs
   ├─ Utils/
   │  ├─ ArgTemplate.cs
   │  ├─ PathUtils.cs
   │  └─ ProcessKiller.cs
   └─ wwwroot/                # 前端构建产物输出目录
```

## 环境要求

- Windows 10/11
- .NET SDK 8.x
- Node.js 18+
- npm 9+

## 首次运行

### 1) 构建前端（输出到后端 `wwwroot`）

```powershell
cd frontend
npm install
npm run build
```

### 2) 运行桌面应用

```powershell
cd ..
dotnet run --project ToolHub.App/ToolHub.App.csproj
```

## 一键脚本

根目录提供 `build.ps1`：

```powershell
./build.ps1
```

它会执行：
1. 前端 `npm install && npm run build`
2. 后端 `dotnet build`
3. 启动 `dotnet run`

仅构建不启动：

```powershell
./build.ps1 -NoRun
```

## tools.json 配置

根目录 `tools.json` 是工具注册表。示例：

```json
{
  "tools": [
    {
      "id": "demo_py",
      "name": "示例 Python 工具",
      "type": "python",
      "path": "D:/tools/demo.py",
      "runtimePath": "D:/tools/venv/Scripts/python.exe",
      "cwd": "D:/tools",
      "argsTemplate": "--date {date} --mode {mode}",
      "tags": ["数据"]
    },
    {
      "id": "demo_node",
      "name": "示例 Node 工具",
      "type": "node",
      "path": "D:/tools/server.mjs",
      "runtimePath": "C:/Program Files/nodejs/node.exe",
      "cwd": "D:/tools",
      "argsTemplate": "--port {port}",
      "tags": ["服务"]
    },
    {
      "id": "demo_cmd",
      "name": "示例命令工具",
      "type": "command",
      "path": "npm",
      "cwd": "D:/project/frontend",
      "argsTemplate": "run dev",
      "tags": ["前端"]
    },
    {
      "id": "demo_exe",
      "name": "示例可执行工具",
      "type": "executable",
      "path": "D:/tools/demo.exe",
      "cwd": "D:/tools",
      "argsTemplate": "",
      "tags": ["系统"]
    },
    {
      "id": "docs",
      "name": "项目文档",
      "type": "url",
      "path": "https://example.com/docs",
      "tags": ["文档"]
    }
  ]
}
```

说明：
- `type=python|node`：`runtimePath` 可选；留空时回退到工具默认、应用默认或系统运行时
- `type=command`：`path` 填命令名，例如 `git`、`npm`、`docker`
- `type=executable`：`path` 填本地可执行文件路径
- `type=url`：`path` 仅支持 `http://` 或 `https://`，运行时会交给系统默认浏览器
- `argsTemplate`：支持 `{placeholder}` 占位符，前端自动生成动态表单
- `cwd`：`python` / `node` / `command` / `executable` 可使用；`url` 不需要
- 旧配置中的 `type=exe` 与 `python` 字段会自动兼容读取，并在保存后写回新 schema

## 通信协议

### 前端 -> 后端

- `{ "type": "getTools" }`
- `{ "type": "runTool", "toolId": "...", "args": { "key": "value" }, "runtimePath": "C:/path/runtime.exe" }`
- `{ "type": "runToolInTerminal", "toolId": "...", "args": { "key": "value" }, "runtimePath": "C:/path/runtime.exe", "terminalId": "..." }`
- `{ "type": "openUrlTool", "toolId": "docs" }`
- `{ "type": "stopRun", "runId": "..." }`
- `{ "type": "getRuns" }`
- `{ "type": "browsePython", "defaultPath": "C:/any/folder/or/file", "purpose": "toolRunnerRuntime|addToolRuntime|editToolRuntime|packageManager|settingsDefaultPython" }`
- `{ "type": "browseFile", "defaultPath": "C:/any/folder/or/file", "filter": "js,mjs,cjs", "purpose": "addToolPath|addToolRuntime|editToolPath|editToolRuntime|toolRunnerRuntime|settingsDefaultNode" }`
- `{ "type": "getPythonPackages", "pythonPath": "C:/path/python.exe" }`
- `{ "type": "installPythonPackage", "pythonPath": "C:/path/python.exe", "packageName": "requests" }`
- `{ "type": "getTerminals" }`
- `{ "type": "startTerminal", "shell": "cmd.exe", "cwd": "C:/workdir" }`
- `{ "type": "terminalInput", "terminalId": "...", "data": "dir\\r" }`
- `{ "type": "stopTerminal", "terminalId": "..." }`

### 后端 -> 前端

- `{ "type": "tools", "tools": [...] }`
- `{ "type": "runStarted", "run": {...} }`
- `{ "type": "log", "runId": "...", "channel": "stdout|stderr", "line": "...", "ts": "..." }`
- `{ "type": "runStatus", "run": {...} }`
- `{ "type": "runs", "runs": [...] }`
- `{ "type": "pythonSelected", "path": "C:/path/python.exe", "purpose": "toolRunnerRuntime|addToolRuntime|editToolRuntime|packageManager|settingsDefaultPython" }`
- `{ "type": "fileSelected", "path": "C:/path/node.exe", "purpose": "addToolPath|addToolRuntime|editToolPath|editToolRuntime|toolRunnerRuntime|settingsDefaultNode" }`
- `{ "type": "pythonPackages", "pythonPath": "...", "packages": [{ "name": "pip", "version": "24.3.1" }] }`
- `{ "type": "pythonPackageInstallStatus", "packageName": "requests", "status": "running|succeeded|failed", "pythonPath": "...", "message": "..." }`
- `{ "type": "terminals", "terminals": [...] }`
- `{ "type": "terminalStarted", "terminal": {...} }`
- `{ "type": "terminalOutput", "terminalId": "...", "data": "...", "channel": "stdout|stderr", "ts": "..." }`
- `{ "type": "terminalStatus", "terminal": {...} }`
- `{ "type": "error", "message": "...", "details": any }`

## 核心实现说明

- 不使用 ASP.NET / Kestrel / 本地端口
- 使用 `window.external.sendMessage(JSON.stringify(msg))` 与 `RegisterWebMessageReceivedHandler`
- 后端通过 `SendWebMessage` 推送终端输出与运行状态
- 支持并发运行多个工具（`ConcurrentDictionary<runId, RunContext>`）
- Windows 停止运行使用 `taskkill /PID {pid} /T /F`
- 其他系统使用 `proc.Kill(entireProcessTree: true)`（为未来扩展保留）
- Python 包管理独立保留；Node 这次只提供默认运行时和运行覆盖，不包含包管理页

## 常见问题

### 1) 启动时提示找不到 `wwwroot/index.html`

先执行前端构建：

```powershell
cd frontend
npm install
npm run build
```

### 2) 工具显示“路径异常”

检查 `tools.json` 中的 `path` / `cwd` / `runtimePath` 是否存在且可访问。

### 3) 停止运行后状态显示 failed

当进程被强制终止时，可能返回非 0 退出码；当前实现会优先按“主动停止”标记为 `stopped`。

## 打包建议

- 先执行 `npm run build` 生成 `ToolHub.App/wwwroot`
- 再执行 `dotnet publish ToolHub.App -c Release`
- 发布目录中保留 `tools.json`（可与 exe 同目录或项目根路径）
