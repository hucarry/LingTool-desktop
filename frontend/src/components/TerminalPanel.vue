<script setup lang="ts">
import { defineAsyncComponent } from 'vue'

import { useTerminalPanelState, type TerminalPanelProps } from '../composables/useTerminalPanelState'

const TerminalViewport = defineAsyncComponent(() => import('./TerminalViewport.vue'))

const props = defineProps<TerminalPanelProps>()

const emit = defineEmits<{
  (e: 'selectTerminal', terminalId: string): void
  (e: 'createTerminal', payload: { shell?: string; cwd?: string }): void
  (e: 'stopTerminal', terminalId: string): void
  (e: 'stopAllTerminals'): void
  (e: 'input', payload: { terminalId: string; data: string }): void
  (e: 'resizeTerminal', payload: { terminalId: string; cols: number; rows: number }): void
  (e: 'clearOutput', terminalId: string): void
}>()

const {
  commandTargetLabel,
  commandTerminalId,
  contextMenu,
  copyTerminalId,
  cwdInput,
  clearOutput,
  clearProfileInputs,
  closeSplit,
  createTerminalFromToolbar,
  focusTerminal,
  getCompactPath,
  getLabel,
  getTerminalStatusLabel,
  hasTerminalSessions,
  openTabContextMenu,
  primaryOutputs,
  primaryTerminal,
  primaryTerminalId,
  secondaryOutputs,
  secondaryTerminal,
  secondaryTerminalId,
  resolvedSecondaryTerminalId,
  selectTab,
  sessionSummary,
  shellInput,
  showToolbar,
  splitActive,
  splitCandidates,
  splitEnabled,
  splitStatusText,
  splitWithTerminal,
  stopActiveTerminal,
  stopAllTerminals,
  stopOtherTerminals,
  stopTerminalById,
  t,
  terminalTabs,
  toggleSplit,
  useCurrentCwd,
  useCurrentShell,
  changeSecondaryTerminal,
} = useTerminalPanelState({
  props,
  actions: {
    selectTerminal: (terminalId) => emit('selectTerminal', terminalId),
    createTerminal: (payload) => emit('createTerminal', payload),
    stopTerminal: (terminalId) => emit('stopTerminal', terminalId),
    stopAllTerminals: () => emit('stopAllTerminals'),
    input: (payload) => emit('input', payload),
  },
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
  background: var(--surface-panel);
}

.terminal-toolbar {
  flex-shrink: 0;
  border-bottom: 1px solid var(--border-default);
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
  color: var(--text-muted);
  letter-spacing: 0.8px;
  white-space: nowrap;
}

.panel-tab.is-active {
  color: var(--text-primary);
}

.terminal-instance {
  border: 0;
  background: transparent;
  color: var(--text-muted);
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
  background: var(--surface-hover);
  color: var(--text-primary);
}

.terminal-instance.active {
  background: var(--surface-active);
  color: var(--text-primary);
}

.terminal-instance.target {
  outline: 1px solid color-mix(in srgb, var(--accent-color) 55%, transparent);
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
  color: var(--accent-color);
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
  background: var(--surface-hover);
  opacity: 1;
}

.terminal-state {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--text-muted);
  flex-shrink: 0;
}

.terminal-state.running {
  background: var(--status-success);
}

.terminal-state.stopped,
.terminal-state.exited {
  background: var(--text-muted);
}

.terminal-state.failed {
  background: var(--status-danger);
}

.terminal-empty {
  color: var(--text-muted);
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
  color: var(--text-muted);
  font-size: 11px;
  white-space: nowrap;
}

.toolbar-target {
  display: inline-flex;
  align-items: center;
  height: 22px;
  padding: 0 8px;
  border: 1px solid var(--border-default);
  border-radius: 999px;
  color: var(--text-primary);
  font-size: 11px;
  background: var(--surface-soft);
  white-space: nowrap;
}

.toolbar-action {
  border: 1px solid transparent;
  background: transparent;
  color: var(--text-muted);
  height: 22px;
  min-width: 22px;
  padding: 0 8px;
  border-radius: 3px;
  cursor: pointer;
  font-size: 11px;
}

.toolbar-action:hover:not(:disabled) {
  background: var(--surface-hover);
  color: var(--text-primary);
}

.toolbar-action:disabled {
  cursor: not-allowed;
  opacity: 0.45;
}

.toolbar-action.danger:hover:not(:disabled) {
  color: var(--status-danger);
}

.toolbar-action.is-accent {
  color: var(--text-primary);
  border-color: var(--border-default);
}

