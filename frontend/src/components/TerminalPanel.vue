<script setup lang="ts">
import { computed, defineAsyncComponent, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import type { TerminalInfo } from '../types'
import { useI18n } from '../composables/useI18n'

const TerminalViewport = defineAsyncComponent(() => import('./TerminalViewport.vue'))
const TERMINAL_PANEL_UI_STORAGE_KEY = 'toolhub.terminalPanelUi'

const props = defineProps<{
  visible: boolean
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

interface PersistedTerminalPanelUiState {
  shellInput: string
  cwdInput: string
  showToolbar: boolean
  splitEnabled: boolean
  secondaryTerminalId: string
  focusedTerminalId: string
}

function loadPersistedTerminalPanelUiState(): PersistedTerminalPanelUiState {
  if (typeof window === 'undefined') {
    return {
      shellInput: '',
      cwdInput: '',
      showToolbar: false,
      splitEnabled: false,
      secondaryTerminalId: '',
      focusedTerminalId: '',
    }
  }

  try {
    const raw = window.localStorage.getItem(TERMINAL_PANEL_UI_STORAGE_KEY)
    if (!raw) {
      throw new Error('missing terminal panel state')
    }

    const parsed = JSON.parse(raw) as Partial<PersistedTerminalPanelUiState>
    return {
      shellInput: typeof parsed.shellInput === 'string' ? parsed.shellInput : '',
      cwdInput: typeof parsed.cwdInput === 'string' ? parsed.cwdInput : '',
      showToolbar: parsed.showToolbar === true,
      splitEnabled: parsed.splitEnabled === true,
      secondaryTerminalId: typeof parsed.secondaryTerminalId === 'string' ? parsed.secondaryTerminalId : '',
      focusedTerminalId: typeof parsed.focusedTerminalId === 'string' ? parsed.focusedTerminalId : '',
    }
  } catch {
    return {
      shellInput: '',
      cwdInput: '',
      showToolbar: false,
      splitEnabled: false,
      secondaryTerminalId: '',
      focusedTerminalId: '',
    }
  }
}

const persistedUiState = loadPersistedTerminalPanelUiState()

const shellInput = ref(persistedUiState.shellInput)
const cwdInput = ref(persistedUiState.cwdInput)
const showToolbar = ref(persistedUiState.showToolbar)
const splitEnabled = ref(persistedUiState.splitEnabled)
const secondaryTerminalId = ref(persistedUiState.secondaryTerminalId)
const focusedTerminalId = ref(persistedUiState.focusedTerminalId)
const pendingSplitCreation = ref(false)
const { t, formatSessionCount } = useI18n()

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
      isCommandTarget: terminal.terminalId === commandTerminalId.value,
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

const terminalMap = computed(() => {
  const map = new Map<string, TerminalInfo>()
  props.terminals.forEach((terminal) => {
    map.set(terminal.terminalId, terminal)
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

const sessionSummary = computed(() => formatSessionCount(props.terminals.length))

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

const primaryTerminal = computed(() => {
  if (!primaryTerminalId.value) {
    return null
  }

  return terminalMap.value.get(primaryTerminalId.value) ?? null
})

const secondaryTerminal = computed(() => {
  if (!resolvedSecondaryTerminalId.value) {
    return null
  }

  return terminalMap.value.get(resolvedSecondaryTerminalId.value) ?? null
})

const splitCandidates = computed(() => {
  return terminalTabs.value.filter((terminal) => terminal.terminalId !== primaryTerminalId.value)
})

const splitStatusText = computed(() => {
  if (!splitEnabled.value) {
    return ''
  }

  if (pendingSplitCreation.value) {
    return t('terminal.creatingSplit')
  }

  if (resolvedSecondaryTerminalId.value) {
    return `${t('terminal.split')}: ${getLabel(resolvedSecondaryTerminalId.value)}`
  }

  return t('terminal.waitingSplit')
})

const commandTargetLabel = computed(() => {
  if (!commandTerminalId.value) {
    return t('terminal.noSessions')
  }

  return getLabel(commandTerminalId.value)
})

const commandTerminal = computed(() => {
  if (!commandTerminalId.value) {
    return null
  }

  return terminalMap.value.get(commandTerminalId.value) ?? null
})

const hasTerminalSessions = computed(() => terminalIdList.value.length > 0)

function getLabel(terminalId: string): string {
  return terminalLabelMap.value.get(terminalId) ?? terminalId
}

function getTerminalStatusLabel(terminal: TerminalInfo | null): string {
  if (!terminal) {
    return ''
  }

  return t(`terminal.status.${terminal.status}`)
}

function getCompactPath(path: string): string {
  const normalized = path.replace(/\\/g, '/')
  const segments = normalized.split('/').filter(Boolean)
  if (segments.length <= 2) {
    return normalized
  }

  return segments.slice(-2).join('/')
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

function persistTerminalPanelUiState(): void {
  if (typeof window === 'undefined') {
    return
  }

  try {
    window.localStorage.setItem(
      TERMINAL_PANEL_UI_STORAGE_KEY,
      JSON.stringify({
        shellInput: shellInput.value,
        cwdInput: cwdInput.value,
        showToolbar: showToolbar.value,
        splitEnabled: splitEnabled.value,
        secondaryTerminalId: secondaryTerminalId.value,
        focusedTerminalId: focusedTerminalId.value,
      } satisfies PersistedTerminalPanelUiState),
    )
  } catch {
    // ignore storage failures
  }
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

function useCurrentShell(): void {
  shellInput.value = commandTerminal.value?.shell ?? ''
}

function useCurrentCwd(): void {
  cwdInput.value = commandTerminal.value?.cwd ?? ''
}

function clearProfileInputs(): void {
  shellInput.value = ''
  cwdInput.value = ''
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

  if (focusedTerminalId.value === terminalId) {
    focusedTerminalId.value = findAlternativeTerminalId(terminalId)
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
    focusedTerminalId.value = selected
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

function changeSecondaryTerminal(event: Event): void {
  const target = event.target as HTMLSelectElement | null
  const nextTerminalId = target?.value ?? ''
  if (!nextTerminalId) {
    return
  }

  secondaryTerminalId.value = nextTerminalId
  focusedTerminalId.value = nextTerminalId
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
        focusedTerminalId.value = candidate
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

watch(
  () => [
    shellInput.value,
    cwdInput.value,
    showToolbar.value,
    splitEnabled.value,
    secondaryTerminalId.value,
    focusedTerminalId.value,
  ],
  () => {
    persistTerminalPanelUiState()
  },
  { deep: false },
)

watch(
  () => props.visible,
  (visible) => {
    if (!visible) {
      closeContextMenu()
    }
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
          :class="{
            active: terminal.terminalId === activeTerminalId,
            target: terminal.isCommandTarget,
          }"
          role="button"
          tabindex="0"
          @click="selectTab(terminal.terminalId)"
          @keydown.enter.prevent="selectTab(terminal.terminalId)"
          @keydown.space.prevent="selectTab(terminal.terminalId)"
          @contextmenu="openTabContextMenu($event, terminal.terminalId)"
        >
          <span class="terminal-state" :class="terminal.status" />
          <span class="terminal-label">{{ terminal.label }}</span>
          <span v-if="terminal.isCommandTarget" class="terminal-target-badge">IN</span>
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
        <span class="toolbar-summary">{{ sessionSummary }}</span>
        <span v-if="hasTerminalSessions" class="toolbar-target">IN: {{ commandTargetLabel }}</span>
        <button class="toolbar-action" type="button" @click="showToolbar = !showToolbar">
          {{ showToolbar ? t('terminal.hideProfile') : t('terminal.newProfile') }}
        </button>
        <button class="toolbar-action" type="button" :disabled="!primaryTerminalId" @click="toggleSplit">
          {{ splitEnabled ? t('terminal.unsplit') : t('terminal.split') }}
        </button>
        <button
          class="toolbar-action is-accent"
          type="button"
          :title="t('terminal.create')"
          :aria-label="t('terminal.create')"
          @click="createTerminalFromToolbar"
        >
          +
        </button>
        <button class="toolbar-action" type="button" :disabled="!commandTerminalId" @click="clearOutput">{{ t('terminal.clear') }}</button>
        <button class="toolbar-action danger" type="button" :disabled="!commandTerminalId" @click="stopActiveTerminal">{{ t('terminal.kill') }}</button>
        <button v-if="terminals.length > 1" class="toolbar-action danger" type="button" @click="stopAllTerminals">
          {{ t('terminal.killAll') }}
        </button>
      </div>
    </header>

    <div v-if="splitEnabled" class="split-toolbar">
      <span class="split-status">{{ splitStatusText }}</span>

      <div v-if="resolvedSecondaryTerminalId" class="split-targets">
        <button
          class="split-target-chip"
          :class="{ active: commandTerminalId === primaryTerminalId }"
          type="button"
          @click="focusTerminal(primaryTerminalId)"
        >
          {{ getLabel(primaryTerminalId) }}
        </button>
        <button
          class="split-target-chip"
          :class="{ active: commandTerminalId === resolvedSecondaryTerminalId }"
          type="button"
          @click="focusTerminal(resolvedSecondaryTerminalId)"
        >
          {{ getLabel(resolvedSecondaryTerminalId) }}
        </button>
      </div>

      <label v-if="splitCandidates.length > 0" class="split-selector">
        <span>{{ t('terminal.split') }}</span>
        <select :value="resolvedSecondaryTerminalId || secondaryTerminalId" @change="changeSecondaryTerminal">
          <option v-for="terminal in splitCandidates" :key="terminal.terminalId" :value="terminal.terminalId">
            {{ terminal.label }}
          </option>
        </select>
      </label>
    </div>

    <div v-show="showToolbar" class="advanced-toolbar">
      <label class="profile-field">
        <span>{{ t('terminal.shell') }}</span>
        <input v-model="shellInput" type="text" placeholder="powershell.exe" />
      </label>

      <label class="profile-field">
        <span>{{ t('terminal.workingDir') }}</span>
        <input v-model="cwdInput" type="text" placeholder="C:\\project" />
      </label>

      <button class="profile-create" type="button" @click="createTerminalFromToolbar">
        {{ t('terminal.create') }}
      </button>

      <div v-if="hasTerminalSessions" class="profile-shortcuts">
        <button class="profile-shortcut" type="button" @click="useCurrentShell">Shell <- current</button>
        <button class="profile-shortcut" type="button" @click="useCurrentCwd">CWD <- current</button>
        <button class="profile-shortcut" type="button" @click="clearProfileInputs">Clear</button>
      </div>
    </div>

    <div class="terminal-layout" :class="{ 'is-split': splitActive }">
      <template v-if="primaryTerminalId">
        <article class="terminal-pane" :class="{ focused: commandTerminalId === primaryTerminalId }">
          <header class="pane-header">
            <div class="pane-heading">
              <span class="pane-title">{{ getLabel(primaryTerminalId) }}</span>
              <span class="pane-path" :title="primaryTerminal?.cwd || ''">{{ primaryTerminal?.cwd ? getCompactPath(primaryTerminal.cwd) : '' }}</span>
            </div>
            <div class="pane-meta-group">
              <span class="pane-meta">{{ t('terminal.primary') }}</span>
              <span v-if="commandTerminalId === primaryTerminalId" class="pane-target-tag">INPUT</span>
              <span class="pane-status" :class="primaryTerminal?.status">{{ getTerminalStatusLabel(primaryTerminal) }}</span>
            </div>
          </header>
          <div class="pane-body">
            <TerminalViewport
              v-if="visible"
              :terminal-id="primaryTerminalId"
              :outputs="primaryOutputs"
              @focus="focusTerminal"
              @input="emit('input', $event)"
              @resize="emit('resizeTerminal', $event)"
            />
            <div v-else class="terminal-state-result">
              <div class="terminal-empty-card">
                <span class="terminal-empty-title">{{ t('terminal.panel') }}</span>
                <span class="terminal-empty-desc">{{ t('terminal.noSessions') }}</span>
              </div>
            </div>
          </div>
        </article>

        <article
          v-if="splitEnabled"
          class="terminal-pane"
          :class="{ focused: commandTerminalId === resolvedSecondaryTerminalId }"
        >
          <header class="pane-header">
            <div class="pane-heading">
              <span class="pane-title">
                {{ resolvedSecondaryTerminalId ? getLabel(resolvedSecondaryTerminalId) : t('terminal.creatingSplit') }}
              </span>
              <span class="pane-path" :title="secondaryTerminal?.cwd || ''">{{ secondaryTerminal?.cwd ? getCompactPath(secondaryTerminal.cwd) : '' }}</span>
            </div>
            <div class="pane-meta-group">
              <span v-if="commandTerminalId === resolvedSecondaryTerminalId" class="pane-target-tag">INPUT</span>
              <span class="pane-status" :class="secondaryTerminal?.status">{{ getTerminalStatusLabel(secondaryTerminal) }}</span>
              <button class="pane-close" type="button" @click="closeSplit">{{ t('terminal.closeSplit') }}</button>
            </div>
          </header>

          <div class="pane-body">
            <TerminalViewport
              v-if="visible && resolvedSecondaryTerminalId"
              :terminal-id="resolvedSecondaryTerminalId"
              :outputs="secondaryOutputs"
              @focus="focusTerminal"
              @input="emit('input', $event)"
              @resize="emit('resizeTerminal', $event)"
            />
            <div v-else class="terminal-state-result">
              <div class="terminal-empty-card">
                <span class="terminal-empty-title">{{ t('terminal.split') }}</span>
                <span class="terminal-empty-desc">
                  {{ resolvedSecondaryTerminalId ? t('terminal.noSessions') : t('terminal.waitingSplit') }}
                </span>
              </div>
            </div>
          </div>
        </article>
      </template>

      <div v-else class="terminal-placeholder">
        <div class="terminal-empty-card">
          <span class="terminal-empty-title">{{ t('terminal.panel') }}</span>
          <span class="terminal-empty-desc">{{ t('terminal.noSessions') }}</span>
          <div class="terminal-empty-actions">
            <button class="empty-action primary" type="button" @click="createTerminalFromToolbar">
              {{ t('terminal.create') }}
            </button>
            <button class="empty-action" type="button" @click="showToolbar = true">
              {{ t('terminal.newProfile') }}
            </button>
          </div>
        </div>
      </div>
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
  flex-shrink: 0;
  border-bottom: 1px solid var(--vscode-border-color);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 0 8px;
  min-height: 32px;
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

.terminal-instance.target {
  outline: 1px solid color-mix(in srgb, var(--vscode-accent-color) 55%, transparent);
  outline-offset: -1px;
}

.terminal-label {
  max-width: 170px;
  overflow: hidden;
  text-overflow: ellipsis;
}

.terminal-target-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 18px;
  height: 16px;
  padding: 0 4px;
  border-radius: 999px;
  background: var(--accent-soft);
  color: var(--vscode-accent-color);
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.06em;
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
  background: var(--vscode-hover-bg);
  opacity: 1;
}

.terminal-state {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--vscode-text-muted);
  flex-shrink: 0;
}

.terminal-state.running {
  background: var(--status-success);
}

.terminal-state.stopped,
.terminal-state.exited {
  background: var(--vscode-text-muted);
}

.terminal-state.failed {
  background: var(--status-danger);
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
  flex-wrap: wrap;
  justify-content: flex-end;
}

.toolbar-summary {
  color: var(--vscode-text-muted);
  font-size: 11px;
  white-space: nowrap;
}

.toolbar-target {
  display: inline-flex;
  align-items: center;
  height: 22px;
  padding: 0 8px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 999px;
  color: var(--vscode-text-primary);
  font-size: 11px;
  background: var(--surface-soft);
  white-space: nowrap;
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
  color: var(--status-danger);
}

.toolbar-action.is-accent {
  color: var(--vscode-text-primary);
  border-color: var(--vscode-border-color);
}

.split-toolbar {
  flex-shrink: 0;
  border-bottom: 1px solid var(--vscode-border-color);
  background: var(--surface-soft);
  padding: 6px 8px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.split-status {
  color: var(--vscode-text-muted);
  font-size: 11px;
}

.split-targets {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.split-target-chip {
  height: 24px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 999px;
  background: transparent;
  color: var(--vscode-text-muted);
  padding: 0 10px;
  font-size: 11px;
  cursor: pointer;
}

.split-target-chip.active {
  border-color: var(--vscode-accent-color);
  color: var(--vscode-text-primary);
  background: var(--accent-soft);
}

.split-selector {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: var(--vscode-text-muted);
  font-size: 11px;
}

.split-selector select {
  min-width: 180px;
  height: 28px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 4px;
  background: var(--vscode-editor-bg);
  color: var(--vscode-text-primary);
  padding: 0 8px;
}

.advanced-toolbar {
  flex-shrink: 0;
  border-bottom: 1px solid var(--vscode-border-color);
  background: var(--vscode-sidebar-bg);
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
  width: 100%;
  height: 32px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 4px;
  background: var(--vscode-editor-bg);
  color: var(--vscode-text-primary);
  padding: 0 10px;
  font-family: var(--vscode-font-mono);
}

.profile-create {
  align-self: end;
  height: 32px;
  border: 1px solid var(--vscode-accent-color);
  border-radius: 4px;
  background: var(--vscode-accent-color);
  color: #ffffff;
  padding: 0 14px;
  cursor: pointer;
}

.profile-create:hover {
  filter: brightness(1.08);
}

.profile-shortcuts {
  grid-column: 1 / -1;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.profile-shortcut {
  height: 28px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 999px;
  background: transparent;
  color: var(--vscode-text-muted);
  padding: 0 10px;
  font-size: 11px;
  cursor: pointer;
}

.profile-shortcut:hover {
  border-color: var(--vscode-accent-color);
  color: var(--vscode-text-primary);
  background: var(--vscode-hover-bg);
}

.terminal-layout {
  flex: 1;
  min-height: 0;
  display: grid;
  grid-template-columns: 1fr;
  background: var(--vscode-editor-bg);
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
  background: var(--vscode-editor-bg);
  border: 1px solid transparent;
}

.terminal-pane.focused {
  border-color: var(--vscode-accent-color);
}

.pane-header {
  min-height: 24px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 0 8px;
  border-bottom: 1px solid var(--vscode-border-color);
  background: var(--vscode-sidebar-bg);
}

.pane-heading {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.pane-title {
  color: var(--vscode-text-primary);
  font-size: 11px;
}

.pane-path {
  color: var(--vscode-text-muted);
  font-size: 11px;
  max-width: 220px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-family: var(--vscode-font-mono);
}

.pane-meta-group {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.pane-meta {
  color: var(--vscode-text-muted);
  font-size: 11px;
}

.pane-target-tag {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: 18px;
  padding: 0 6px;
  border-radius: 999px;
  background: var(--accent-soft);
  color: var(--vscode-accent-color);
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.08em;
}

.pane-status {
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--vscode-text-muted);
}

.pane-status.running {
  color: var(--status-success);
}

.pane-status.failed {
  color: var(--status-danger);
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

.terminal-state-result {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.terminal-empty-card {
  min-width: 220px;
  max-width: 320px;
  padding: 18px 20px;
  border: 1px dashed var(--vscode-border-color);
  border-radius: 8px;
  background: var(--surface-empty);
  display: flex;
  flex-direction: column;
  gap: 6px;
  text-align: center;
}

.terminal-empty-title {
  font-size: 12px;
  color: var(--vscode-text-primary);
  letter-spacing: 0.3px;
}

.terminal-empty-desc {
  font-size: 11px;
  color: var(--vscode-text-muted);
}

.terminal-empty-actions {
  margin-top: 8px;
  display: flex;
  justify-content: center;
  gap: 8px;
  flex-wrap: wrap;
}

.empty-action {
  height: 30px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 6px;
  background: transparent;
  color: var(--vscode-text-primary);
  padding: 0 12px;
  cursor: pointer;
}

.empty-action.primary {
  border-color: var(--vscode-accent-color);
  background: var(--vscode-accent-color);
  color: var(--text-on-accent);
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
  background: var(--vscode-sidebar-bg);
  box-shadow: var(--shadow-menu);
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
  background: var(--vscode-active-bg);
}

.menu-item.danger:hover {
  background: var(--status-danger);
  color: #ffffff;
}

@media (max-width: 980px) {
  .terminal-toolbar,
  .split-toolbar {
    align-items: flex-start;
    flex-direction: column;
  }

  .toolbar-actions {
    width: 100%;
    justify-content: flex-start;
  }

  .advanced-toolbar {
    grid-template-columns: 1fr;
  }

  .split-selector {
    width: 100%;
    justify-content: space-between;
  }

  .split-selector select {
    min-width: 0;
    flex: 1;
  }

  .terminal-layout.is-split {
    grid-template-columns: 1fr;
    grid-template-rows: 1fr 1fr;
  }
}
</style>
