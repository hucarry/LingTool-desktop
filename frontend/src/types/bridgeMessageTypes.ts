type ValueOf<T> = T[keyof T]

export const BRIDGE_MESSAGE_TYPES = {
  GET_TOOLS: 'getTools',
  GET_APP_DEFAULTS: 'getAppDefaults',
  ADD_TOOL: 'addTool',
  UPDATE_TOOL: 'updateTool',
  DELETE_TOOLS: 'deleteTools',
  RUN_TOOL: 'runTool',
  RUN_TOOL_IN_TERMINAL: 'runToolInTerminal',
  OPEN_URL_TOOL: 'openUrlTool',
  STOP_RUN: 'stopRun',
  GET_RUNS: 'getRuns',
  BROWSE_PYTHON: 'browsePython',
  BROWSE_FILE: 'browseFile',
  GET_PYTHON_PACKAGES: 'getPythonPackages',
  INSTALL_PYTHON_PACKAGE: 'installPythonPackage',
  UNINSTALL_PYTHON_PACKAGE: 'uninstallPythonPackage',
  GET_TERMINALS: 'getTerminals',
  START_TERMINAL: 'startTerminal',
  TERMINAL_INPUT: 'terminalInput',
  TERMINAL_RESIZE: 'terminalResize',
  STOP_TERMINAL: 'stopTerminal',
  TOOLS: 'tools',
  RUN_STARTED: 'runStarted',
  RUN_STATUS: 'runStatus',
  RUNS: 'runs',
  LOG: 'log',
  ERROR: 'error',
  PYTHON_SELECTED: 'pythonSelected',
  FILE_SELECTED: 'fileSelected',
  TOOL_ADDED: 'toolAdded',
  TOOL_UPDATED: 'toolUpdated',
  TOOLS_DELETED: 'toolsDeleted',
  PYTHON_PACKAGES: 'pythonPackages',
  PYTHON_PACKAGE_INSTALL_STATUS: 'pythonPackageInstallStatus',
  TERMINALS: 'terminals',
  TERMINAL_STARTED: 'terminalStarted',
  TERMINAL_OUTPUT: 'terminalOutput',
  TERMINAL_STATUS: 'terminalStatus',
  APP_DEFAULTS: 'appDefaults',
} as const

export type BridgeMessageType = ValueOf<typeof BRIDGE_MESSAGE_TYPES>

export const LOG_CHANNELS = {
  STDOUT: 'stdout',
  STDERR: 'stderr',
} as const

export type LogChannel = ValueOf<typeof LOG_CHANNELS>

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
