import { useI18n } from './useI18n'
import { bridge } from '../services/bridge'
import { useNotificationsStore } from '../stores/notifications'
import { usePythonStore } from '../stores/python'
import { useSettingsStore } from '../stores/settings'
import { useTerminalsStore } from '../stores/terminals'
import { useToolsStore } from '../stores/tools'
import { createBridgeMessageHandler } from './bridge/createBridgeMessageHandler'

let unsubscribe: (() => void) | null = null
let initialized = false

export function useBridgeBootstrap() {
  const toolsStore = useToolsStore()
  const pythonStore = usePythonStore()
  const terminalsStore = useTerminalsStore()
  const settingsStore = useSettingsStore()
  const notifications = useNotificationsStore()
  const { t } = useI18n()
  const handleBackendMessage = createBridgeMessageHandler({
    toolsStore,
    pythonStore,
    terminalsStore,
    settingsStore,
    notifications,
    t,
  })

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
