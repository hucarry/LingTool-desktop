import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'

import type { TerminalInfo } from '../types'
import { useI18n } from './useI18n'

const TERMINAL_PANEL_UI_STORAGE_KEY = 'toolhub.terminalPanelUi'

export interface TerminalPanelProps {
  visible: boolean
  terminals: TerminalInfo[]
  activeTerminalId: string
  outputsByTerminal: Record<string, string[]>
}

export interface TerminalPanelActions {
  selectTerminal: (terminalId: string) => void
  createTerminal: (payload: { shell?: string; cwd?: string }) => void
  stopTerminal: (terminalId: string) => void
  stopAllTerminals: () => void
  input: (payload: { terminalId: string; data: string }) => void
}

interface ContextMenuState {
  visible: boolean
  x: number
  y: number
  terminalId: string
}

interface PersistedTerminalPanelUiState {
  shellInput: string
  cwdInput: string
  showToolbar: boolean
  splitEnabled: boolean
  secondaryTerminalId: string
  focusedTerminalId: string
}

function createDefaultUiState(): PersistedTerminalPanelUiState {
  return {
    shellInput: '',
    cwdInput: '',
    showToolbar: false,
    splitEnabled: false,
    secondaryTerminalId: '',
    focusedTerminalId: '',
  }
}

function loadPersistedTerminalPanelUiState(): PersistedTerminalPanelUiState {
  if (typeof window === 'undefined') {
    return createDefaultUiState()
  }

  try {
    const raw = window.localStorage.getItem(TERMINAL_PANEL_UI_STORAGE_KEY)
    if (!raw) {
      throw new Error('missing terminal panel state')
    }

    const parsed = JSON.parse(raw) as Partial<PersistedTerminalPanelUiState>
    return {
      shellInput: typeof parsed.shellInput === 'string' ? parsed.shellInput : '',
      cwdInput: typeof parsed.cwdInput === 'string' ? parsed.cwdInput : '',
      showToolbar: parsed.showToolbar === true,
      splitEnabled: parsed.splitEnabled === true,
      secondaryTerminalId: typeof parsed.secondaryTerminalId === 'string' ? parsed.secondaryTerminalId : '',
      focusedTerminalId: typeof parsed.focusedTerminalId === 'string' ? parsed.focusedTerminalId : '',
    }
  } catch {
    return createDefaultUiState()
  }
}

