import type { TerminalInfo } from '../../types'

export interface TerminalTab extends TerminalInfo {
  label: string
  isCommandTarget: boolean
}
