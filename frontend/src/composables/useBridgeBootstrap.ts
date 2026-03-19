import { useI18n } from './useI18n'
import { bridge } from '../services/bridge'
import { useNotificationsStore } from '../stores/notifications'
import { usePythonStore } from '../stores/python'
import { useSettingsStore } from '../stores/settings'
import { useTerminalsStore } from '../stores/terminals'
import { useToolsStore } from '../stores/tools'
import type { BackMessage } from '../types'

let unsubscribe: (() => void) | null = null
let initialized = false

export function useBridgeBootstrap() {
  const toolsStore = useToolsStore()
  const pythonStore = usePythonStore()
  const terminalsStore = useTerminalsStore()
  const settingsStore = useSettingsStore()
  const notifications = useNotificationsStore()
  const { t } = useI18n()

  function handleBackendMessage(message: BackMessage): void {
    switch (message.type) {
      case 'appDefaults':
        settingsStore.setAppDefaultPythonPath(message.pythonPath)
        break
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
          terminalsStore.fetchTerminals()
          notifications.warning(t('terminal.currentUnavailable'))
          break
        }

        if (message.message.includes('Failed to start terminal')) {
          terminalsStore.handleTerminalStartFailed()
        }

        if (typeof message.details === 'string' && message.details.trim()) {
          notifications.error(`${message.message}: ${message.details}`)
        } else {
          notifications.error(message.message)
        }
        break
      case 'pythonSelected':
        if (message.purpose === 'packageManager') {
          if (message.path?.trim()) {
            pythonStore.setPackagePythonPath(message.path)
            pythonStore.refreshPythonPackages()
          }
          break
        }

        if (message.purpose === 'settingsDefaultPython') {
          if (message.path?.trim()) {
            settingsStore.setDefaultPythonPath(message.path)
            notifications.success(t('app.defaultPythonUpdated'))
          }
          break
        }

        if (message.purpose === 'addToolRuntime') {
          toolsStore.addToolRuntimeSelection = message.path ?? ''
          break
        }

        if (message.purpose === 'editToolRuntime') {
          toolsStore.editToolRuntimeSelection = message.path ?? ''
          break
        }

        toolsStore.handleRuntimeSelected(message.path)
        break
      case 'pythonPackages':
        pythonStore.handlePythonPackagesMessage(message)
        break
      case 'fileSelected':
        if (message.purpose === 'settingsDefaultNode') {
          if (message.path?.trim()) {
            settingsStore.setDefaultNodePath(message.path)
            notifications.success(t('app.defaultNodeUpdated'))
          }
          break
        }

        toolsStore.handleFileSelectedMessage(message)
        break
      case 'toolAdded':
        toolsStore.handleToolAddedMessage(message)
        toolsStore.fetchTools()
        break
      case 'toolUpdated':
        toolsStore.handleToolUpdatedMessage(message)
        toolsStore.fetchTools()
        break
      case 'toolsDeleted':
        toolsStore.handleToolsDeletedMessage(message)
        toolsStore.fetchTools()
        break
      case 'pythonPackageInstallStatus':
        pythonStore.handlePythonPackageInstallStatusMessage(message)
        if (message.status === 'succeeded') {
          pythonStore.refreshPythonPackages()
        }
        break
      case 'terminals':
        terminalsStore.handleTerminalsMessage(message)
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

  function init(): void {
    if (initialized) {
      return
    }

    initialized = true
    unsubscribe = bridge.onMessage(handleBackendMessage)
    bridge.send({ type: 'getAppDefaults' })
    terminalsStore.fetchTerminals()
  }

  function dispose(): void {
    unsubscribe?.()
    unsubscribe = null
    initialized = false
    terminalsStore.resetCreateState()
  }

  return {
    init,
    dispose,
  }
}
