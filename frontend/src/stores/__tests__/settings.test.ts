import { nextTick } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { useSettingsStore } from '../settings'

function setExternalSender(spy: ReturnType<typeof vi.fn>) {
  Object.defineProperty(window, 'external', {
    value: { sendMessage: spy },
    configurable: true,
    writable: true,
  })
}

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

  it('stores app-level path presets from backend defaults', async () => {
    const store = useSettingsStore()

    store.setAppRootPath('D:/Apps/ToolHub')
    store.setDesktopPath('C:/Users/demo/Desktop')
    await nextTick()

    expect(store.appRootPath).toBe('D:/Apps/ToolHub')
    expect(store.desktopPath).toBe('C:/Users/demo/Desktop')
  })

  it('sends a diagnostic bundle export request and tracks completion', () => {
    const sendSpy = vi.fn()
    setExternalSender(sendSpy)

    const store = useSettingsStore()
    store.exportDiagnosticBundle()

    expect(store.diagnosticsExporting).toBe(true)
    expect(JSON.parse(sendSpy.mock.calls[0]![0]!)).toEqual({
      type: 'exportDiagnosticBundle',
    })

    store.completeDiagnosticExport('D:/Diagnostics/toolhub.zip')

    expect(store.diagnosticsExporting).toBe(false)
    expect(store.lastDiagnosticBundlePath).toBe('D:/Diagnostics/toolhub.zip')
  })
})
