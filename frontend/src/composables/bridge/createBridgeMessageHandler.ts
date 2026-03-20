import type {
  AppDefaultsMessage,
  BackMessage,
  ErrorMessage,
  FileSelectedMessage,
  PythonPackageInstallStatusMessage,
  PythonPackagesMessage,
  PythonSelectedMessage,
  TerminalOutputMessage,
  TerminalStartedMessage,
  TerminalStatusMessage,
  TerminalsMessage,
  ToolAddedMessage,
  ToolsDeletedMessage,
  ToolsMessage,
  ToolUpdatedMessage,
} from '../../types'

type TranslateFn = (key: string, params?: Record<string, string | number>) => string

type BridgeMessageHandlerMap = {
  [K in BackMessage['type']]?: (message: Extract<BackMessage, { type: K }>) => void
}

export interface BridgeMessageHandlerDependencies {
  toolsStore: {
    addToolRuntimeSelection: string
    editToolRuntimeSelection: string
    fetchTools(): void
    handleFileSelectedMessage(message: FileSelectedMessage): void
    handleRuntimeSelected(path?: string): void
    handleToolAddedMessage(message: ToolAddedMessage): void
    handleToolsDeletedMessage(message: ToolsDeletedMessage): void
    handleToolsMessage(message: ToolsMessage): void
    handleToolUpdatedMessage(message: ToolUpdatedMessage): void
    resetBusyStates(): void
  }
  pythonStore: {
    handlePythonPackageInstallStatusMessage(message: PythonPackageInstallStatusMessage): void
    handlePythonPackagesMessage(message: PythonPackagesMessage): void
    refreshPythonPackages(): void
    resetBusyState(): void
    setPackagePythonPath(path: string): void
  }
  terminalsStore: {
    fetchTerminals(): void
    handleTerminalOutput(message: TerminalOutputMessage): void
    handleTerminalStartFailed(): void
    handleTerminalStarted(message: TerminalStartedMessage): void
    handleTerminalStatus(message: TerminalStatusMessage): void
    handleTerminalsMessage(message: TerminalsMessage): void
  }
  settingsStore: {
    setAppDefaultPythonPath(path?: string): void
    setAppRootPath(path?: string): void
    setDefaultNodePath(path: string): void
    setDefaultPythonPath(path: string): void
    setDesktopPath(path?: string): void
  }
  notifications: {
    error(message: string): void
    success(message: string): void
    warning(message: string): void
  }
  t: TranslateFn
}

function createAppHandlers(
  dependencies: BridgeMessageHandlerDependencies,
): Pick<BridgeMessageHandlerMap, 'appDefaults' | 'error'> {
  const { settingsStore, toolsStore, pythonStore, terminalsStore, notifications, t } = dependencies

  return {
    appDefaults(message: AppDefaultsMessage) {
      settingsStore.setAppDefaultPythonPath(message.pythonPath)
      settingsStore.setAppRootPath(message.appRootPath)
      settingsStore.setDesktopPath(message.desktopPath)
    },

    error(message: ErrorMessage) {
      toolsStore.resetBusyStates()
      pythonStore.resetBusyState()

      if (message.message.includes('Terminal not found or not running')) {
        terminalsStore.fetchTerminals()
        notifications.warning(t('terminal.currentUnavailable'))
        return
      }

      if (message.message.includes('Failed to start terminal')) {
        terminalsStore.handleTerminalStartFailed()
      }

      if (typeof message.details === 'string' && message.details.trim()) {
        notifications.error(`${message.message}: ${message.details}`)
        return
      }

      notifications.error(message.message)
    },
  }
}

function createToolHandlers(
  dependencies: BridgeMessageHandlerDependencies,
): Pick<BridgeMessageHandlerMap, 'tools' | 'fileSelected' | 'toolAdded' | 'toolUpdated' | 'toolsDeleted'> {
  const { toolsStore, settingsStore, notifications, t } = dependencies

  return {
    tools(message: ToolsMessage) {
      toolsStore.handleToolsMessage(message)
    },

    fileSelected(message: FileSelectedMessage) {
      if (message.purpose === 'settingsDefaultNode') {
        if (message.path?.trim()) {
          settingsStore.setDefaultNodePath(message.path)
          notifications.success(t('app.defaultNodeUpdated'))
        }
        return
      }

      toolsStore.handleFileSelectedMessage(message)
    },

    toolAdded(message: ToolAddedMessage) {
      toolsStore.handleToolAddedMessage(message)
    },

    toolUpdated(message: ToolUpdatedMessage) {
      toolsStore.handleToolUpdatedMessage(message)
    },

    toolsDeleted(message: ToolsDeletedMessage) {
      toolsStore.handleToolsDeletedMessage(message)
    },
  }
}

function createPythonHandlers(
  dependencies: BridgeMessageHandlerDependencies,
): Pick<BridgeMessageHandlerMap, 'pythonSelected' | 'pythonPackages' | 'pythonPackageInstallStatus'> {
  const { toolsStore, pythonStore, settingsStore, notifications, t } = dependencies

  return {
    pythonSelected(message: PythonSelectedMessage) {
      if (message.purpose === 'packageManager') {
        if (message.path?.trim()) {
          pythonStore.setPackagePythonPath(message.path)
          pythonStore.refreshPythonPackages()
        }
        return
      }

      if (message.purpose === 'settingsDefaultPython') {
        if (message.path?.trim()) {
          settingsStore.setDefaultPythonPath(message.path)
          notifications.success(t('app.defaultPythonUpdated'))
        }
        return
      }

      if (message.purpose === 'addToolRuntime') {
        toolsStore.addToolRuntimeSelection = message.path ?? ''
        return
      }

      if (message.purpose === 'editToolRuntime') {
        toolsStore.editToolRuntimeSelection = message.path ?? ''
        return
      }

      toolsStore.handleRuntimeSelected(message.path)
    },

    pythonPackages(message: PythonPackagesMessage) {
      pythonStore.handlePythonPackagesMessage(message)
    },

    pythonPackageInstallStatus(message: PythonPackageInstallStatusMessage) {
      pythonStore.handlePythonPackageInstallStatusMessage(message)
      if (message.status === 'succeeded') {
        pythonStore.refreshPythonPackages()
      }
    },
  }
}

function createTerminalHandlers(
  dependencies: BridgeMessageHandlerDependencies,
): Pick<BridgeMessageHandlerMap, 'terminals' | 'terminalStarted' | 'terminalOutput' | 'terminalStatus'> {
  const { terminalsStore } = dependencies

  return {
    terminals(message: TerminalsMessage) {
      terminalsStore.handleTerminalsMessage(message)
    },

    terminalStarted(message: TerminalStartedMessage) {
      terminalsStore.handleTerminalStarted(message)
    },

    terminalOutput(message: TerminalOutputMessage) {
      terminalsStore.handleTerminalOutput(message)
    },

    terminalStatus(message: TerminalStatusMessage) {
      terminalsStore.handleTerminalStatus(message)
    },
  }
}

export function createBridgeMessageHandler(dependencies: BridgeMessageHandlerDependencies) {
  const handlers: BridgeMessageHandlerMap = {
    ...createAppHandlers(dependencies),
    ...createToolHandlers(dependencies),
    ...createPythonHandlers(dependencies),
    ...createTerminalHandlers(dependencies),
  }

  return (message: BackMessage): void => {
    const handler = handlers[message.type] as ((payload: BackMessage) => void) | undefined
    handler?.(message)
  }
}
