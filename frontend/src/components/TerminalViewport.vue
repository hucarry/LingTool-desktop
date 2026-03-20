<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { Terminal } from '@xterm/xterm'
import { FitAddon } from '@xterm/addon-fit'
import '@xterm/xterm/css/xterm.css'
import { useSettingsStore } from '../stores/settings'

const props = defineProps<{
  terminalId: string
  outputs: string[]
}>()

const emit = defineEmits<{
  (e: 'input', payload: { terminalId: string; data: string }): void
  (e: 'resize', payload: { terminalId: string; cols: number; rows: number }): void
  (e: 'focus', terminalId: string): void
}>()

const hostRef = ref<HTMLElement>()
const renderedChunks = ref(0)
const settingsStore = useSettingsStore()
const { theme } = storeToRefs(settingsStore)

let term: Terminal | null = null
let fitAddon: FitAddon | null = null
let resizeObserver: ResizeObserver | null = null

function readThemeVar(name: string, fallback: string): string {
  if (typeof document === 'undefined') {
    return fallback
  }

  const value = window.getComputedStyle(document.documentElement).getPropertyValue(name).trim()
  return value || fallback
}

function resolveTerminalTheme() {
  return {
    background: readThemeVar('--terminal-bg', '#1e1e1e'),
    foreground: readThemeVar('--terminal-foreground', '#cccccc'),
    cursor: readThemeVar('--terminal-cursor', '#aeafad'),
    selectionBackground: readThemeVar('--terminal-selection', '#264f78'),
    black: readThemeVar('--terminal-black', '#000000'),
    red: readThemeVar('--terminal-red', '#cd3131'),
    green: readThemeVar('--terminal-green', '#0dbc79'),
    yellow: readThemeVar('--terminal-yellow', '#e5e510'),
    blue: readThemeVar('--terminal-blue', '#2472c8'),
    magenta: readThemeVar('--terminal-magenta', '#bc3fbc'),
    cyan: readThemeVar('--terminal-cyan', '#11a8cd'),
    white: readThemeVar('--terminal-white', '#e5e5e5'),
    brightBlack: readThemeVar('--terminal-bright-black', '#666666'),
    brightRed: readThemeVar('--terminal-bright-red', '#f14c4c'),
    brightGreen: readThemeVar('--terminal-bright-green', '#23d18b'),
    brightYellow: readThemeVar('--terminal-bright-yellow', '#f5f543'),
    brightBlue: readThemeVar('--terminal-bright-blue', '#3b8eea'),
    brightMagenta: readThemeVar('--terminal-bright-magenta', '#d670d6'),
    brightCyan: readThemeVar('--terminal-bright-cyan', '#29b8db'),
    brightWhite: readThemeVar('--terminal-bright-white', '#e5e5e5'),
  }
}

function applyTerminalTheme(): void {
  if (!term) {
    return
  }

  term.options.theme = resolveTerminalTheme()
}

function setupTerminal(): void {
  term = new Terminal({
    convertEol: true,
    cursorBlink: true,
    fontFamily: "Consolas, 'Courier New', monospace",
    fontSize: 13,
    lineHeight: 1.25,
    scrollback: 10000,
    theme: resolveTerminalTheme(),
  })

  fitAddon = new FitAddon()
  term.loadAddon(fitAddon)

  if (hostRef.value) {
    term.open(hostRef.value)
    fitAddon.fit()
  }

  term.onData((data) => {
    if (!props.terminalId) {
      return
    }

    emit('input', {
      terminalId: props.terminalId,
      data,
    })
  })

  term.onResize(({ cols, rows }) => {
    if (!props.terminalId || cols <= 0 || rows <= 0) {
      return
    }

    emit('resize', {
      terminalId: props.terminalId,
      cols,
      rows,
    })
  })

  if (hostRef.value) {
    resizeObserver = new ResizeObserver(() => {
      fitTerminal()
    })
    resizeObserver.observe(hostRef.value)
  }
}

function teardownTerminal(): void {
  resizeObserver?.disconnect()
  resizeObserver = null
  term?.dispose()
  term = null
  fitAddon?.dispose()
  fitAddon = null
}

function fitTerminal(): void {
  fitAddon?.fit()
}

function redrawAll(): void {
  if (!term) {
    return
  }

  term.reset()
  renderedChunks.value = 0
  writeNewChunks()
}

function writeNewChunks(): void {
  if (!term) {
    return
  }

  if (props.outputs.length < renderedChunks.value) {
    renderedChunks.value = 0
    term.reset()
  }

  for (let index = renderedChunks.value; index < props.outputs.length; index += 1) {
    const chunk = props.outputs[index]
    if (typeof chunk === 'string') {
      term.write(chunk)
    }
  }

  renderedChunks.value = props.outputs.length
}

function onFocus(): void {
  if (!props.terminalId) {
    return
  }

  emit('focus', props.terminalId)
}

onMounted(async () => {
  setupTerminal()
  await nextTick()
  redrawAll()
  fitTerminal()
  window.addEventListener('resize', fitTerminal)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', fitTerminal)
  teardownTerminal()
})

watch(
  () => props.terminalId,
  async () => {
    await nextTick()
    redrawAll()
    fitTerminal()
  },
)

watch(
  () => props.outputs.length,
  () => {
    writeNewChunks()
  },
)

watch(theme, async () => {
  await nextTick()
  applyTerminalTheme()
})
</script>

<template>
  <div ref="hostRef" class="terminal-viewport" @mousedown="onFocus" />
</template>

<style scoped>
.terminal-viewport {
  width: 100%;
  height: 100%;
  min-height: 0;
  overflow: hidden;
  background: var(--terminal-bg);
}

:deep(.xterm) {
  height: 100%;
  padding: 4px 8px;
}

:deep(.xterm-viewport) {
  overflow-y: auto;
}
</style>
