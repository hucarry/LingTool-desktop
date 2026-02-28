<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { Terminal } from '@xterm/xterm'
import { FitAddon } from '@xterm/addon-fit'
import '@xterm/xterm/css/xterm.css'

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

let term: Terminal | null = null
let fitAddon: FitAddon | null = null
let resizeObserver: ResizeObserver | null = null

function setupTerminal(): void {
  term = new Terminal({
    convertEol: true,
    cursorBlink: true,
    fontFamily: "Consolas, 'Courier New', monospace",
    fontSize: 13,
    lineHeight: 1.25,
    scrollback: 10000,
    theme: {
      background: '#1e1e1e',
      foreground: '#cccccc',
      cursor: '#aeafad',
      selectionBackground: '#264f78',
      black: '#000000',
      red: '#cd3131',
      green: '#0dbc79',
      yellow: '#e5e510',
      blue: '#2472c8',
      magenta: '#bc3fbc',
      cyan: '#11a8cd',
      white: '#e5e5e5',
      brightBlack: '#666666',
      brightRed: '#f14c4c',
      brightGreen: '#23d18b',
      brightYellow: '#f5f543',
      brightBlue: '#3b8eea',
      brightMagenta: '#d670d6',
      brightCyan: '#29b8db',
      brightWhite: '#e5e5e5',
    },
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
  background: #1e1e1e;
}

:deep(.xterm) {
  height: 100%;
  padding: 4px 8px;
}

:deep(.xterm-viewport) {
  overflow-y: auto;
}
</style>
