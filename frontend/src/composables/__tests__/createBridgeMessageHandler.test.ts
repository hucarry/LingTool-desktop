import { describe, expect, it, vi } from 'vitest'

import { createBridgeMessageHandler } from '../bridge/createBridgeMessageHandler'

function createDependencies() {
  return {
    toolsStore: {
      addToolRuntimeSelection: '',
      editToolRuntimeSelection: '',
      fetchTools: vi.fn(),
      handleFileSelectedMessage: vi.fn(),
      handleRuntimeSelected: vi.fn(),
      handleToolAddedMessage: vi.fn(),
      handleToolsDeletedMessage: vi.fn(),
      handleToolsMessage: vi.fn(),
      handleToolUpdatedMessage: vi.fn(),
      resetBusyStates: vi.fn(),
    },
    pythonStore: {
      handlePythonPackageInstallStatusMessage: vi.fn(),
      handlePythonPackagesMessage: vi.fn(),
      refreshPythonPackages: vi.fn(),
      resetBusyState: vi.fn(),
      setPackagePythonPath: vi.fn(),
    },
    terminalsStore: {
      fetchTerminals: vi.fn(),
      handleTerminalOutput: vi.fn(),
      handleTerminalStartFailed: vi.fn(),
      handleTerminalStarted: vi.fn(),
      handleTerminalStatus: vi.fn(),
      handleTerminalsMessage: vi.fn(),
    },
    settingsStore: {
      setAppDefaultPythonPath: vi.fn(),
      setAppRootPath: vi.fn(),
      setDefaultNodePath: vi.fn(),
      setDefaultPythonPath: vi.fn(),
      setDesktopPath: vi.fn(),
    },
    notifications: {
      error: vi.fn(),
      success: vi.fn(),
      warning: vi.fn(),
    },
    t: vi.fn((key: string) => key),
  }
}

describe('createBridgeMessageHandler', () => {
  it('routes app defaults to settings store', () => {
    const dependencies = createDependencies()
    const handleMessage = createBridgeMessageHandler(dependencies)

    handleMessage({
      type: 'appDefaults',
      pythonPath: 'D:/ToolHub/python/python.exe',
      appRootPath: 'D:/ToolHub',
      desktopPath: 'C:/Users/demo/Desktop',
    })

    expect(dependencies.settingsStore.setAppDefaultPythonPath).toHaveBeenCalledWith('D:/ToolHub/python/python.exe')
    expect(dependencies.settingsStore.setAppRootPath).toHaveBeenCalledWith('D:/ToolHub')
    expect(dependencies.settingsStore.setDesktopPath).toHaveBeenCalledWith('C:/Users/demo/Desktop')
  })

  it('handles python runtime selection by purpose', () => {
    const dependencies = createDependencies()
    const handleMessage = createBridgeMessageHandler(dependencies)

    handleMessage({
      type: 'pythonSelected',
      purpose: 'addToolRuntime',
      path: 'D:/Python/python.exe',
    })

    expect(dependencies.toolsStore.addToolRuntimeSelection).toBe('D:/Python/python.exe')
    expect(dependencies.toolsStore.handleRuntimeSelected).not.toHaveBeenCalled()
  })

  it('refreshes terminals and warns on missing active terminal errors', () => {
    const dependencies = createDependencies()
    const handleMessage = createBridgeMessageHandler(dependencies)

    handleMessage({
      type: 'error',
      message: 'Terminal not found or not running',
    })

    expect(dependencies.toolsStore.resetBusyStates).toHaveBeenCalled()
    expect(dependencies.pythonStore.resetBusyState).toHaveBeenCalled()
    expect(dependencies.terminalsStore.fetchTerminals).toHaveBeenCalled()
    expect(dependencies.notifications.warning).toHaveBeenCalledWith('terminal.currentUnavailable')
  })

  it('handles tool updates without a redundant catalog refetch', () => {
    const dependencies = createDependencies()
    const handleMessage = createBridgeMessageHandler(dependencies)

    handleMessage({
      type: 'toolUpdated',
      toolId: 'demo',
    })

    expect(dependencies.toolsStore.handleToolUpdatedMessage).toHaveBeenCalledWith({
      type: 'toolUpdated',
      toolId: 'demo',
    })
    expect(dependencies.toolsStore.fetchTools).not.toHaveBeenCalled()
  })
})
