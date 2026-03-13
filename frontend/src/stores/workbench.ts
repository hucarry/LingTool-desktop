import { defineStore } from 'pinia'
import { ref } from 'vue'

const SIDEBAR_WIDTH_STORAGE_KEY = 'toolhub.sidebarWidth'
const SIDEBAR_VISIBLE_STORAGE_KEY = 'toolhub.sidebarVisible'
const TERMINAL_HEIGHT_STORAGE_KEY = 'toolhub.terminalHeight'
const TERMINAL_EXPANDED_STORAGE_KEY = 'toolhub.terminalExpanded'

export const ACTIVITY_BAR_WIDTH = 48
export const SIDEBAR_SASH_WIDTH = 4
export const MIN_EDITOR_WIDTH = 420
export const MIN_SIDEBAR_WIDTH = 180
export const DEFAULT_SIDEBAR_WIDTH = 248
export const MAX_SIDEBAR_WIDTH = 560
export const MIN_TERMINAL_HEIGHT = 140
export const DEFAULT_TERMINAL_HEIGHT = 260
export const COLLAPSED_TERMINAL_HEIGHT = 34

function loadSidebarWidth(): number {
  if (typeof window === 'undefined') {
    return DEFAULT_SIDEBAR_WIDTH
  }

  try {
    const raw = window.localStorage.getItem(SIDEBAR_WIDTH_STORAGE_KEY)
    const parsed = raw ? Number.parseInt(raw, 10) : Number.NaN
    if (Number.isFinite(parsed)) {
      return Math.max(MIN_SIDEBAR_WIDTH, Math.min(MAX_SIDEBAR_WIDTH, parsed))
    }
  } catch {
    // ignore
  }

  return DEFAULT_SIDEBAR_WIDTH
}

function loadSidebarVisible(): boolean {
  if (typeof window === 'undefined') {
    return true
  }

  try {
    return window.localStorage.getItem(SIDEBAR_VISIBLE_STORAGE_KEY) !== 'false'
  } catch {
    return true
  }
}

function loadTerminalHeight(): number {
  if (typeof window === 'undefined') {
    return DEFAULT_TERMINAL_HEIGHT
  }

  try {
    const raw = window.localStorage.getItem(TERMINAL_HEIGHT_STORAGE_KEY)
    const parsed = raw ? Number.parseInt(raw, 10) : Number.NaN
    if (Number.isFinite(parsed)) {
      return Math.max(MIN_TERMINAL_HEIGHT, parsed)
    }
  } catch {
    // ignore
  }

  return DEFAULT_TERMINAL_HEIGHT
}

function loadTerminalExpanded(): boolean {
  if (typeof window === 'undefined') {
    return true
  }

  try {
    return window.localStorage.getItem(TERMINAL_EXPANDED_STORAGE_KEY) !== 'false'
  } catch {
    return true
  }
}

export const useWorkbenchStore = defineStore('workbench', () => {
  const sidebarWidth = ref(loadSidebarWidth())
  const sidebarVisible = ref(loadSidebarVisible())
  const terminalHeight = ref(loadTerminalHeight())
  const terminalExpanded = ref(loadTerminalExpanded())

  function persistSidebarWidth(width: number): void {
    if (typeof window === 'undefined') {
      return
    }

    try {
      window.localStorage.setItem(SIDEBAR_WIDTH_STORAGE_KEY, String(width))
    } catch {
      // ignore
    }
  }

  function persistSidebarVisible(value: boolean): void {
    if (typeof window === 'undefined') {
      return
    }

    try {
      window.localStorage.setItem(SIDEBAR_VISIBLE_STORAGE_KEY, String(value))
    } catch {
      // ignore
    }
  }

  function persistTerminalState(): void {
    if (typeof window === 'undefined') {
      return
    }

    try {
      window.localStorage.setItem(TERMINAL_HEIGHT_STORAGE_KEY, String(terminalHeight.value))
      window.localStorage.setItem(TERMINAL_EXPANDED_STORAGE_KEY, String(terminalExpanded.value))
    } catch {
      // ignore
    }
  }

  function setSidebarWidth(nextWidth: number, maxWidth = MAX_SIDEBAR_WIDTH): void {
    const normalized = Math.max(MIN_SIDEBAR_WIDTH, Math.min(maxWidth, Math.round(nextWidth)))
    sidebarWidth.value = normalized
    persistSidebarWidth(normalized)
  }

  function setSidebarVisible(nextVisible: boolean): void {
    sidebarVisible.value = nextVisible
    persistSidebarVisible(nextVisible)
  }

  function toggleSidebarVisible(): void {
    setSidebarVisible(!sidebarVisible.value)
  }

  function setTerminalHeight(nextHeight: number): void {
    terminalHeight.value = Math.max(MIN_TERMINAL_HEIGHT, Math.round(nextHeight))
    persistTerminalState()
  }

  function setTerminalExpanded(nextExpanded: boolean): void {
    terminalExpanded.value = nextExpanded
    persistTerminalState()
  }

  function toggleTerminalExpanded(): void {
    setTerminalExpanded(!terminalExpanded.value)
  }

  return {
    sidebarWidth,
    sidebarVisible,
    terminalHeight,
    terminalExpanded,
    setSidebarWidth,
    setSidebarVisible,
    toggleSidebarVisible,
    setTerminalHeight,
    setTerminalExpanded,
    toggleTerminalExpanded,
  }
})
