import type { Ref } from 'vue'

import type { TerminalInfo } from '../../types'

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

export interface ContextMenuState {
  visible: boolean
  x: number
  y: number
  terminalId: string
}

export interface PersistedTerminalPanelUiState {
  shellInput: string
  cwdInput: string
  showToolbar: boolean
  splitEnabled: boolean
  secondaryTerminalId: string
  focusedTerminalId: string
}

export interface TerminalPanelUiStateRefs {
  shellInput: Ref<string>
  cwdInput: Ref<string>
  showToolbar: Ref<boolean>
  splitEnabled: Ref<boolean>
  secondaryTerminalId: Ref<string>
  focusedTerminalId: Ref<string>
  pendingSplitCreation: Ref<boolean>
}
