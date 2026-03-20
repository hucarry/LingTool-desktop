<script setup lang="ts">
import { computed } from 'vue'

import { useTerminalPanelState, type TerminalPanelProps } from '../composables/useTerminalPanelState'
import TerminalContextMenu from './terminalPanel/TerminalContextMenu.vue'
import TerminalPaneView from './terminalPanel/TerminalPaneView.vue'
import TerminalPanelHeader from './terminalPanel/TerminalPanelHeader.vue'
import TerminalProfileToolbar from './terminalPanel/TerminalProfileToolbar.vue'
import TerminalSplitToolbar from './terminalPanel/TerminalSplitToolbar.vue'

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

const primaryPanePathText = computed(() => {
  return primaryTerminal.value?.cwd ? getCompactPath(primaryTerminal.value.cwd) : ''
})

const secondaryPaneTitle = computed(() => {
  return resolvedSecondaryTerminalId.value
    ? getLabel(resolvedSecondaryTerminalId.value)
    : t('terminal.creatingSplit')
})

const secondaryPanePathText = computed(() => {
  return secondaryTerminal.value?.cwd ? getCompactPath(secondaryTerminal.value.cwd) : ''
})

const secondaryPaneEmptyDesc = computed(() => {
  return resolvedSecondaryTerminalId.value ? t('terminal.noSessions') : t('terminal.waitingSplit')
})
</script>

<template>
  <section class="terminal-panel">
    <TerminalPanelHeader
      :terminals-count="terminals.length"
      :terminal-tabs="terminalTabs"
      :active-terminal-id="activeTerminalId"
      :primary-terminal-id="primaryTerminalId"
      :command-terminal-id="commandTerminalId"
      :command-target-label="commandTargetLabel"
      :has-terminal-sessions="hasTerminalSessions"
      :session-summary="sessionSummary"
      :split-enabled="splitEnabled"
      :show-toolbar="showToolbar"
      :t="t"
      @select-tab="selectTab"
      @open-tab-context-menu="openTabContextMenu"
      @stop-terminal="stopTerminalById"
      @toggle-toolbar="showToolbar = !showToolbar"
      @toggle-split="toggleSplit"
      @create-terminal="createTerminalFromToolbar"
      @clear-output="clearOutput"
      @stop-active-terminal="stopActiveTerminal"
      @stop-all-terminals="stopAllTerminals"
    />

    <TerminalSplitToolbar
      :split-enabled="splitEnabled"
      :split-status-text="splitStatusText"
      :command-terminal-id="commandTerminalId"
      :primary-terminal-id="primaryTerminalId"
      :resolved-secondary-terminal-id="resolvedSecondaryTerminalId"
      :secondary-terminal-id="secondaryTerminalId"
      :split-candidates="splitCandidates"
      :get-label="getLabel"
      :t="t"
      @focus-terminal="focusTerminal"
      @change-secondary-terminal="changeSecondaryTerminal"
    />

    <TerminalProfileToolbar
      v-model:shell-input="shellInput"
      v-model:cwd-input="cwdInput"
      :visible="showToolbar"
      :has-terminal-sessions="hasTerminalSessions"
      :t="t"
      @create-terminal="createTerminalFromToolbar"
      @use-current-shell="useCurrentShell"
      @use-current-cwd="useCurrentCwd"
      @clear-profile-inputs="clearProfileInputs"
    />

    <div class="terminal-layout" :class="{ 'is-split': splitActive }">
      <template v-if="primaryTerminalId">
        <TerminalPaneView
          :visible="visible"
          :terminal-id="primaryTerminalId"
          :outputs="primaryOutputs"
          :terminal="primaryTerminal"
          :title="getLabel(primaryTerminalId)"
          :path-title="primaryTerminal?.cwd || ''"
          :path-text="primaryPanePathText"
          :status-text="getTerminalStatusLabel(primaryTerminal)"
          :focused="commandTerminalId === primaryTerminalId"
          :target="commandTerminalId === primaryTerminalId"
          :role-label="t('terminal.primary')"
          :empty-title="t('terminal.panel')"
          :empty-desc="t('terminal.noSessions')"
          @focus-terminal="focusTerminal"
          @input="emit('input', $event)"
          @resize="emit('resizeTerminal', $event)"
        />

        <TerminalPaneView
          v-if="splitEnabled"
          :visible="visible && !!resolvedSecondaryTerminalId"
          :terminal-id="resolvedSecondaryTerminalId"
          :outputs="secondaryOutputs"
          :terminal="secondaryTerminal"
          :title="secondaryPaneTitle"
          :path-title="secondaryTerminal?.cwd || ''"
          :path-text="secondaryPanePathText"
          :status-text="getTerminalStatusLabel(secondaryTerminal)"
          :focused="commandTerminalId === resolvedSecondaryTerminalId"
          :target="commandTerminalId === resolvedSecondaryTerminalId"
          :close-label="t('terminal.closeSplit')"
          :empty-title="t('terminal.split')"
          :empty-desc="secondaryPaneEmptyDesc"
          @focus-terminal="focusTerminal"
          @input="emit('input', $event)"
          @resize="emit('resizeTerminal', $event)"
          @close-pane="closeSplit"
        />
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

    <TerminalContextMenu
      :visible="contextMenu.visible"
      :x="contextMenu.x"
      :y="contextMenu.y"
      :terminal-id="contextMenu.terminalId"
      :t="t"
      @split-with-terminal="splitWithTerminal"
      @clear-output="clearOutput"
      @stop-terminal="stopTerminalById"
      @stop-other-terminals="stopOtherTerminals"
      @copy-terminal-id="copyTerminalId"
    />
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

.terminal-placeholder {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--text-muted);
  font-size: 12px;
  letter-spacing: 0.3px;
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

@media (max-width: 980px) {
  .terminal-layout.is-split {
    grid-template-columns: 1fr;
    grid-template-rows: 1fr 1fr;
  }
}
</style>
