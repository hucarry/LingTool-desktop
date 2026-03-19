import { nextTick } from 'vue'
import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { useSettingsStore } from '../settings'

describe('settings store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.localStorage.clear()
    document.documentElement.removeAttribute('data-theme')
  })

  it('applies and persists theme changes', async () => {
    const store = useSettingsStore()

    expect(document.documentElement.dataset.theme).toBe('dark')

    store.setTheme('light')
    await nextTick()

    expect(document.documentElement.dataset.theme).toBe('light')
    expect(window.localStorage.getItem('toolhub.theme')).toBe('light')
  })

  it('persists locale and default runtime paths', async () => {
    const store = useSettingsStore()

    store.setLocale('en-US')
    store.setDefaultPythonPath('C:/Python/python.exe')
    store.setDefaultNodePath('C:/Node/node.exe')
    await nextTick()

    expect(window.localStorage.getItem('toolhub.locale')).toBe('en-US')
    expect(window.localStorage.getItem('toolhub.defaultPythonPath')).toBe('C:/Python/python.exe')
    expect(window.localStorage.getItem('toolhub.defaultNodePath')).toBe('C:/Node/node.exe')
  })

  it('falls back to the app bundled Python when no manual path is set', async () => {
    const store = useSettingsStore()

    store.setAppDefaultPythonPath('D:/Apps/ToolHub/python/python.exe')
    await nextTick()

    expect(store.defaultPythonPath).toBe('D:/Apps/ToolHub/python/python.exe')

    store.setDefaultPythonPath('C:/Python/python.exe')
    await nextTick()
    expect(store.defaultPythonPath).toBe('C:/Python/python.exe')

    store.clearDefaultPythonPath()
    await nextTick()
    expect(store.defaultPythonPath).toBe('D:/Apps/ToolHub/python/python.exe')
  })
})
