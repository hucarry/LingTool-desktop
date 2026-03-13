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

  it('persists locale and default python path', async () => {
    const store = useSettingsStore()

    store.setLocale('en-US')
    store.setDefaultPythonPath('C:/Python/python.exe')
    await nextTick()

    expect(window.localStorage.getItem('toolhub.locale')).toBe('en-US')
    expect(window.localStorage.getItem('toolhub.defaultPythonPath')).toBe('C:/Python/python.exe')
  })
})
