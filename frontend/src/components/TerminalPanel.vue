<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { Terminal } from '@xterm/xterm'
import { FitAddon } from '@xterm/addon-fit'
import '@xterm/xterm/css/xterm.css'
import type { TerminalInfo } from '../types'

const props = defineProps<{
  terminals: TerminalInfo[]
  activeTerminalId: string
  outputs: string[]
}>()

const emit = defineEmits<{
  (e: 'selectTerminal', terminalId: string): void
  (e: 'createTerminal', payload: { shell?: string; cwd?: string }): void
  (e: 'stopTerminal', terminalId: string): void
  (e: 'stopAllTerminals'): void
  (e: 'input', payload: { terminalId: string; data: string }): void
  (e: 'resizeTerminal', payload: { terminalId: string; cols: number; rows: number }): void
  (e: 'clearOutput', terminalId: string): void
}>()

const hostRef = ref<HTMLElement>()
const shellInput = ref('')
const cwdInput = ref('')
const renderedChunks = ref(0)
const showToolbar = ref(false)

const terminalTabs = computed(() => {
  return props.terminals.map((terminal, index) => {
    const shellName = terminal.shell.split(/[\\/]/).pop() || terminal.shell || 'shell'
    const order = props.terminals.length - index
    return {
      ...terminal,
      label: `${shellName} ${order}`,
    }
  })
})

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
    if (!props.activeTerminalId) {
      return
    }

    emit('input', {
      terminalId: props.activeTerminalId,
      data,
    })
  })

  term.onResize(({ cols, rows }) => {
    if (props.activeTerminalId && cols > 0 && rows > 0) {
      emit('resizeTerminal', {
        terminalId: props.activeTerminalId,
        cols,
        rows,
      })
    }
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

function createTerminal(): void {
  emit('createTerminal', {
    shell: shellInput.value.trim() || undefined,
    cwd: cwdInput.value.trim() || undefined,
  })
}

function stopTerminal(): void {
  if (!props.activeTerminalId) {
    return
  }

  emit('stopTerminal', props.activeTerminalId)
}

function stopAllTerminals(): void {
  emit('stopAllTerminals')
}

function clearOutput(): void {
  if (!props.activeTerminalId) {
    return
  }

  emit('clearOutput', props.activeTerminalId)
  term?.reset()
  renderedChunks.value = 0
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
  () => props.activeTerminalId,
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

watch(
  () => showToolbar.value,
  async () => {
    await nextTick()
    fitTerminal()
  },
)
</script>

<template>
  <section class="terminal-panel">
    <header class="terminal-toolbar">
      <div class="terminal-tab-strip">
        <span class="panel-tab is-active">TERMINAL</span>

        <button
          v-for="terminal in terminalTabs"
          :key="terminal.terminalId"
          class="terminal-instance"
          :class="{ active: terminal.terminalId === activeTerminalId }"
          type="button"
          @click="emit('selectTerminal', terminal.terminalId)"
        >
          <span class="terminal-state" :class="terminal.status" />
          <span class="terminal-label">{{ terminal.label }}</span>
        </button>

        <span v-if="terminalTabs.length === 0" class="terminal-empty">No terminal sessions</span>
      </div>

      <div class="toolbar-actions">
        <button class="toolbar-action" type="button" @click="showToolbar = !showToolbar">
          {{ showToolbar ? 'Hide Profile' : 'New Profile' }}
        </button>
        <button class="toolbar-action" type="button" @click="createTerminal">+</button>
        <button class="toolbar-action" type="button" :disabled="!activeTerminalId" @click="clearOutput">Clear</button>
        <button class="toolbar-action danger" type="button" :disabled="!activeTerminalId" @click="stopTerminal">Kill</button>
        <button
          v-if="terminals.length > 1"
          class="toolbar-action danger"
          type="button"
          @click="stopAllTerminals"
        >
          Kill All
        </button>
      </div>
    </header>

    <div v-show="showToolbar" class="advanced-toolbar">
      <label class="profile-field">
        <span>Shell</span>
        <input v-model="shellInput" type="text" placeholder="powershell.exe" />
      </label>

      <label class="profile-field">
        <span>Working Directory</span>
        <input v-model="cwdInput" type="text" placeholder="C:\\project" />
      </label>

      <button class="profile-create" type="button" @click="createTerminal">Create Terminal</button>
    </div>

    <div ref="hostRef" class="terminal-host" />
  </section>
</template>

<style scoped>
.terminal-panel {
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;
  background: var(--vscode-panel-bg);
}

.terminal-toolbar {
  height: 32px;
  flex-shrink: 0;
  border-bottom: 1px solid var(--vscode-border-color);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 0 8px;
  overflow: hidden;
}

.terminal-tab-strip {
  min-width: 0;
  flex: 1;
  display: flex;
  align-items: center;
  gap: 6px;
  overflow-x: auto;
}

.panel-tab {
  font-size: 11px;
  color: var(--vscode-text-muted);
  letter-spacing: 0.8px;
  white-space: nowrap;
}

.panel-tab.is-active {
  color: var(--vscode-text-primary);
}

.terminal-instance {
  border: 0;
  background: transparent;
  color: var(--vscode-text-muted);
  font-size: 11px;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 0 8px;
  height: 24px;
  border-radius: 3px;
  cursor: pointer;
  white-space: nowrap;
}

.terminal-instance:hover {
  background: var(--vscode-hover-bg);
  color: var(--vscode-text-primary);
}

.terminal-instance.active {
  background: var(--vscode-active-bg);
  color: var(--vscode-text-primary);
}

.terminal-state {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #808080;
  flex-shrink: 0;
}

.terminal-state.running {
  background: #0dbc79;
}

.terminal-state.stopped,
.terminal-state.exited {
  background: #c5c5c5;
}

.terminal-state.failed {
  background: #f14c4c;
}

.terminal-empty {
  color: var(--vscode-text-muted);
  font-size: 11px;
}

.toolbar-actions {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  flex-shrink: 0;
}

.toolbar-action {
  border: 1px solid transparent;
  background: transparent;
  color: var(--vscode-text-muted);
  height: 22px;
  min-width: 22px;
  padding: 0 8px;
  border-radius: 3px;
  cursor: pointer;
  font-size: 11px;
}

.toolbar-action:hover:not(:disabled) {
  background: var(--vscode-hover-bg);
  color: var(--vscode-text-primary);
}

.toolbar-action:disabled {
  cursor: not-allowed;
  opacity: 0.45;
}

.toolbar-action.danger:hover:not(:disabled) {
  color: #f48771;
}

.advanced-toolbar {
  flex-shrink: 0;
  border-bottom: 1px solid var(--vscode-border-color);
  background: #1a1a1a;
  padding: 8px;
  display: grid;
  grid-template-columns: minmax(160px, 220px) 1fr auto;
  gap: 8px;
  align-items: end;
}

.profile-field {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.profile-field span {
  font-size: 11px;
  color: var(--vscode-text-muted);
}

.profile-field input {
  height: 28px;
  border: 1px solid var(--vscode-border-color);
  background: #3c3c3c;
  color: var(--vscode-text-primary);
  border-radius: 2px;
  padding: 0 8px;
  font-size: 12px;
  font-family: var(--vscode-font-mono);
}

.profile-field input:focus {
  outline: 1px solid var(--vscode-accent-color);
  border-color: var(--vscode-accent-color);
}

.profile-create {
  height: 28px;
  border: 1px solid var(--vscode-border-color);
  background: #0e639c;
  color: #ffffff;
  border-radius: 2px;
  padding: 0 10px;
  cursor: pointer;
  font-size: 12px;
}

.profile-create:hover {
  background: #1177bb;
}

.terminal-host {
  flex: 1;
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

@media (max-width: 980px) {
  .advanced-toolbar {
    grid-template-columns: 1fr;
  }
}
</style>
