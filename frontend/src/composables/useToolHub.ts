import { storeToRefs } from 'pinia'

import { usePythonStore } from '../stores/python'
import { useTerminalsStore } from '../stores/terminals'
import { useToolsStore } from '../stores/tools'
import { bridge } from '../services/bridge'
import { useI18n } from './useI18n'
import { useNotify } from './useNotify'
import { useSettings } from './useSettings'
import type { AddToolPayload, BackMessage, ToolItem } from '../types'

let unsubscribe: (() => void) | null = null
let initialized = false

export function useToolHub() {
  const toolsStore = useToolsStore()
  const pythonStore = usePythonStore()
  const terminalsStore = useTerminalsStore()

  const toolRefs = storeToRefs(toolsStore)
  const pythonRefs = storeToRefs(pythonStore)
  const terminalRefs = storeToRefs(terminalsStore)

  const { t } = useI18n()
  const notify = useNotify()
  const { defaultPythonPath, setDefaultPythonPath } = useSettings()

  function fetchTools(): void {
    toolsStore.beginLoadingTools()
    bridge.send({ type: 'getTools' })
  }

  function addTool(tool: AddToolPayload): void {
    toolsStore.beginAddTool()
    bridge.send({
      type: 'addTool',
      tool,
    })
  }

  function updateTool(tool: AddToolPayload): void {
    toolsStore.beginUpdateTool()
    bridge.send({
      type: 'updateTool',
      tool,
    })
  }

  function deleteTools(toolIds: string[]): void {
    const normalized = toolIds
      .map((item) => item.trim())
      .filter((item, index, arr) => item.length > 0 && arr.indexOf(item) === index)

    if (normalized.length === 0) {
      return
    }

    toolsStore.beginDeleteTools()
    bridge.send({
      type: 'deleteTools',
      toolIds: normalized,
    })
  }

  function pickAddToolPath(defaultPath?: string, toolType?: string): void {
    bridge.send({
      type: 'browseFile',
      defaultPath,
      filter: toolType === 'python' ? '*.py' : '*.exe',
      purpose: 'addToolPath',
    })
  }

  function pickAddToolPython(defaultPath?: string): void {
    bridge.send({
      type: 'browseFile',
      defaultPath,
      filter: '*.exe',
      purpose: 'addToolPython',
    })
  }

  function pickEditToolPath(defaultPath?: string, toolType?: string): void {
    bridge.send({
      type: 'browseFile',
      defaultPath,
      filter: toolType === 'python' ? '*.py' : '*.exe',
      purpose: 'editToolPath',
    })
  }

  function pickEditToolPython(defaultPath?: string): void {
    bridge.send({
      type: 'browseFile',
      defaultPath,
      filter: '*.exe',
      purpose: 'editToolPython',
    })
  }

  function fetchTerminals(): void {
    bridge.send({ type: 'getTerminals' })
  }

  function openTool(tool: ToolItem): void {
    toolsStore.openTool(tool, defaultPythonPath.value)
  }

  function handleRun(payload: { toolId: string; args: Record<string, string>; python?: string }): void {
    const tool = toolsStore.tools.find((item) => item.id === payload.toolId)
    const effectivePython = payload.python?.trim()
      || (tool?.type === 'python'
        ? (tool.python?.trim() || defaultPythonPath.value.trim() || undefined)
        : undefined)

    bridge.send({
      type: 'runToolInTerminal',
      toolId: payload.toolId,
      args: payload.args,
      python: effectivePython,
      terminalId: terminalsStore.activeTerminalId || undefined,
    })
  }

  function pickPythonInterpreter(): void {
    const activeTool = toolsStore.activeTool
    if (activeTool?.type !== 'python') {
      return
    }

    bridge.send({
      type: 'browsePython',
      defaultPath: toolsStore.pythonOverride
        || activeTool.python
        || defaultPythonPath.value
        || activeTool.cwd
        || activeTool.path,
      purpose: 'toolRunner',
    })
  }

  function pickPythonForPackages(): void {
    bridge.send({
      type: 'browsePython',
      defaultPath: pythonStore.packagePythonPath || defaultPythonPath.value || toolsStore.pythonOverride,
      purpose: 'packageManager',
    })
  }

  function useSystemPythonForPackages(): void {
    pythonStore.useSystemPythonPath()
    refreshPythonPackages()
  }

  function refreshPythonPackages(): void {
    pythonStore.beginLoadingPackages()
    bridge.send({
      type: 'getPythonPackages',
      pythonPath: pythonStore.packagePythonPath || undefined,
    })
  }

  function installPythonPackage(packageName: string): void {
    const normalized = packageName.trim()
    if (!normalized) {
      return
    }

    pythonStore.beginOperation('install', normalized)
    bridge.send({
      type: 'installPythonPackage',
      pythonPath: pythonStore.packagePythonPath || undefined,
      packageName: normalized,
    })
  }

  function uninstallPythonPackage(packageName: string): void {
    const normalized = packageName.trim()
    if (!normalized) {
      return
    }

    pythonStore.beginOperation('uninstall', normalized)
    bridge.send({
      type: 'uninstallPythonPackage',
      pythonPath: pythonStore.packagePythonPath || undefined,
      packageName: normalized,
    })
  }

  function createTerminal(payload: { shell?: string; cwd?: string } = {}): void {
    if (!terminalsStore.beginCreateRequest()) {
      return
    }

    bridge.send({
      type: 'startTerminal',
      shell: payload.shell,
      cwd: payload.cwd,
    })
  }

  function stopTerminal(terminalId: string): void {
    bridge.send({
      type: 'stopTerminal',
      terminalId,
    })
  }

  function stopAllTerminals(): void {
    const ids = terminalsStore.terminals.map((terminal) => terminal.terminalId)
    ids.forEach((terminalId) => stopTerminal(terminalId))
    terminalsStore.setActiveTerminalId('')
  }

  function sendTerminalInput(payload: { terminalId: string; data: string }): void {
    if (!payload.terminalId) {
      return
    }

    bridge.send({
      type: 'terminalInput',
      terminalId: payload.terminalId,
      data: payload.data,
    })
  }

  function resizeTerminal(payload: { terminalId: string; cols: number; rows: number }): void {
    if (!payload.terminalId || payload.cols <= 0 || payload.rows <= 0) {
      return
    }

    bridge.send({
      type: 'terminalResize',
      terminalId: payload.terminalId,
      cols: payload.cols,
      rows: payload.rows,
    })
  }

  function clearTerminalOutput(terminalId: string): void {
    terminalsStore.clearTerminalOutput(terminalId)
  }

  function selectTerminal(terminalId: string): void {
    terminalsStore.selectTerminal(terminalId)
  }

  function handleBackendMessage(message: BackMessage): void {
    switch (message.type) {
      case 'tools':
        toolsStore.handleToolsMessage(message)
        break
      case 'log':
      case 'runs':
      case 'runStarted':
      case 'runStatus':
        break
      case 'error':
        toolsStore.resetBusyStates()
        pythonStore.resetBusyState()

        if (message.message.includes('Terminal not found or not running')) {
          fetchTerminals()
          notify.warning(t('terminal.currentUnavailable'))
          break
        }

        if (message.message.includes('Failed to start terminal')) {
          terminalsStore.handleTerminalStartFailed()
        }

        if (typeof message.details === 'string' && message.details.trim()) {
          notify.error(`${message.message}: ${message.details}`)
        } else {
          notify.error(message.message)
        }
        break
      case 'pythonSelected':
        if (message.purpose === 'packageManager') {
          if (typeof message.path === 'string' && message.path.trim()) {
            pythonStore.setPackagePythonPath(message.path)
            refreshPythonPackages()
          }
          break
        }

        if (message.purpose === 'settingsDefaultPython') {
          if (typeof message.path === 'string' && message.path.trim()) {
            setDefaultPythonPath(message.path)
            notify.success(t('app.defaultPythonUpdated'))
          }
          break
        }

        toolsStore.handlePythonSelected(message.path)
        break
      case 'pythonPackages':
        pythonStore.handlePythonPackagesMessage(message)
        break
      case 'fileSelected':
        toolsStore.handleFileSelectedMessage(message)
        break
      case 'toolAdded':
        toolsStore.handleToolAddedMessage(message)
        fetchTools()
        break
      case 'toolUpdated':
        toolsStore.handleToolUpdatedMessage(message)
        fetchTools()
        break
      case 'toolsDeleted':
        toolsStore.handleToolsDeletedMessage(message)
        fetchTools()
        break
      case 'pythonPackageInstallStatus':
        pythonStore.handlePythonPackageInstallStatusMessage(message)
        if (message.status === 'succeeded') {
          refreshPythonPackages()
        }
        break
      case 'terminals':
        terminalsStore.handleTerminalsMessage(message, () => createTerminal())
        break
      case 'terminalStarted':
        terminalsStore.handleTerminalStarted(message)
        break
      case 'terminalOutput':
        terminalsStore.handleTerminalOutput(message)
        break
      case 'terminalStatus':
        terminalsStore.handleTerminalStatus(message)
        break
      default:
        break
    }
  }

  function initToolHub(): void {
    if (initialized) {
      return
    }

    initialized = true
    unsubscribe = bridge.onMessage(handleBackendMessage)

    fetchTerminals()
  }

  function disposeToolHub(): void {
    unsubscribe?.()
    unsubscribe = null
    initialized = false
    terminalsStore.resetCreateState()
  }

  return {
    ...toolRefs,
    ...pythonRefs,
    ...terminalRefs,
    fetchTools,
    addTool,
    updateTool,
    deleteTools,
    pickAddToolPath,
    pickAddToolPython,
    pickEditToolPath,
    pickEditToolPython,
    openTool,
    handleRun,
    pickPythonInterpreter,
    pickPythonForPackages,
    useSystemPythonForPackages,
    refreshPythonPackages,
    installPythonPackage,
    uninstallPythonPackage,
    createTerminal,
    stopTerminal,
    stopAllTerminals,
    sendTerminalInput,
    resizeTerminal,
    clearTerminalOutput,
    selectTerminal,
    initToolHub,
    disposeToolHub,
  }
}
