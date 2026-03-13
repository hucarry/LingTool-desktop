import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { useTerminalsStore } from '../terminals'
import type { TerminalOutputMessage, TerminalsMessage } from '../../types'

describe('terminals store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.localStorage.clear()
    vi.useFakeTimers()
  })

  it('persists the active terminal id', () => {
    const store = useTerminalsStore()
    const message: TerminalsMessage = {
      type: 'terminals',
      terminals: [
        { terminalId: 'a', shell: 'pwsh', cwd: 'C:/a', status: 'running', startTime: '2024-01-01T00:00:00Z' },
      ],
    }

    store.handleTerminalsMessage(message)
    store.selectTerminal('a')

    expect(window.localStorage.getItem('toolhub.activeTerminalId')).toBe('a')
  })

  it('caps buffered output per terminal', () => {
    const store = useTerminalsStore()
    const terminalId = 'a'
    const baseMessage: TerminalOutputMessage = {
      type: 'terminalOutput',
      terminalId,
      data: 'chunk',
      channel: 'stdout',
      ts: '2024-01-01T00:00:00Z',
    }

    for (let index = 0; index < 10050; index += 1) {
      store.handleTerminalOutput({ ...baseMessage, data: `chunk-${index}` })
    }

    expect(store.terminalOutputsById[terminalId]).toHaveLength(10000)
    expect(store.terminalOutputsById[terminalId]?.[0]).toBe('chunk-50')
  })
})
