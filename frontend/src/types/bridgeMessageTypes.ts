export {
  BRIDGE_MESSAGE_TYPES,
  LOG_CHANNELS,
  type BridgeMessageType,
  type LogChannel,
} from './bridgeMessageTypes.generated'

type ValueOf<T> = T[keyof T]

export const PYTHON_SELECTION_PURPOSES = {
  TOOL_RUNNER_RUNTIME: 'toolRunnerRuntime',
  ADD_TOOL_RUNTIME: 'addToolRuntime',
  EDIT_TOOL_RUNTIME: 'editToolRuntime',
  PACKAGE_MANAGER: 'packageManager',
  SETTINGS_DEFAULT_PYTHON: 'settingsDefaultPython',
} as const

export type PythonSelectionPurpose = ValueOf<typeof PYTHON_SELECTION_PURPOSES>

export const FILE_SELECTION_PURPOSES = {
  ADD_TOOL_PATH: 'addToolPath',
  ADD_TOOL_RUNTIME: 'addToolRuntime',
  EDIT_TOOL_PATH: 'editToolPath',
  EDIT_TOOL_RUNTIME: 'editToolRuntime',
  TOOL_RUNNER_RUNTIME: 'toolRunnerRuntime',
  SETTINGS_DEFAULT_NODE: 'settingsDefaultNode',
} as const

export type FileSelectionPurpose = ValueOf<typeof FILE_SELECTION_PURPOSES>

export const PYTHON_PACKAGE_ACTIONS = {
  INSTALL: 'install',
  UNINSTALL: 'uninstall',
} as const

export type PythonPackageAction = ValueOf<typeof PYTHON_PACKAGE_ACTIONS>

export const PYTHON_PACKAGE_INSTALL_STATUSES = {
  RUNNING: 'running',
  SUCCEEDED: 'succeeded',
  FAILED: 'failed',
} as const

export type PythonPackageInstallStatus = ValueOf<typeof PYTHON_PACKAGE_INSTALL_STATUSES>
