import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { useTerminalsStore } from '../terminals'
import type { TerminalOutputMessage, TerminalsMessage } from '../../types'

function setExternalSender(spy: ReturnType<typeof vi.fn>) {
  Object.defineProperty(window, 'external', {
    value: { sendMessage: spy },
    configurable: true,
    writable: true,
  })
}

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

  it('sends tool runtime context when creating a terminal', () => {
    const sendSpy = vi.fn()
    setExternalSender(sendSpy)

    const store = useTerminalsStore()
    store.createTerminal({
      title: '测试脚本',
      shell: 'powershell.exe',
      cwd: 'D:/Tools',
      toolType: 'python',
      runtimePath: 'D:/Tools/.venv/Scripts/python.exe',
    })

    expect(sendSpy).toHaveBeenCalledTimes(1)
    expect(JSON.parse(sendSpy.mock.calls[0]![0]!)).toEqual({
      type: 'startTerminal',
      title: '测试脚本',
      shell: 'powershell.exe',
      cwd: 'D:/Tools',
      toolType: 'python',
      runtimePath: 'D:/Tools/.venv/Scripts/python.exe',
    })
  })

  it('does not auto create a terminal when the backend reports none', () => {
    const sendSpy = vi.fn()
    setExternalSender(sendSpy)

    const store = useTerminalsStore()
    store.handleTerminalsMessage({
      type: 'terminals',
      terminals: [],
    })

    expect(sendSpy).not.toHaveBeenCalled()
    expect(store.terminals).toHaveLength(0)
    expect(store.activeTerminalId).toBe('')
  })
})
