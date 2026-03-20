import { reactive } from 'vue'

import type { TerminalInfo } from '../../types'
import type {
  ContextMenuState,
  TerminalPanelActions,
  TerminalPanelProps,
  TerminalPanelUiStateRefs,
} from './types'

interface UseTerminalPanelActionsOptions {
  props: TerminalPanelProps
  actions: TerminalPanelActions
  shellInput: TerminalPanelUiStateRefs['shellInput']
  cwdInput: TerminalPanelUiStateRefs['cwdInput']
  splitEnabled: TerminalPanelUiStateRefs['splitEnabled']
  secondaryTerminalId: TerminalPanelUiStateRefs['secondaryTerminalId']
  focusedTerminalId: TerminalPanelUiStateRefs['focusedTerminalId']
  pendingSplitCreation: TerminalPanelUiStateRefs['pendingSplitCreation']
  primaryTerminalId: { value: string }
  commandTerminalId: { value: string }
  commandTerminal: { value: TerminalInfo | null }
  resolvedSecondaryTerminalId: { value: string }
  terminalIdList: { value: string[] }
  findAlternativeTerminalId: (primaryId: string, preferredId?: string) => string
}

export function useTerminalPanelActions(options: UseTerminalPanelActionsOptions) {
  const {
    props,
    actions,
    shellInput,
    cwdInput,
    splitEnabled,
    secondaryTerminalId,
    focusedTerminalId,
    pendingSplitCreation,
    primaryTerminalId,
    commandTerminalId,
    commandTerminal,
    resolvedSecondaryTerminalId,
    terminalIdList,
    findAlternativeTerminalId,
  } = options

  const contextMenu = reactive<ContextMenuState>({
    visible: false,
    x: 0,
    y: 0,
    terminalId: '',
  })

  function closeContextMenu(): void {
    contextMenu.visible = false
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

  function syncActiveStateOnTerminalChange(): void {
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
  }

  function ensureContextMenuTerminalExists(): void {
    if (!contextMenu.terminalId) {
      return
    }

    if (!terminalIdList.value.includes(contextMenu.terminalId)) {
      closeContextMenu()
    }
  }

  return {
    changeSecondaryTerminal,
    clearOutput,
    clearProfileInputs,
    closeContextMenu,
    closeSplit,
    contextMenu,
    copyTerminalId,
    createTerminalFromToolbar,
    focusTerminal,
    onGlobalKeyDown,
    onGlobalPointerDown,
    openTabContextMenu,
    selectTab,
    splitWithTerminal,
    stopActiveTerminal,
    stopAllTerminals,
    stopOtherTerminals,
    stopTerminalById,
    syncActiveStateOnTerminalChange,
    ensureContextMenuTerminalExists,
    toggleSplit,
    useCurrentCwd,
    useCurrentShell,
  }
}
