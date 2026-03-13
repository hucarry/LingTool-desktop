import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

import { bridge } from '../services/bridge'

export type Theme = 'dark' | 'light'
export type AppLocale = 'zh-CN' | 'en-US'

const THEME_KEY = 'toolhub.theme'
const LOCALE_KEY = 'toolhub.locale'
const PYTHON_PATH_KEY = 'toolhub.defaultPythonPath'
const NODE_PATH_KEY = 'toolhub.defaultNodePath'

function loadTheme(): Theme {
  if (typeof window === 'undefined') {
    return 'dark'
  }

  try {
    const saved = window.localStorage.getItem(THEME_KEY)
    return saved === 'light' ? 'light' : 'dark'
  } catch {
    return 'dark'
  }
}

function loadLocale(): AppLocale {
  if (typeof window === 'undefined') {
    return 'zh-CN'
  }

  try {
    const saved = window.localStorage.getItem(LOCALE_KEY)
    return saved === 'en-US' ? 'en-US' : 'zh-CN'
  } catch {
    return 'zh-CN'
  }
}

function loadPythonPath(): string {
  if (typeof window === 'undefined') {
    return ''
  }

  try {
    return window.localStorage.getItem(PYTHON_PATH_KEY) ?? ''
  } catch {
    return ''
  }
}

function loadNodePath(): string {
  if (typeof window === 'undefined') {
    return ''
  }

  try {
    return window.localStorage.getItem(NODE_PATH_KEY) ?? ''
  } catch {
    return ''
  }
}

function applyTheme(theme: Theme): void {
  if (typeof document === 'undefined') {
    return
  }

  document.documentElement.dataset.theme = theme
}

export const useSettingsStore = defineStore('settings', () => {
  const theme = ref<Theme>(loadTheme())
  const locale = ref<AppLocale>(loadLocale())
  const defaultPythonPath = ref(loadPythonPath())
  const defaultNodePath = ref(loadNodePath())

  watch(
    theme,
    (nextTheme) => {
      applyTheme(nextTheme)

      if (typeof window === 'undefined') {
        return
      }

      try {
        window.localStorage.setItem(THEME_KEY, nextTheme)
      } catch {
        // ignore
      }
    },
    { immediate: true },
  )

  watch(locale, (nextLocale) => {
    if (typeof window === 'undefined') {
      return
    }

    try {
      window.localStorage.setItem(LOCALE_KEY, nextLocale)
    } catch {
      // ignore
    }
  })

  watch(defaultPythonPath, (nextPath) => {
    if (typeof window === 'undefined') {
      return
    }

    try {
      window.localStorage.setItem(PYTHON_PATH_KEY, nextPath)
    } catch {
      // ignore
    }
  })

  watch(defaultNodePath, (nextPath) => {
    if (typeof window === 'undefined') {
      return
    }

    try {
      window.localStorage.setItem(NODE_PATH_KEY, nextPath)
    } catch {
      // ignore
    }
  })

  function setTheme(nextTheme: Theme): void {
    theme.value = nextTheme
  }

  function setLocale(nextLocale: AppLocale): void {
    locale.value = nextLocale
  }

  function setDefaultPythonPath(path: string): void {
    defaultPythonPath.value = path.trim()
  }

  function clearDefaultPythonPath(): void {
    defaultPythonPath.value = ''
  }

  function setDefaultNodePath(path: string): void {
    defaultNodePath.value = path.trim()
  }

  function clearDefaultNodePath(): void {
    defaultNodePath.value = ''
  }

  function pickDefaultPython(): void {
    bridge.send({
      type: 'browsePython',
      defaultPath: defaultPythonPath.value || undefined,
      purpose: 'settingsDefaultPython',
    })
  }

  function pickDefaultNode(): void {
    bridge.send({
      type: 'browseFile',
      defaultPath: defaultNodePath.value || undefined,
      filter: 'exe,cmd,bat',
      purpose: 'settingsDefaultNode',
    })
  }

  return {
    theme,
    locale,
    defaultPythonPath,
    defaultNodePath,
    setTheme,
    setLocale,
    setDefaultPythonPath,
    clearDefaultPythonPath,
    setDefaultNodePath,
    clearDefaultNodePath,
    pickDefaultPython,
    pickDefaultNode,
  }
})
