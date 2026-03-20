import { defineStore } from 'pinia'
import { computed, reactive, ref } from 'vue'

import { bridge } from '../services/bridge'
import type {
  ToolType,
  TerminalInfo,
  TerminalOutputMessage,
  TerminalStartedMessage,
  TerminalStatusMessage,
  TerminalsMessage,
} from '../types'

const ACTIVE_TERMINAL_STORAGE_KEY = 'toolhub.activeTerminalId'
const TERMINAL_CREATE_COOLDOWN_MS = 900
const TERMINAL_CREATE_UNLOCK_MS = 1200
const TERMINAL_OUTPUT_BUFFER_LIMIT = 10000

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

export const useTerminalsStore = defineStore('terminals', () => {
  const terminals = ref<TerminalInfo[]>([])
  const activeTerminalId = ref(loadPersistedActiveTerminalId())
  const terminalBuffers = reactive<Record<string, string[]>>({})
  const terminalOutputsById = terminalBuffers
  const activeTerminalOutputs = computed(() => {
    if (!activeTerminalId.value) {
      return []
    }

    return terminalBuffers[activeTerminalId.value] ?? []
  })

  let terminalCreateInFlight = false
  let lastTerminalCreateAt = 0

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
      // ignore storage failures
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

  function beginCreateRequest(): boolean {
    const now = Date.now()
    if (terminalCreateInFlight) {
      return false
    }

    if (now - lastTerminalCreateAt < TERMINAL_CREATE_COOLDOWN_MS) {
      return false
    }

    terminalCreateInFlight = true
    lastTerminalCreateAt = now

    setTimeout(() => {
      terminalCreateInFlight = false
    }, TERMINAL_CREATE_UNLOCK_MS)

    return true
  }

  function handleTerminalStartFailed(): void {
    terminalCreateInFlight = false
  }

  function fetchTerminals(): void {
    bridge.send({ type: 'getTerminals' })
  }

  function createTerminal(payload: { title?: string; shell?: string; cwd?: string; toolType?: ToolType; runtimePath?: string } = {}): void {
    if (!beginCreateRequest()) {
      return
    }

    bridge.send({
      type: 'startTerminal',
      title: payload.title,
      shell: payload.shell,
      cwd: payload.cwd,
      toolType: payload.toolType,
      runtimePath: payload.runtimePath,
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

  function stopTerminal(terminalId: string): void {
    bridge.send({
      type: 'stopTerminal',
      terminalId,
    })
  }

  function stopAllTerminals(): void {
    terminals.value.forEach((terminal) => stopTerminal(terminal.terminalId))
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

  function handleTerminalsMessage(message: TerminalsMessage): void {
    terminals.value = normalizeTerminals(message.terminals)
    pruneTerminalBuffers()
    ensureActiveTerminal()
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

  function resetCreateState(): void {
    terminalCreateInFlight = false
  }

  return {
    terminals,
    activeTerminalId,
    terminalOutputsById,
    activeTerminalOutputs,
    setActiveTerminalId,
    beginCreateRequest,
    handleTerminalStartFailed,
    fetchTerminals,
    createTerminal,
    stopTerminal,
    stopAllTerminals,
    sendTerminalInput,
    resizeTerminal,
    clearTerminalOutput,
    selectTerminal,
    handleTerminalsMessage,
    handleTerminalStarted,
    handleTerminalStatus,
    handleTerminalOutput,
    resetCreateState,
  }
})