export function useTerminalPanelState(options: {
  props: TerminalPanelProps
  actions: TerminalPanelActions
}) {
  const { props, actions } = options
  const persistedUiState = loadPersistedTerminalPanelUiState()
  const { t, formatSessionCount } = useI18n()

  const shellInput = ref(persistedUiState.shellInput)
  const cwdInput = ref(persistedUiState.cwdInput)
  const showToolbar = ref(persistedUiState.showToolbar)
  const splitEnabled = ref(persistedUiState.splitEnabled)
  const secondaryTerminalId = ref(persistedUiState.secondaryTerminalId)
  const focusedTerminalId = ref(persistedUiState.focusedTerminalId)
  const pendingSplitCreation = ref(false)

  const contextMenu = reactive<ContextMenuState>({
    visible: false,
    x: 0,
    y: 0,
    terminalId: '',
  })

  function getTerminalBaseLabel(terminal: TerminalInfo): string {
    const title = terminal.title?.trim()
    if (title) {
      return title
    }

    return terminal.shell.split(/[\\/]/).pop() || terminal.shell || t('terminal.shellFallback')
  }

  const terminalLabelCounts = computed(() => {
    const counts = new Map<string, number>()
    props.terminals.forEach((terminal) => {
      const baseLabel = getTerminalBaseLabel(terminal)
      counts.set(baseLabel, (counts.get(baseLabel) ?? 0) + 1)
    })
    return counts
  })

  const terminalIdList = computed(() => props.terminals.map((terminal) => terminal.terminalId))

  const primaryTerminalId = computed(() => {
    if (props.activeTerminalId && terminalIdList.value.includes(props.activeTerminalId)) {
      return props.activeTerminalId
    }

    return terminalIdList.value[0] ?? ''
  })

  const commandTerminalId = computed(() => {
    if (focusedTerminalId.value && terminalIdList.value.includes(focusedTerminalId.value)) {
      return focusedTerminalId.value
    }

    return primaryTerminalId.value
  })

  const terminalTabs = computed(() => {
    return props.terminals.map((terminal, index) => {
      const baseLabel = getTerminalBaseLabel(terminal)
      const order = props.terminals.length - index
      return {
        ...terminal,
        label: (terminalLabelCounts.value.get(baseLabel) ?? 0) > 1 ? `${baseLabel} ${order}` : baseLabel,
        isCommandTarget: terminal.terminalId === commandTerminalId.value,
      }
    })
  })

  const terminalLabelMap = computed(() => {
    const map = new Map<string, string>()
    terminalTabs.value.forEach((terminal) => {
      map.set(terminal.terminalId, terminal.label)
    })
    return map
  })

  const terminalMap = computed(() => {
    const map = new Map<string, TerminalInfo>()
    props.terminals.forEach((terminal) => {
      map.set(terminal.terminalId, terminal)
    })
    return map
  })

  const resolvedSecondaryTerminalId = computed(() => {
    if (!splitEnabled.value || !secondaryTerminalId.value) {
      return ''
    }

    if (!terminalIdList.value.includes(secondaryTerminalId.value)) {
      return ''
    }

    if (secondaryTerminalId.value === primaryTerminalId.value) {
      return ''
    }

    return secondaryTerminalId.value
  })

  const splitActive = computed(() => splitEnabled.value && !!resolvedSecondaryTerminalId.value)
  const sessionSummary = computed(() => formatSessionCount(props.terminals.length))

  const primaryOutputs = computed(() => {
    if (!primaryTerminalId.value) {
      return []
    }

    return props.outputsByTerminal[primaryTerminalId.value] ?? []
  })

  const secondaryOutputs = computed(() => {
    if (!resolvedSecondaryTerminalId.value) {
      return []
    }

    return props.outputsByTerminal[resolvedSecondaryTerminalId.value] ?? []
  })

  const primaryTerminal = computed(() => {
    if (!primaryTerminalId.value) {
      return null
    }

    return terminalMap.value.get(primaryTerminalId.value) ?? null
  })

  const secondaryTerminal = computed(() => {
    if (!resolvedSecondaryTerminalId.value) {
      return null
    }

    return terminalMap.value.get(resolvedSecondaryTerminalId.value) ?? null
  })

  const splitCandidates = computed(() => {
    return terminalTabs.value.filter((terminal) => terminal.terminalId !== primaryTerminalId.value)
  })

  const splitStatusText = computed(() => {
    if (!splitEnabled.value) {
      return ''
    }

    if (pendingSplitCreation.value) {
      return t('terminal.creatingSplit')
    }

    if (resolvedSecondaryTerminalId.value) {
      return `${t('terminal.split')}: ${getLabel(resolvedSecondaryTerminalId.value)}`
    }

    return t('terminal.waitingSplit')
  })

  const commandTargetLabel = computed(() => {
    if (!commandTerminalId.value) {
      return t('terminal.noSessions')
    }

    return getLabel(commandTerminalId.value)
  })

  const commandTerminal = computed(() => {
    if (!commandTerminalId.value) {
      return null
    }

    return terminalMap.value.get(commandTerminalId.value) ?? null
  })

  const hasTerminalSessions = computed(() => terminalIdList.value.length > 0)

  function getLabel(terminalId: string): string {
    return terminalLabelMap.value.get(terminalId) ?? terminalId
  }

  function getTerminalStatusLabel(terminal: TerminalInfo | null): string {
    if (!terminal) {
      return ''
    }

    return t(`terminal.status.${terminal.status}`)
  }

  function getCompactPath(path: string): string {
    const normalized = path.replace(/\\/g, '/')
    const segments = normalized.split('/').filter(Boolean)
    if (segments.length <= 2) {
      return normalized
    }

    return segments.slice(-2).join('/')
  }

  function findAlternativeTerminalId(primaryId: string, preferredId = ''): string {
    if (preferredId && preferredId !== primaryId && terminalIdList.value.includes(preferredId)) {
      return preferredId
    }

    const fallback = props.terminals.find((terminal) => terminal.terminalId !== primaryId)
    return fallback?.terminalId ?? ''
  }

  function closeContextMenu(): void {
    contextMenu.visible = false
  }

  function persistTerminalPanelUiState(): void {
    if (typeof window === 'undefined') {
      return
    }

    try {
      window.localStorage.setItem(
        TERMINAL_PANEL_UI_STORAGE_KEY,
        JSON.stringify({
          shellInput: shellInput.value,
          cwdInput: cwdInput.value,
          showToolbar: showToolbar.value,
          splitEnabled: splitEnabled.value,
          secondaryTerminalId: secondaryTerminalId.value,
          focusedTerminalId: focusedTerminalId.value,
        } satisfies PersistedTerminalPanelUiState),
      )
    } catch {
      // ignore storage failures
    }
  }

  function onGlobalPointerDown(event: MouseEvent): void {
    if (!contextMenu.visible) {
      return
    }

    const target = event.target as HTMLElement | null
    if (target?.closest('.terminal-context-menu')) {
      return
    }

    closeContextMenu()
  }

  function onGlobalKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Escape' && contextMenu.visible) {
      closeContextMenu()
    }
  }

  function createTerminal(payload: { shell?: string; cwd?: string } = {}): void {
    actions.createTerminal(payload)
  }

  function createTerminalFromToolbar(): void {
    createTerminal({
      shell: shellInput.value.trim() || undefined,
      cwd: cwdInput.value.trim() || undefined,
    })
  }

  function useCurrentShell(): void {
    shellInput.value = commandTerminal.value?.shell ?? ''
  }

  function useCurrentCwd(): void {
    cwdInput.value = commandTerminal.value?.cwd ?? ''
  }

  function clearProfileInputs(): void {
    shellInput.value = ''
    cwdInput.value = ''
  }

  function focusTerminal(terminalId: string): void {
    focusedTerminalId.value = terminalId
    actions.selectTerminal(terminalId)
  }

  function selectTab(terminalId: string): void {
    focusTerminal(terminalId)
    closeContextMenu()
  }

  function stopTerminalById(terminalId: string): void {
    if (!terminalId) {
      return
    }

    if (focusedTerminalId.value === terminalId) {
      focusedTerminalId.value = findAlternativeTerminalId(terminalId)
    }

    actions.stopTerminal(terminalId)

    if (secondaryTerminalId.value === terminalId) {
      secondaryTerminalId.value = ''
    }

    closeContextMenu()
  }

  function stopActiveTerminal(): void {
    if (!commandTerminalId.value) {
      return
    }

    stopTerminalById(commandTerminalId.value)
  }

  function stopOtherTerminals(keepTerminalId: string): void {
    props.terminals
      .filter((terminal) => terminal.terminalId !== keepTerminalId)
      .forEach((terminal) => actions.stopTerminal(terminal.terminalId))

    if (secondaryTerminalId.value && secondaryTerminalId.value !== keepTerminalId) {
      secondaryTerminalId.value = ''
    }

    actions.selectTerminal(keepTerminalId)
    focusedTerminalId.value = keepTerminalId
    closeContextMenu()
  }

  function stopAllTerminals(): void {
    actions.stopAllTerminals()
    splitEnabled.value = false
    secondaryTerminalId.value = ''
    focusedTerminalId.value = ''
    pendingSplitCreation.value = false
    closeContextMenu()
  }

  function clearOutput(): void {
    if (!commandTerminalId.value) {
      return
    }

    actions.input({ terminalId: commandTerminalId.value, data: 'clear\r' })
  }

  function openTabContextMenu(event: MouseEvent, terminalId: string): void {
    event.preventDefault()
    event.stopPropagation()

    contextMenu.visible = true
    contextMenu.x = event.clientX
    contextMenu.y = event.clientY
    contextMenu.terminalId = terminalId

    focusTerminal(terminalId)
  }

  function splitWithTerminal(targetTerminalId: string): void {
    if (!primaryTerminalId.value) {
      return
    }

    splitEnabled.value = true

    const selected = findAlternativeTerminalId(primaryTerminalId.value, targetTerminalId)
    if (selected) {
      secondaryTerminalId.value = selected
      focusedTerminalId.value = selected
      pendingSplitCreation.value = false
      closeContextMenu()
      return
    }

    secondaryTerminalId.value = ''

    if (!pendingSplitCreation.value) {
      pendingSplitCreation.value = true
      createTerminal()
    }

    closeContextMenu()
  }

  function toggleSplit(): void {
    if (splitEnabled.value) {
      splitEnabled.value = false
      secondaryTerminalId.value = ''
      pendingSplitCreation.value = false
      return
    }

    splitWithTerminal('')
  }

  function closeSplit(): void {
    splitEnabled.value = false
    secondaryTerminalId.value = ''
    pendingSplitCreation.value = false
  }

  function changeSecondaryTerminal(event: Event): void {
    const target = event.target as HTMLSelectElement | null
    const nextTerminalId = target?.value ?? ''
    if (!nextTerminalId) {
      return
    }

    secondaryTerminalId.value = nextTerminalId
    focusedTerminalId.value = nextTerminalId
  }

  function copyTerminalId(terminalId: string): void {
    if (!terminalId || typeof navigator === 'undefined' || !navigator.clipboard) {
      closeContextMenu()
      return
    }

    navigator.clipboard.writeText(terminalId).finally(() => {
      closeContextMenu()
    })
  }

  watch(
    () => [primaryTerminalId.value, terminalIdList.value.join('|')],
    () => {
      if (!focusedTerminalId.value || !terminalIdList.value.includes(focusedTerminalId.value)) {
        focusedTerminalId.value = primaryTerminalId.value
      }

      if (!splitEnabled.value) {
        if (contextMenu.visible && contextMenu.terminalId && !terminalIdList.value.includes(contextMenu.terminalId)) {
          closeContextMenu()
        }
        return
      }

      if (pendingSplitCreation.value) {
        const candidate = findAlternativeTerminalId(primaryTerminalId.value, secondaryTerminalId.value)
        if (candidate) {
          secondaryTerminalId.value = candidate
          focusedTerminalId.value = candidate
          pendingSplitCreation.value = false
        }
        return
      }

      if (!resolvedSecondaryTerminalId.value) {
        const candidate = findAlternativeTerminalId(primaryTerminalId.value)
        if (candidate) {
          secondaryTerminalId.value = candidate
        } else {
          closeSplit()
        }
      }
    },
    { immediate: true },
  )

  watch(
    () => contextMenu.terminalId,
    () => {
      if (!contextMenu.terminalId) {
        return
      }

      if (!terminalIdList.value.includes(contextMenu.terminalId)) {
        closeContextMenu()
      }
    },
  )

  watch(
    () => showToolbar.value,
    () => {
      closeContextMenu()
    },
  )

  watch(
    () => [
      shellInput.value,
      cwdInput.value,
      showToolbar.value,
      splitEnabled.value,
      secondaryTerminalId.value,
      focusedTerminalId.value,
    ],
    () => {
      persistTerminalPanelUiState()
    },
    { deep: false },
  )

  watch(
    () => props.visible,
    (visible) => {
      if (!visible) {
        closeContextMenu()
      }
    },
  )

  onMounted(() => {
    window.addEventListener('mousedown', onGlobalPointerDown)
    window.addEventListener('keydown', onGlobalKeyDown)
    window.addEventListener('blur', closeContextMenu)
    window.addEventListener('resize', closeContextMenu)
  })

  onBeforeUnmount(() => {
    window.removeEventListener('mousedown', onGlobalPointerDown)
    window.removeEventListener('keydown', onGlobalKeyDown)
    window.removeEventListener('blur', closeContextMenu)
    window.removeEventListener('resize', closeContextMenu)
  })

  return {
    commandTargetLabel,
    commandTerminalId,
    contextMenu,
    copyTerminalId,
    cwdInput,
    clearOutput,
    clearProfileInputs,
    closeSplit,
    createTerminalFromToolbar,
    focusTerminal,
    getCompactPath,
    getLabel,
    getTerminalStatusLabel,
    hasTerminalSessions,
    openTabContextMenu,
    primaryOutputs,
    primaryTerminal,
    primaryTerminalId,
    secondaryOutputs,
    secondaryTerminal,
    secondaryTerminalId,
    resolvedSecondaryTerminalId,
    selectTab,
    sessionSummary,
    shellInput,
    showToolbar,
    splitActive,
    splitCandidates,
    splitEnabled,
    splitStatusText,
    splitWithTerminal,
    stopActiveTerminal,
    stopAllTerminals,
    stopOtherTerminals,
    stopTerminalById,
    t,
    terminalTabs,
    toggleSplit,
    useCurrentCwd,
    useCurrentShell,
    changeSecondaryTerminal,
  }
}
