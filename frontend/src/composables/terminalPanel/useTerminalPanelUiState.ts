import { ref } from 'vue'

import type {
  PersistedTerminalPanelUiState,
  TerminalPanelUiStateRefs,
} from './types'

const TERMINAL_PANEL_UI_STORAGE_KEY = 'toolhub.terminalPanelUi'

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

export function useTerminalPanelUiState(): TerminalPanelUiStateRefs & {
  persistTerminalPanelUiState(): void
} {
  const persistedUiState = loadPersistedTerminalPanelUiState()

  const shellInput = ref(persistedUiState.shellInput)
  const cwdInput = ref(persistedUiState.cwdInput)
  const showToolbar = ref(persistedUiState.showToolbar)
  const splitEnabled = ref(persistedUiState.splitEnabled)
  const secondaryTerminalId = ref(persistedUiState.secondaryTerminalId)
  const focusedTerminalId = ref(persistedUiState.focusedTerminalId)
  const pendingSplitCreation = ref(false)

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

  return {
    shellInput,
    cwdInput,
    showToolbar,
    splitEnabled,
    secondaryTerminalId,
    focusedTerminalId,
    pendingSplitCreation,
    persistTerminalPanelUiState,
  }
}
