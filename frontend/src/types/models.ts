export type ToolType = 'python' | 'node' | 'command' | 'executable' | 'url'
export type RunState = 'running' | 'exited' | 'stopped' | 'failed'
export type TerminalState = 'running' | 'exited' | 'stopped' | 'failed'
export type ArgFieldKind = 'text' | 'path' | 'number' | 'flag' | 'select' | 'secret'
export type ArgEditorMode = 'legacy' | 'structured'

export interface ArgFieldOption {
  label: string
  value: string
}

export interface ArgFieldSpec {
  name: string
  label?: string
  description?: string
  kind: ArgFieldKind
  required?: boolean
  defaultValue?: string
  placeholder?: string
  options?: ArgFieldOption[]
}

export type ArgTokenSpec =
  | { kind: 'literal'; value: string }
  | { kind: 'field'; field: string; prefix?: string; suffix?: string; omitWhenEmpty?: boolean }
  | { kind: 'switch'; field: string; whenTrue: string; whenFalse?: string }

export interface ArgsSpecV1 {
  version: 1
  fields: ArgFieldSpec[]
  argv: ArgTokenSpec[]
}

export interface ToolItem {
  id: string
  name: string
  type: ToolType
  path: string
  runtimePath?: string
  cwd?: string
  argsTemplate: string
  argsSpec?: ArgsSpecV1
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
  argsSpec?: ArgsSpecV1
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
