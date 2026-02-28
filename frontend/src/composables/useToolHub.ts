import { computed, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { bridge } from '../services/bridge'
import { useI18n } from './useI18n'
import type {
  BackMessage,
  PythonPackageInstallStatusMessage,
  PythonPackagesMessage,
  TerminalInfo,
  TerminalOutputMessage,
  TerminalStartedMessage,
  TerminalStatusMessage,
  TerminalsMessage,
  ToolItem,
  ToolsMessage,
} from '../types'

const tools = ref<ToolItem[]>([])
const loadingTools = ref(false)

const activeTool = ref<ToolItem | null>(null)
const runnerVisible = ref(false)
const pythonOverride = ref('')

const packagePythonPath = ref('')
const pythonPackages = ref<{ name: string; version: string }[]>([])
const loadingPythonPackages = ref(false)
const pythonOperationBusy = ref(false)
const pythonOperationAction = ref<'install' | 'uninstall' | ''>('')
const pythonOperationPackage = ref('')

const { locale, t } = useI18n()
const pythonPackageStatus = ref(t('python.ready'))

const terminals = ref<TerminalInfo[]>([])
const ACTIVE_TERMINAL_STORAGE_KEY = 'toolhub.activeTerminalId'

const activeTerminalId = ref(loadPersistedActiveTerminalId())
const terminalBuffers = reactive<Record<string, string[]>>({})
const terminalOutputsById = terminalBuffers
const activeTerminalOutputs = computed(() => {
  if (!activeTerminalId.value) {
    return []
  }

  return terminalBuffers[activeTerminalId.value] ?? []
})

let unsubscribe: (() => void) | null = null
let initialized = false
let terminalBootstrapped = false
let terminalCreateInFlight = false
let lastTerminalCreateAt = 0

const TERMINAL_CREATE_COOLDOWN_MS = 900
const TERMINAL_CREATE_UNLOCK_MS = 1200
const TERMINAL_OUTPUT_BUFFER_LIMIT = 10000

watch(locale, () => {
  if (!pythonOperationBusy.value) {
    pythonPackageStatus.value = t('python.ready')
  }
})

function loadPersistedActiveTerminalId(): string {
  if (typeof window === 'undefined') {
    return ''
  }

  try {
    const value = window.localStorage.getItem(ACTIVE_TERMINAL_STORAGE_KEY)
    return typeof value === 'string' ? value : ''
  } catch {
    return ''
  }
}

function persistActiveTerminalId(terminalId: string): void {
  if (typeof window === 'undefined') {
    return
  }

  try {
    if (terminalId) {
      window.localStorage.setItem(ACTIVE_TERMINAL_STORAGE_KEY, terminalId)
    } else {
      window.localStorage.removeItem(ACTIVE_TERMINAL_STORAGE_KEY)
    }
  } catch {
    // ignore storage failures (private mode / permission restrictions)
  }
}

function setActiveTerminalId(terminalId: string): void {
  activeTerminalId.value = terminalId
  persistActiveTerminalId(terminalId)
}

function findFirstRunningTerminalId(list: TerminalInfo[]): string {
  const running = list.find((terminal) => terminal.status === 'running')
  return running?.terminalId ?? ''
}

function sortTerminals(terminalList: TerminalInfo[]): TerminalInfo[] {
  return [...terminalList].sort((left, right) => {
    return new Date(right.startTime).getTime() - new Date(left.startTime).getTime()
  })
}

function normalizeTerminals(terminalList: TerminalInfo[]): TerminalInfo[] {
  return sortTerminals(terminalList.filter((terminal) => terminal.status === 'running'))
}

function resetPythonOperationState(): void {
  pythonOperationBusy.value = false
  pythonOperationAction.value = ''
  pythonOperationPackage.value = ''
}

function ensureActiveTerminal(): void {
  const activeExists = terminals.value.some((terminal) => terminal.terminalId === activeTerminalId.value)
  if (activeExists) {
    return
  }

  setActiveTerminalId(findFirstRunningTerminalId(terminals.value))
}

function pruneTerminalBuffers(): void {
  const liveIds = new Set(terminals.value.map((terminal) => terminal.terminalId))

  Object.keys(terminalBuffers).forEach((terminalId) => {
    if (!liveIds.has(terminalId)) {
      delete terminalBuffers[terminalId]
    }
  })
}

function removeTerminal(terminalId: string): void {
  terminals.value = terminals.value.filter((terminal) => terminal.terminalId !== terminalId)
  delete terminalBuffers[terminalId]

  if (activeTerminalId.value === terminalId) {
    setActiveTerminalId(findFirstRunningTerminalId(terminals.value))
  }
}

function packageActionText(action: 'install' | 'uninstall'): string {
  return action === 'uninstall' ? t('python.action.uninstall') : t('python.action.install')
}

function fetchTools(): void {
  loadingTools.value = true
  bridge.send({ type: 'getTools' })
}

function fetchTerminals(): void {
  bridge.send({ type: 'getTerminals' })
}

function openTool(tool: ToolItem): void {
  activeTool.value = tool
  pythonOverride.value = tool.type === 'python' ? (tool.python ?? '') : ''
  runnerVisible.value = true
}

function handleRun(payload: { toolId: string; args: Record<string, string>; python?: string }): void {
  bridge.send({
    type: 'runToolInTerminal',
    toolId: payload.toolId,
    args: payload.args,
    python: payload.python,
    terminalId: activeTerminalId.value || undefined,
  })
}

function pickPythonInterpreter(): void {
  if (activeTool.value?.type !== 'python') {
    return
  }

  bridge.send({
    type: 'browsePython',
    defaultPath: pythonOverride.value || activeTool.value.cwd || activeTool.value.path,
    purpose: 'toolRunner',
  })
}

function pickPythonForPackages(): void {
  bridge.send({
    type: 'browsePython',
    defaultPath: packagePythonPath.value || pythonOverride.value,
    purpose: 'packageManager',
  })
}

function useSystemPythonForPackages(): void {
  packagePythonPath.value = ''
  refreshPythonPackages()
}

function refreshPythonPackages(): void {
  loadingPythonPackages.value = true
  bridge.send({
    type: 'getPythonPackages',
    pythonPath: packagePythonPath.value || undefined,
  })
}

function installPythonPackage(packageName: string): void {
  const normalized = packageName.trim()
  if (!normalized) {
    return
  }

  pythonOperationBusy.value = true
  pythonOperationAction.value = 'install'
  pythonOperationPackage.value = normalized
  pythonPackageStatus.value = t('python.installing', { packageName: normalized })

  bridge.send({
    type: 'installPythonPackage',
    pythonPath: packagePythonPath.value || undefined,
    packageName: normalized,
  })
}

function uninstallPythonPackage(packageName: string): void {
  const normalized = packageName.trim()
  if (!normalized) {
    return
  }

  pythonOperationBusy.value = true
  pythonOperationAction.value = 'uninstall'
  pythonOperationPackage.value = normalized
  pythonPackageStatus.value = t('python.uninstalling', { packageName: normalized })

  bridge.send({
    type: 'uninstallPythonPackage',
    pythonPath: packagePythonPath.value || undefined,
    packageName: normalized,
  })
}

function createTerminal(payload: { shell?: string; cwd?: string } = {}): void {
  const now = Date.now()
  if (terminalCreateInFlight) {
    return
  }

  if (now - lastTerminalCreateAt < TERMINAL_CREATE_COOLDOWN_MS) {
    return
  }

  terminalCreateInFlight = true
  lastTerminalCreateAt = now

  bridge.send({
    type: 'startTerminal',
    shell: payload.shell,
    cwd: payload.cwd,
  })

  setTimeout(() => {
    terminalCreateInFlight = false
  }, TERMINAL_CREATE_UNLOCK_MS)
}

function stopTerminal(terminalId: string): void {
  bridge.send({
    type: 'stopTerminal',
    terminalId,
  })
}

function stopAllTerminals(): void {
  const ids = terminals.value.map((terminal) => terminal.terminalId)
  ids.forEach((terminalId) => stopTerminal(terminalId))
  setActiveTerminalId('')
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
  terminalBuffers[terminalId] = []
}

function selectTerminal(terminalId: string): void {
  const exists = terminals.value.some((terminal) => terminal.terminalId === terminalId)
  if (!exists) {
    return
  }

  setActiveTerminalId(terminalId)
}

function handleToolsMessage(message: ToolsMessage): void {
  tools.value = message.tools
  loadingTools.value = false

  if (!packagePythonPath.value) {
    const firstPythonTool = message.tools.find((item) => item.type === 'python')
    packagePythonPath.value = firstPythonTool?.python ?? ''
  }
}

function handlePythonPackagesMessage(message: PythonPackagesMessage): void {
  pythonPackages.value = message.packages
  packagePythonPath.value = message.pythonPath === 'python' ? '' : message.pythonPath
  loadingPythonPackages.value = false
}

function handlePythonPackageInstallStatusMessage(message: PythonPackageInstallStatusMessage): void {
  const actionText = packageActionText(message.action)

  switch (message.status) {
    case 'running':
      pythonOperationBusy.value = true
      pythonOperationAction.value = message.action
      pythonOperationPackage.value = message.packageName
      pythonPackageStatus.value = t('python.status.running', {
        action: actionText,
        packageName: message.packageName,
      })
      break
    case 'succeeded':
      resetPythonOperationState()
      pythonPackageStatus.value = t('python.status.succeeded', {
        action: actionText,
        packageName: message.packageName,
      })
      ElMessage.success(
        t('python.status.succeeded', {
          action: actionText,
          packageName: message.packageName,
        }),
      )
      break
    case 'failed':
      resetPythonOperationState()
      pythonPackageStatus.value = t('python.status.failed', {
        action: actionText,
        packageName: message.packageName,
        details: message.message ? ` (${message.message})` : '',
      })
      ElMessage.error(
        t('python.status.failed', {
          action: actionText,
          packageName: message.packageName,
          details: '',
        }),
      )
      break
    default:
      break
  }
}

function handleTerminalsMessage(message: TerminalsMessage): void {
  terminals.value = normalizeTerminals(message.terminals)
  pruneTerminalBuffers()
  ensureActiveTerminal()

  if (!terminalBootstrapped) {
    terminalBootstrapped = true
    if (terminals.value.length === 0) {
      createTerminal()
    }
  }
}

function handleTerminalStarted(message: TerminalStartedMessage): void {
  terminalCreateInFlight = false
  upsertTerminal(message.terminal)
  setActiveTerminalId(message.terminal.terminalId)
  terminalBuffers[message.terminal.terminalId] ??= []
}

function handleTerminalStatus(message: TerminalStatusMessage): void {
  upsertTerminal(message.terminal)
  ensureActiveTerminal()
}

function handleTerminalOutput(message: TerminalOutputMessage): void {
  const chunks = terminalBuffers[message.terminalId] ?? (terminalBuffers[message.terminalId] = [])
  chunks.push(message.data)

  if (chunks.length > TERMINAL_OUTPUT_BUFFER_LIMIT) {
    chunks.splice(0, chunks.length - TERMINAL_OUTPUT_BUFFER_LIMIT)
  }

  if (!activeTerminalId.value) {
    setActiveTerminalId(message.terminalId)
  }
}

function upsertTerminal(nextTerminal: TerminalInfo): void {
  if (nextTerminal.status !== 'running') {
    removeTerminal(nextTerminal.terminalId)
    return
  }

  const index = terminals.value.findIndex((terminal) => terminal.terminalId === nextTerminal.terminalId)
  if (index >= 0) {
    terminals.value[index] = nextTerminal
  } else {
    terminals.value.push(nextTerminal)
  }

  terminals.value = sortTerminals(terminals.value)
  terminalBuffers[nextTerminal.terminalId] ??= []
}

function handleBackendMessage(message: BackMessage): void {
  switch (message.type) {
    case 'tools':
      handleToolsMessage(message)
      break
    case 'log':
    case 'runs':
    case 'runStarted':
    case 'runStatus':
      break
    case 'error':
      loadingTools.value = false
      loadingPythonPackages.value = false
      resetPythonOperationState()

      if (message.message.includes('Terminal not found or not running')) {
        fetchTerminals()
        ElMessage.warning(t('terminal.currentUnavailable'))
        break
      }

      if (message.message.includes('Failed to start terminal')) {
        terminalCreateInFlight = false
      }

      if (typeof message.details === 'string' && message.details.trim()) {
        ElMessage.error(`${message.message}: ${message.details}`)
      } else {
        ElMessage.error(message.message)
      }
      break
    case 'pythonSelected':
      if (message.purpose === 'packageManager') {
        if (typeof message.path === 'string' && message.path.trim()) {
          packagePythonPath.value = message.path
          refreshPythonPackages()
        }
        break
      }

      if (typeof message.path === 'string' && message.path.trim()) {
        pythonOverride.value = message.path
      }
      break
    case 'pythonPackages':
      handlePythonPackagesMessage(message)
      break
    case 'pythonPackageInstallStatus':
      handlePythonPackageInstallStatusMessage(message)
      break
    case 'terminals':
      handleTerminalsMessage(message)
      break
    case 'terminalStarted':
      handleTerminalStarted(message)
      break
    case 'terminalOutput':
      handleTerminalOutput(message)
      break
    case 'terminalStatus':
      handleTerminalStatus(message)
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

  fetchTools()
  fetchTerminals()
  refreshPythonPackages()
}

function disposeToolHub(): void {
  unsubscribe?.()
  unsubscribe = null
  initialized = false
  terminalCreateInFlight = false
}

export function useToolHub() {
  return {
    tools,
    loadingTools,
    activeTool,
    runnerVisible,
    pythonOverride,
    packagePythonPath,
    pythonPackages,
    loadingPythonPackages,
    pythonOperationBusy,
    pythonOperationAction,
    pythonOperationPackage,
    pythonPackageStatus,
    terminals,
    activeTerminalId,
    terminalOutputsById,
    activeTerminalOutputs,
    fetchTools,
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
