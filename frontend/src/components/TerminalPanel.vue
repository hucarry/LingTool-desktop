<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import TerminalViewport from './TerminalViewport.vue'
import type { TerminalInfo } from '../types'
import { useI18n } from '../composables/useI18n'

const props = defineProps<{
  terminals: TerminalInfo[]
  activeTerminalId: string
  outputsByTerminal: Record<string, string[]>
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

interface ContextMenuState {
  visible: boolean
  x: number
  y: number
  terminalId: string
}

const shellInput = ref('')
const cwdInput = ref('')
const showToolbar = ref(false)
const splitEnabled = ref(false)
const secondaryTerminalId = ref('')
const focusedTerminalId = ref('')
const pendingSplitCreation = ref(false)
const { t } = useI18n()

const contextMenu = reactive<ContextMenuState>({
  visible: false,
  x: 0,
  y: 0,
  terminalId: '',
})

const terminalTabs = computed(() => {
  return props.terminals.map((terminal, index) => {
    const shellName = terminal.shell.split(/[\\/]/).pop() || terminal.shell || t('terminal.shellFallback')
    const order = props.terminals.length - index
    return {
      ...terminal,
      label: `${shellName} ${order}`,
    }
  })
})

const terminalIdList = computed(() => props.terminals.map((terminal) => terminal.terminalId))

const terminalLabelMap = computed(() => {
  const map = new Map<string, string>()
  terminalTabs.value.forEach((terminal) => {
    map.set(terminal.terminalId, terminal.label)
  })
  return map
})

const primaryTerminalId = computed(() => {
  if (props.activeTerminalId && terminalIdList.value.includes(props.activeTerminalId)) {
    return props.activeTerminalId
  }

  return terminalIdList.value[0] ?? ''
})

const resolvedSecondaryTerminalId = computed(() => {
  if (!splitEnabled.value || !secondaryTerminalId.value) {
    return ''
  }

  if (!terminalIdList.value.includes(secondaryTerminalId.value)) {
    return ''
  }

  if (secondaryTerminalId.value === primaryTerminalId.value) {
    return ''
  }

  return secondaryTerminalId.value
})

const splitActive = computed(() => splitEnabled.value && !!resolvedSecondaryTerminalId.value)

const commandTerminalId = computed(() => {
  if (focusedTerminalId.value && terminalIdList.value.includes(focusedTerminalId.value)) {
    return focusedTerminalId.value
  }

  return primaryTerminalId.value
})

const primaryOutputs = computed(() => {
  if (!primaryTerminalId.value) {
    return []
  }

  return props.outputsByTerminal[primaryTerminalId.value] ?? []
})

const secondaryOutputs = computed(() => {
  if (!resolvedSecondaryTerminalId.value) {
    return []
  }

  return props.outputsByTerminal[resolvedSecondaryTerminalId.value] ?? []
})

function getLabel(terminalId: string): string {
  return terminalLabelMap.value.get(terminalId) ?? terminalId
}

function findAlternativeTerminalId(primaryId: string, preferredId = ''): string {
  if (preferredId && preferredId !== primaryId && terminalIdList.value.includes(preferredId)) {
    return preferredId
  }

  const fallback = props.terminals.find((terminal) => terminal.terminalId !== primaryId)
  return fallback?.terminalId ?? ''
}

function closeContextMenu(): void {
  contextMenu.visible = false
}

function onGlobalPointerDown(event: MouseEvent): void {
  if (!contextMenu.visible) {
    return
  }

  const target = event.target as HTMLElement | null
  if (target?.closest('.terminal-context-menu')) {
    return
  }

  closeContextMenu()
}

function onGlobalKeyDown(event: KeyboardEvent): void {
  if (event.key === 'Escape' && contextMenu.visible) {
    closeContextMenu()
  }
}

function createTerminal(payload: { shell?: string; cwd?: string } = {}): void {
  emit('createTerminal', payload)
}

function createTerminalFromToolbar(): void {
  createTerminal({
    shell: shellInput.value.trim() || undefined,
    cwd: cwdInput.value.trim() || undefined,
  })
}

function focusTerminal(terminalId: string): void {
  focusedTerminalId.value = terminalId
  emit('selectTerminal', terminalId)
}

function selectTab(terminalId: string): void {
  focusTerminal(terminalId)
  closeContextMenu()
}

function stopTerminalById(terminalId: string): void {
  if (!terminalId) {
    return
  }

  emit('stopTerminal', terminalId)

  if (secondaryTerminalId.value === terminalId) {
    secondaryTerminalId.value = ''
  }

  closeContextMenu()
}

function stopActiveTerminal(): void {
  if (!commandTerminalId.value) {
    return
  }

  stopTerminalById(commandTerminalId.value)
}

function stopOtherTerminals(keepTerminalId: string): void {
  props.terminals
    .filter((terminal) => terminal.terminalId !== keepTerminalId)
    .forEach((terminal) => emit('stopTerminal', terminal.terminalId))

  if (secondaryTerminalId.value && secondaryTerminalId.value !== keepTerminalId) {
    secondaryTerminalId.value = ''
  }

  emit('selectTerminal', keepTerminalId)
  focusedTerminalId.value = keepTerminalId
  closeContextMenu()
}

function stopAllTerminals(): void {
  emit('stopAllTerminals')
  splitEnabled.value = false
  secondaryTerminalId.value = ''
  focusedTerminalId.value = ''
  pendingSplitCreation.value = false
  closeContextMenu()
}

function clearOutput(): void {
  if (!commandTerminalId.value) {
    return
  }

  emit('clearOutput', commandTerminalId.value)
}

function openTabContextMenu(event: MouseEvent, terminalId: string): void {
  event.preventDefault()
  event.stopPropagation()

  contextMenu.visible = true
  contextMenu.x = event.clientX
  contextMenu.y = event.clientY
  contextMenu.terminalId = terminalId

  focusTerminal(terminalId)
}

function splitWithTerminal(targetTerminalId: string): void {
  if (!primaryTerminalId.value) {
    return
  }

  splitEnabled.value = true

  const selected = findAlternativeTerminalId(primaryTerminalId.value, targetTerminalId)
  if (selected) {
    secondaryTerminalId.value = selected
    pendingSplitCreation.value = false
    closeContextMenu()
    return
  }

  secondaryTerminalId.value = ''

  if (!pendingSplitCreation.value) {
    pendingSplitCreation.value = true
    createTerminal()
  }

  closeContextMenu()
}

function toggleSplit(): void {
  if (splitEnabled.value) {
    splitEnabled.value = false
    secondaryTerminalId.value = ''
    pendingSplitCreation.value = false
    return
  }

  splitWithTerminal('')
}

function closeSplit(): void {
  splitEnabled.value = false
  secondaryTerminalId.value = ''
  pendingSplitCreation.value = false
}

function copyTerminalId(terminalId: string): void {
  if (!terminalId || typeof navigator === 'undefined' || !navigator.clipboard) {
    closeContextMenu()
    return
  }

  navigator.clipboard.writeText(terminalId).finally(() => {
    closeContextMenu()
  })
}

watch(
  () => [primaryTerminalId.value, terminalIdList.value.join('|')],
  () => {
    if (!focusedTerminalId.value || !terminalIdList.value.includes(focusedTerminalId.value)) {
      focusedTerminalId.value = primaryTerminalId.value
    }

    if (!splitEnabled.value) {
      if (contextMenu.visible && contextMenu.terminalId && !terminalIdList.value.includes(contextMenu.terminalId)) {
        closeContextMenu()
      }
      return
    }

    if (pendingSplitCreation.value) {
      const candidate = findAlternativeTerminalId(primaryTerminalId.value, secondaryTerminalId.value)
      if (candidate) {
        secondaryTerminalId.value = candidate
        pendingSplitCreation.value = false
      }
      return
    }

    if (!resolvedSecondaryTerminalId.value) {
      const candidate = findAlternativeTerminalId(primaryTerminalId.value)
      if (candidate) {
        secondaryTerminalId.value = candidate
      } else {
        closeSplit()
      }
    }
  },
  { immediate: true },
)

watch(
  () => contextMenu.terminalId,
  () => {
    if (!contextMenu.terminalId) {
      return
    }

    if (!terminalIdList.value.includes(contextMenu.terminalId)) {
      closeContextMenu()
    }
  },
)

watch(
  () => showToolbar.value,
  () => {
    closeContextMenu()
  },
)

onMounted(() => {
  window.addEventListener('mousedown', onGlobalPointerDown)
  window.addEventListener('keydown', onGlobalKeyDown)
  window.addEventListener('blur', closeContextMenu)
  window.addEventListener('resize', closeContextMenu)
})

onBeforeUnmount(() => {
  window.removeEventListener('mousedown', onGlobalPointerDown)
  window.removeEventListener('keydown', onGlobalKeyDown)
  window.removeEventListener('blur', closeContextMenu)
  window.removeEventListener('resize', closeContextMenu)
})
</script>

<template>
  <section class="terminal-panel">
    <header class="terminal-toolbar">
      <div class="terminal-tab-strip">
        <span class="panel-tab is-active">{{ t('terminal.panel') }}</span>

        <div
          v-for="terminal in terminalTabs"
          :key="terminal.terminalId"
          class="terminal-instance"
          :class="{ active: terminal.terminalId === activeTerminalId }"
          role="button"
          tabindex="0"
          @click="selectTab(terminal.terminalId)"
          @contextmenu="openTabContextMenu($event, terminal.terminalId)"
        >
          <span class="terminal-state" :class="terminal.status" />
          <span class="terminal-label">{{ terminal.label }}</span>
          <button
            class="terminal-close"
            type="button"
            :title="t('terminal.killTitle')"
            @click.stop="stopTerminalById(terminal.terminalId)"
          >
            &times;
          </button>
        </div>

        <span v-if="terminalTabs.length === 0" class="terminal-empty">{{ t('terminal.noSessions') }}</span>
      </div>

      <div class="toolbar-actions">
        <button class="toolbar-action" type="button" @click="showToolbar = !showToolbar">
          {{ showToolbar ? t('terminal.hideProfile') : t('terminal.newProfile') }}
        </button>
        <button class="toolbar-action" type="button" :disabled="!primaryTerminalId" @click="toggleSplit">
          {{ splitEnabled ? t('terminal.unsplit') : t('terminal.split') }}
        </button>
        <button class="toolbar-action" type="button" @click="createTerminalFromToolbar">+</button>
        <button class="toolbar-action" type="button" :disabled="!commandTerminalId" @click="clearOutput">{{ t('terminal.clear') }}</button>
        <button class="toolbar-action danger" type="button" :disabled="!commandTerminalId" @click="stopActiveTerminal">{{ t('terminal.kill') }}</button>
        <button v-if="terminals.length > 1" class="toolbar-action danger" type="button" @click="stopAllTerminals">
          {{ t('terminal.killAll') }}
        </button>
      </div>
    </header>

    <div v-show="showToolbar" class="advanced-toolbar">
      <label class="profile-field">
        <span>{{ t('terminal.shell') }}</span>
        <input v-model="shellInput" type="text" placeholder="powershell.exe" />
      </label>

      <label class="profile-field">
        <span>{{ t('terminal.workingDir') }}</span>
        <input v-model="cwdInput" type="text" placeholder="C:\\project" />
      </label>

      <button class="profile-create" type="button" @click="createTerminalFromToolbar">{{ t('terminal.create') }}</button>
    </div>

    <div class="terminal-layout" :class="{ 'is-split': splitActive }">
      <template v-if="primaryTerminalId">
        <article class="terminal-pane" :class="{ focused: commandTerminalId === primaryTerminalId }">
          <header class="pane-header">
            <span class="pane-title">{{ getLabel(primaryTerminalId) }}</span>
            <span class="pane-meta">{{ t('terminal.primary') }}</span>
          </header>
          <div class="pane-body">
            <TerminalViewport
              :terminal-id="primaryTerminalId"
              :outputs="primaryOutputs"
              @focus="focusTerminal"
              @input="emit('input', $event)"
              @resize="emit('resizeTerminal', $event)"
            />
          </div>
        </article>

        <article
          v-if="splitEnabled"
          class="terminal-pane"
          :class="{ focused: commandTerminalId === resolvedSecondaryTerminalId }"
        >
          <header class="pane-header">
            <span class="pane-title">
              {{ resolvedSecondaryTerminalId ? getLabel(resolvedSecondaryTerminalId) : t('terminal.creatingSplit') }}
            </span>
            <button class="pane-close" type="button" @click="closeSplit">{{ t('terminal.closeSplit') }}</button>
          </header>

          <div class="pane-body">
            <TerminalViewport
              v-if="resolvedSecondaryTerminalId"
              :terminal-id="resolvedSecondaryTerminalId"
              :outputs="secondaryOutputs"
              @focus="focusTerminal"
              @input="emit('input', $event)"
              @resize="emit('resizeTerminal', $event)"
            />
            <div v-else class="split-placeholder">{{ t('terminal.waitingSplit') }}</div>
          </div>
        </article>
      </template>

      <div v-else class="terminal-placeholder">{{ t('terminal.noSessions') }}</div>
    </div>

    <teleport to="body">
      <div
        v-if="contextMenu.visible"
        class="terminal-context-menu"
        :style="{ left: `${contextMenu.x}px`, top: `${contextMenu.y}px` }"
        @mousedown.stop
      >
        <button class="menu-item" type="button" @click="splitWithTerminal(contextMenu.terminalId)">{{ t('terminal.split') }}</button>
        <button class="menu-item" type="button" @click="clearOutput">{{ t('terminal.clearActive') }}</button>
        <button class="menu-item danger" type="button" @click="stopTerminalById(contextMenu.terminalId)">{{ t('terminal.kill') }}</button>
        <button class="menu-item" type="button" @click="stopOtherTerminals(contextMenu.terminalId)">{{ t('terminal.killOthers') }}</button>
        <button class="menu-item" type="button" @click="copyTerminalId(contextMenu.terminalId)">{{ t('terminal.copyId') }}</button>
      </div>
    </teleport>
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

.terminal-label {
  max-width: 170px;
  overflow: hidden;
  text-overflow: ellipsis;
}

.terminal-close {
  opacity: 0;
  border: 0;
  background: transparent;
  color: currentColor;
  width: 16px;
  height: 16px;
  border-radius: 2px;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
}

.terminal-instance:hover .terminal-close,
.terminal-instance.active .terminal-close {
  opacity: 0.9;
}

.terminal-close:hover {
  background: #5a5d5e;
  opacity: 1;
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

.terminal-layout {
  flex: 1;
  min-height: 0;
  display: grid;
  grid-template-columns: 1fr;
  background: #1e1e1e;
}

.terminal-layout.is-split {
  grid-template-columns: 1fr 1fr;
  gap: 1px;
  background: var(--vscode-border-color);
}

.terminal-pane {
  min-height: 0;
  display: flex;
  flex-direction: column;
  background: #1e1e1e;
  border: 1px solid transparent;
}

.terminal-pane.focused {
  border-color: #0e639c;
}

.pane-header {
  height: 24px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 8px;
  border-bottom: 1px solid var(--vscode-border-color);
  background: #181818;
}

.pane-title {
  color: var(--vscode-text-primary);
  font-size: 11px;
}

.pane-meta {
  color: var(--vscode-text-muted);
  font-size: 11px;
}

.pane-close {
  border: 0;
  background: transparent;
  color: var(--vscode-text-muted);
  font-size: 11px;
  cursor: pointer;
}

.pane-close:hover {
  color: var(--vscode-text-primary);
}

.pane-body {
  flex: 1;
  min-height: 0;
}

.split-placeholder,
.terminal-placeholder {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--vscode-text-muted);
  font-size: 12px;
  letter-spacing: 0.3px;
}

.terminal-context-menu {
  position: fixed;
  z-index: 4000;
  min-width: 170px;
  display: flex;
  flex-direction: column;
  border: 1px solid var(--vscode-border-color);
  border-radius: 4px;
  overflow: hidden;
  background: #252526;
  box-shadow: 0 6px 20px rgba(0, 0, 0, 0.36);
}

.menu-item {
  border: 0;
  background: transparent;
  color: var(--vscode-text-primary);
  text-align: left;
  font-size: 12px;
  padding: 8px 10px;
  cursor: pointer;
}

.menu-item:hover {
  background: #094771;
}

.menu-item.danger:hover {
  background: #5f2120;
}

@media (max-width: 980px) {
  .advanced-toolbar {
    grid-template-columns: 1fr;
  }

  .terminal-layout.is-split {
    grid-template-columns: 1fr;
    grid-template-rows: 1fr 1fr;
  }
}
</style>