.split-toolbar {
  flex-shrink: 0;
  border-bottom: 1px solid var(--border-default);
  background: var(--surface-soft);
  padding: 6px 8px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.split-status {
  color: var(--text-muted);
  font-size: 11px;
}

.split-targets {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.split-target-chip {
  height: 24px;
  border: 1px solid var(--border-default);
  border-radius: 999px;
  background: transparent;
  color: var(--text-muted);
  padding: 0 10px;
  font-size: 11px;
  cursor: pointer;
}

.split-target-chip.active {
  border-color: var(--accent-color);
  color: var(--text-primary);
  background: var(--accent-soft);
}

.split-selector {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: var(--text-muted);
  font-size: 11px;
}

.split-selector select {
  min-width: 180px;
  height: 28px;
  border: 1px solid var(--border-default);
  border-radius: 4px;
  background: var(--surface-editor);
  color: var(--text-primary);
  padding: 0 8px;
}

.advanced-toolbar {
  flex-shrink: 0;
  border-bottom: 1px solid var(--border-default);
  background: var(--surface-sidebar);
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
  color: var(--text-muted);
}

.profile-field input {
  width: 100%;
  height: 32px;
  border: 1px solid var(--border-default);
  border-radius: 4px;
  background: var(--surface-editor);
  color: var(--text-primary);
  padding: 0 10px;
  font-family: var(--font-mono);
}

.profile-create {
  align-self: end;
  height: 32px;
  border: 1px solid var(--accent-color);
  border-radius: 4px;
  background: var(--accent-color);
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
  border: 1px solid var(--border-default);
  border-radius: 999px;
  background: transparent;
  color: var(--text-muted);
  padding: 0 10px;
  font-size: 11px;
  cursor: pointer;
}

.profile-shortcut:hover {
  border-color: var(--accent-color);
  color: var(--text-primary);
  background: var(--surface-hover);
}

.terminal-layout {
  flex: 1;
  min-height: 0;
  display: grid;
  grid-template-columns: 1fr;
  background: var(--surface-editor);
}

.terminal-layout.is-split {
  grid-template-columns: 1fr 1fr;
  gap: 1px;
  background: var(--border-default);
}

.terminal-pane {
  min-height: 0;
  display: flex;
  flex-direction: column;
  background: var(--surface-editor);
  border: 1px solid transparent;
}

.terminal-pane.focused {
  border-color: var(--accent-color);
}

.pane-header {
  min-height: 24px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 0 8px;
  border-bottom: 1px solid var(--border-default);
  background: var(--surface-sidebar);
}

.pane-heading {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.pane-title {
  color: var(--text-primary);
  font-size: 11px;
}

.pane-path {
  color: var(--text-muted);
  font-size: 11px;
  max-width: 220px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-family: var(--font-mono);
}

.pane-meta-group {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.pane-meta {
  color: var(--text-muted);
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
  color: var(--accent-color);
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.08em;
}

.pane-status {
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--text-muted);
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
  color: var(--text-muted);
  font-size: 11px;
  cursor: pointer;
}

.pane-close:hover {
  color: var(--text-primary);
}

.pane-body {
  flex: 1;
  min-height: 0;
  overflow: hidden;
  background: var(--terminal-bg);
}

.split-placeholder,
.terminal-placeholder {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--text-muted);
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
  border: 1px dashed var(--border-default);
  border-radius: 8px;
  background: var(--surface-empty);
  display: flex;
  flex-direction: column;
  gap: 6px;
  text-align: center;
}

.terminal-empty-title {
  font-size: 12px;
  color: var(--text-primary);
  letter-spacing: 0.3px;
}

.terminal-empty-desc {
  font-size: 11px;
  color: var(--text-muted);
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
  border: 1px solid var(--border-default);
  border-radius: 6px;
  background: transparent;
  color: var(--text-primary);
  padding: 0 12px;
  cursor: pointer;
}

.empty-action.primary {
  border-color: var(--accent-color);
  background: var(--accent-color);
  color: var(--text-on-accent);
}

.terminal-context-menu {
  position: fixed;
  z-index: 4000;
  min-width: 170px;
  display: flex;
  flex-direction: column;
  border: 1px solid var(--border-default);
  border-radius: 4px;
  overflow: hidden;
  background: var(--surface-sidebar);
  box-shadow: var(--shadow-menu);
}

.menu-item {
  border: 0;
  background: transparent;
  color: var(--text-primary);
  text-align: left;
  font-size: 12px;
  padding: 8px 10px;
  cursor: pointer;
}

.menu-item:hover {
  background: var(--surface-active);
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
