export type ToolType = 'python' | 'node' | 'command' | 'executable' | 'url'
export type RunState = 'running' | 'exited' | 'stopped' | 'failed'
export type TerminalState = 'running' | 'exited' | 'stopped' | 'failed'

export interface ToolItem {
  id: string
  name: string
  type: ToolType
  path: string
  runtimePath?: string
  cwd?: string
  argsTemplate: string
  tags: string[]
  description?: string
  pathExists: boolean
  valid: boolean
  validationMessage?: string
}

export interface RunInfo {
  runId: string
  toolId: string
  toolName: string
  status: RunState
  startTime: string
  endTime?: string
  exitCode?: number
  pid?: number
}

export interface LogEntry {
  runId: string
  channel: 'stdout' | 'stderr'
  line: string
  ts: string
}

export interface GetToolsRequest {
  type: 'getTools'
}

export interface GetAppDefaultsRequest {
  type: 'getAppDefaults'
}

export interface AddToolPayload {
  id: string
  name: string
  type: ToolType
  path: string
  runtimePath?: string
  cwd?: string
  argsTemplate?: string
  tags?: string[]
  description?: string
}

export interface AddToolRequest {
  type: 'addTool'
  tool: AddToolPayload
}

export interface UpdateToolRequest {
  type: 'updateTool'
  tool: AddToolPayload
}

export interface DeleteToolsRequest {
  type: 'deleteTools'
  toolIds: string[]
}

export interface RunToolRequest {
  type: 'runTool'
  toolId: string
  args: Record<string, string>
  runtimePath?: string
}

export interface RunToolInTerminalRequest {
  type: 'runToolInTerminal'
  toolId: string
  args: Record<string, string>
  runtimePath?: string
  terminalId?: string
}

export interface OpenUrlToolRequest {
  type: 'openUrlTool'
  toolId: string
}

export interface StopRunRequest {
  type: 'stopRun'
  runId: string
}

export interface GetRunsRequest {
  type: 'getRuns'
}

export interface BrowsePythonRequest {
  type: 'browsePython'
  defaultPath?: string
  purpose?: 'toolRunnerRuntime' | 'addToolRuntime' | 'editToolRuntime' | 'packageManager' | 'settingsDefaultPython'
}

export interface BrowseFileRequest {
  type: 'browseFile'
  defaultPath?: string
  filter?: string
  purpose?: 'addToolPath' | 'addToolRuntime' | 'editToolPath' | 'editToolRuntime' | 'toolRunnerRuntime' | 'settingsDefaultNode'
}

export interface GetPythonPackagesRequest {
  type: 'getPythonPackages'
  pythonPath?: string
}

export interface InstallPythonPackageRequest {
  type: 'installPythonPackage'
  pythonPath?: string
  packageName: string
}

export interface UninstallPythonPackageRequest {
  type: 'uninstallPythonPackage'
  pythonPath?: string
  packageName: string
}

export interface GetTerminalsRequest {
  type: 'getTerminals'
}

export interface StartTerminalRequest {
  type: 'startTerminal'
  shell?: string
  cwd?: string
}

export interface TerminalInputRequest {
  type: 'terminalInput'
  terminalId: string
  data: string
}

export interface TerminalResizeRequest {
  type: 'terminalResize'
  terminalId: string
  cols: number
  rows: number
}

export interface StopTerminalRequest {
  type: 'stopTerminal'
  terminalId: string
}

export type FrontMessage =
  | GetAppDefaultsRequest
  | GetToolsRequest
  | AddToolRequest
  | UpdateToolRequest
  | DeleteToolsRequest
  | RunToolRequest
  | RunToolInTerminalRequest
  | OpenUrlToolRequest
  | StopRunRequest
  | GetRunsRequest
  | BrowsePythonRequest
  | BrowseFileRequest
  | GetPythonPackagesRequest
  | InstallPythonPackageRequest
  | UninstallPythonPackageRequest
  | GetTerminalsRequest
  | StartTerminalRequest
  | TerminalInputRequest
  | TerminalResizeRequest
  | StopTerminalRequest

export interface ToolsMessage {
  type: 'tools'
  tools: ToolItem[]
}

export interface RunStartedMessage {
  type: 'runStarted'
  run: RunInfo
}

export interface RunStatusMessage {
  type: 'runStatus'
  run: RunInfo
}

export interface RunsMessage {
  type: 'runs'
  runs: RunInfo[]
}

export interface LogMessage {
  type: 'log'
  runId: string
  channel: 'stdout' | 'stderr'
  line: string
  ts: string
}

export interface ErrorMessage {
  type: 'error'
  message: string
  details?: unknown
}

export interface PythonSelectedMessage {
  type: 'pythonSelected'
  path?: string
  purpose?: 'toolRunnerRuntime' | 'addToolRuntime' | 'editToolRuntime' | 'packageManager' | 'settingsDefaultPython'
}

export interface FileSelectedMessage {
  type: 'fileSelected'
  path?: string
  purpose?: 'addToolPath' | 'addToolRuntime' | 'editToolPath' | 'editToolRuntime' | 'toolRunnerRuntime' | 'settingsDefaultNode'
}

export interface ToolAddedMessage {
  type: 'toolAdded'
  toolId: string
}

export interface ToolUpdatedMessage {
  type: 'toolUpdated'
  toolId: string
}

export interface ToolsDeletedMessage {
  type: 'toolsDeleted'
  deletedCount: number
}

export interface PythonPackageItem {
  name: string
  version: string
}

export interface PythonPackagesMessage {
  type: 'pythonPackages'
  pythonPath: string
  packages: PythonPackageItem[]
}

export interface PythonPackageInstallStatusMessage {
  type: 'pythonPackageInstallStatus'
  packageName: string
  action: 'install' | 'uninstall'
  status: 'running' | 'succeeded' | 'failed'
  pythonPath: string
  message?: string
}

export interface TerminalInfo {
  terminalId: string
  shell: string
  cwd: string
  status: TerminalState
  startTime: string
  endTime?: string
  exitCode?: number
  pid?: number
}

export interface TerminalsMessage {
  type: 'terminals'
  terminals: TerminalInfo[]
}

export interface TerminalStartedMessage {
  type: 'terminalStarted'
  terminal: TerminalInfo
}

export interface TerminalOutputMessage {
  type: 'terminalOutput'
  terminalId: string
  data: string
  channel: 'stdout' | 'stderr'
  ts: string
}

export interface TerminalStatusMessage {
  type: 'terminalStatus'
  terminal: TerminalInfo
}

export interface AppDefaultsMessage {
  type: 'appDefaults'
  pythonPath?: string
}

export type BackMessage =
  | AppDefaultsMessage
  | ToolsMessage
  | ToolAddedMessage
  | ToolUpdatedMessage
  | ToolsDeletedMessage
  | RunStartedMessage
  | RunStatusMessage
  | RunsMessage
  | LogMessage
  | PythonSelectedMessage
  | FileSelectedMessage
  | PythonPackagesMessage
  | PythonPackageInstallStatusMessage
  | TerminalsMessage
  | TerminalStartedMessage
  | TerminalOutputMessage
  | TerminalStatusMessage
  | ErrorMessage
