import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { useWorkbenchStore } from '../workbench'

describe('workbench store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.localStorage.clear()
  })

  it('persists sidebar and terminal settings', () => {
    const store = useWorkbenchStore()

    store.setSidebarWidth(320)
    store.setSidebarVisible(false)
    store.setTerminalHeight(300)
    store.setTerminalExpanded(false)

    expect(window.localStorage.getItem('toolhub.sidebarWidth')).toBe('320')
    expect(window.localStorage.getItem('toolhub.sidebarVisible')).toBe('false')
    expect(window.localStorage.getItem('toolhub.terminalHeight')).toBe('300')
    expect(window.localStorage.getItem('toolhub.terminalExpanded')).toBe('false')
  })
})
