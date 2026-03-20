import { onBeforeUnmount, onMounted, watch } from 'vue'

import { useI18n } from './useI18n'
import { useTerminalPanelActions } from './terminalPanel/useTerminalPanelActions'
import { useTerminalPanelComputed } from './terminalPanel/useTerminalPanelComputed'
import { useTerminalPanelUiState } from './terminalPanel/useTerminalPanelUiState'
import type { TerminalPanelActions, TerminalPanelProps } from './terminalPanel/types'

export type { TerminalPanelActions, TerminalPanelProps } from './terminalPanel/types'

export function useTerminalPanelState(options: {
  props: TerminalPanelProps
  actions: TerminalPanelActions
}) {
  const { props, actions } = options
  const { t, formatSessionCount } = useI18n()

  const {
    shellInput,
    cwdInput,
    showToolbar,
    splitEnabled,
    secondaryTerminalId,
    focusedTerminalId,
    pendingSplitCreation,
    persistTerminalPanelUiState,
  } = useTerminalPanelUiState()

  const computedState = useTerminalPanelComputed({
    props,
    splitEnabled,
    secondaryTerminalId,
    focusedTerminalId,
    pendingSplitCreation,
    t,
    formatSessionCount,
  })

  const actionState = useTerminalPanelActions({
    props,
    actions,
    shellInput,
    cwdInput,
    splitEnabled,
    secondaryTerminalId,
    focusedTerminalId,
    pendingSplitCreation,
    primaryTerminalId: computedState.primaryTerminalId,
    commandTerminalId: computedState.commandTerminalId,
    commandTerminal: computedState.commandTerminal,
    resolvedSecondaryTerminalId: computedState.resolvedSecondaryTerminalId,
    terminalIdList: computedState.terminalIdList,
    findAlternativeTerminalId: computedState.findAlternativeTerminalId,
  })

  watch(
    () => [computedState.primaryTerminalId.value, computedState.terminalIdList.value.join('|')],
    () => {
      actionState.syncActiveStateOnTerminalChange()
    },
    { immediate: true },
  )

  watch(
    () => actionState.contextMenu.terminalId,
    () => {
      actionState.ensureContextMenuTerminalExists()
    },
  )

  watch(
    () => showToolbar.value,
    () => {
      actionState.closeContextMenu()
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
  )

  watch(
    () => props.visible,
    (visible) => {
      if (!visible) {
        actionState.closeContextMenu()
      }
    },
  )

  onMounted(() => {
    window.addEventListener('mousedown', actionState.onGlobalPointerDown)
    window.addEventListener('keydown', actionState.onGlobalKeyDown)
    window.addEventListener('blur', actionState.closeContextMenu)
    window.addEventListener('resize', actionState.closeContextMenu)
  })

  onBeforeUnmount(() => {
    window.removeEventListener('mousedown', actionState.onGlobalPointerDown)
    window.removeEventListener('keydown', actionState.onGlobalKeyDown)
    window.removeEventListener('blur', actionState.closeContextMenu)
    window.removeEventListener('resize', actionState.closeContextMenu)
  })

  return {
    commandTargetLabel: computedState.commandTargetLabel,
    commandTerminalId: computedState.commandTerminalId,
    contextMenu: actionState.contextMenu,
    copyTerminalId: actionState.copyTerminalId,
    cwdInput,
    clearOutput: actionState.clearOutput,
    clearProfileInputs: actionState.clearProfileInputs,
    closeSplit: actionState.closeSplit,
    createTerminalFromToolbar: actionState.createTerminalFromToolbar,
    focusTerminal: actionState.focusTerminal,
    getCompactPath: computedState.getCompactPath,
    getLabel: computedState.getLabel,
    getTerminalStatusLabel: computedState.getTerminalStatusLabel,
    hasTerminalSessions: computedState.hasTerminalSessions,
    openTabContextMenu: actionState.openTabContextMenu,
    primaryOutputs: computedState.primaryOutputs,
    primaryTerminal: computedState.primaryTerminal,
    primaryTerminalId: computedState.primaryTerminalId,
    secondaryOutputs: computedState.secondaryOutputs,
    secondaryTerminal: computedState.secondaryTerminal,
    secondaryTerminalId,
    resolvedSecondaryTerminalId: computedState.resolvedSecondaryTerminalId,
    selectTab: actionState.selectTab,
    sessionSummary: computedState.sessionSummary,
    shellInput,
    showToolbar,
    splitActive: computedState.splitActive,
    splitCandidates: computedState.splitCandidates,
    splitEnabled,
    splitStatusText: computedState.splitStatusText,
    splitWithTerminal: actionState.splitWithTerminal,
    stopActiveTerminal: actionState.stopActiveTerminal,
    stopAllTerminals: actionState.stopAllTerminals,
    stopOtherTerminals: actionState.stopOtherTerminals,
    stopTerminalById: actionState.stopTerminalById,
    t,
    terminalTabs: computedState.terminalTabs,
    toggleSplit: actionState.toggleSplit,
    useCurrentCwd: actionState.useCurrentCwd,
    useCurrentShell: actionState.useCurrentShell,
    changeSecondaryTerminal: actionState.changeSecondaryTerminal,
  }
}
