import type { ToolType, ToolItem, RunInfo, AddToolPayload, PythonPackageItem, TerminalInfo } from './models'
import {
  BRIDGE_MESSAGE_TYPES,
  type FileSelectionPurpose,
  type LogChannel,
  type PythonPackageAction,
  type PythonPackageInstallStatus,
  type PythonSelectionPurpose,
} from './bridgeMessageTypes'

export interface GetToolsRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.GET_TOOLS
}

export interface GetAppDefaultsRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.GET_APP_DEFAULTS
}

export interface AddToolRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.ADD_TOOL
  tool: AddToolPayload
}

export interface UpdateToolRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.UPDATE_TOOL
  tool: AddToolPayload
}

export interface DeleteToolsRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.DELETE_TOOLS
  toolIds: string[]
}

export interface RunToolRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.RUN_TOOL
  toolId: string
  args: Record<string, string>
  runtimePath?: string
}

export interface RunToolInTerminalRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.RUN_TOOL_IN_TERMINAL
  toolId: string
  args: Record<string, string>
  runtimePath?: string
  terminalId?: string
}

export interface OpenUrlToolRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.OPEN_URL_TOOL
  toolId: string
}

export interface StopRunRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.STOP_RUN
  runId: string
}

export interface GetRunsRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.GET_RUNS
}

export interface BrowsePythonRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.BROWSE_PYTHON
  defaultPath?: string
  purpose?: PythonSelectionPurpose
}

export interface BrowseFileRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.BROWSE_FILE
  defaultPath?: string
  filter?: string
  purpose?: FileSelectionPurpose
}

export interface GetPythonPackagesRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.GET_PYTHON_PACKAGES
  pythonPath?: string
}

export interface InstallPythonPackageRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.INSTALL_PYTHON_PACKAGE
  pythonPath?: string
  packageName: string
}

export interface UninstallPythonPackageRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.UNINSTALL_PYTHON_PACKAGE
  pythonPath?: string
  packageName: string
}

export interface GetTerminalsRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.GET_TERMINALS
}

export interface StartTerminalRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.START_TERMINAL
  title?: string
  shell?: string
  cwd?: string
  toolType?: ToolType
  runtimePath?: string
}

export interface TerminalInputRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.TERMINAL_INPUT
  terminalId: string
  data: string
}

export interface TerminalResizeRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.TERMINAL_RESIZE
  terminalId: string
  cols: number
  rows: number
}

export interface StopTerminalRequest {
  type: typeof BRIDGE_MESSAGE_TYPES.STOP_TERMINAL
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
  type: typeof BRIDGE_MESSAGE_TYPES.TOOLS
  tools: ToolItem[]
}

export interface RunStartedMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.RUN_STARTED
  run: RunInfo
}

export interface RunStatusMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.RUN_STATUS
  run: RunInfo
}

export interface RunsMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.RUNS
  runs: RunInfo[]
}

export interface LogMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.LOG
  runId: string
  channel: LogChannel
  line: string
  ts: string
}

export interface ErrorMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.ERROR
  message: string
  details?: unknown
}

export interface PythonSelectedMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.PYTHON_SELECTED
  path?: string
  purpose?: PythonSelectionPurpose
}

export interface FileSelectedMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.FILE_SELECTED
  path?: string
  purpose?: FileSelectionPurpose
}

export interface ToolAddedMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.TOOL_ADDED
  toolId: string
}

export interface ToolUpdatedMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.TOOL_UPDATED
  toolId: string
}

export interface ToolsDeletedMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.TOOLS_DELETED
  deletedCount: number
}

export interface PythonPackagesMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.PYTHON_PACKAGES
  pythonPath: string
  packages: PythonPackageItem[]
}

export interface PythonPackageInstallStatusMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.PYTHON_PACKAGE_INSTALL_STATUS
  packageName: string
  action: PythonPackageAction
  status: PythonPackageInstallStatus
  pythonPath: string
  message?: string
}

export interface TerminalsMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.TERMINALS
  terminals: TerminalInfo[]
}

export interface TerminalStartedMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.TERMINAL_STARTED
  terminal: TerminalInfo
}

export interface TerminalOutputMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.TERMINAL_OUTPUT
  terminalId: string
  data: string
  channel: LogChannel
  ts: string
}

export interface TerminalStatusMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.TERMINAL_STATUS
  terminal: TerminalInfo
}

export interface AppDefaultsMessage {
  type: typeof BRIDGE_MESSAGE_TYPES.APP_DEFAULTS
  pythonPath?: string
  appRootPath?: string
  desktopPath?: string
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
