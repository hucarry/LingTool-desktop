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

export interface PythonPackageItem {
  name: string
  version: string
}

export interface TerminalInfo {
  terminalId: string
  title?: string
  shell: string
  cwd: string
  status: TerminalState
  startTime: string
  endTime?: string
  exitCode?: number
  pid?: number
}
