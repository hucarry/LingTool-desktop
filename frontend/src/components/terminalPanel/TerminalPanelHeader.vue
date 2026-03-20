<script setup lang="ts">
import type { TerminalTab } from './types'

defineProps<{
  terminalsCount: number
  terminalTabs: TerminalTab[]
  activeTerminalId: string
  primaryTerminalId: string
  commandTerminalId: string
  commandTargetLabel: string
  hasTerminalSessions: boolean
  sessionSummary: string
  splitEnabled: boolean
  showToolbar: boolean
  t: (key: string) => string
}>()

const emit = defineEmits<{
  (e: 'select-tab', terminalId: string): void
  (e: 'open-tab-context-menu', event: MouseEvent, terminalId: string): void
  (e: 'stop-terminal', terminalId: string): void
  (e: 'toggle-toolbar'): void
  (e: 'toggle-split'): void
  (e: 'create-terminal'): void
  (e: 'clear-output'): void
  (e: 'stop-active-terminal'): void
  (e: 'stop-all-terminals'): void
}>()
</script>

<template>
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
        @click="emit('select-tab', terminal.terminalId)"
        @keydown.enter.prevent="emit('select-tab', terminal.terminalId)"
        @keydown.space.prevent="emit('select-tab', terminal.terminalId)"
        @contextmenu="emit('open-tab-context-menu', $event, terminal.terminalId)"
      >
        <span class="terminal-state" :class="terminal.status" />
        <span class="terminal-label">{{ terminal.label }}</span>
        <span v-if="terminal.isCommandTarget" class="terminal-target-badge">IN</span>
        <button
          class="terminal-close"
          type="button"
          :title="t('terminal.killTitle')"
          @click.stop="emit('stop-terminal', terminal.terminalId)"
        >
          &times;
        </button>
      </div>

      <span v-if="terminalTabs.length === 0" class="terminal-empty">{{ t('terminal.noSessions') }}</span>
    </div>

    <div class="toolbar-actions">
      <span class="toolbar-summary">{{ sessionSummary }}</span>
      <span v-if="hasTerminalSessions" class="toolbar-target">IN: {{ commandTargetLabel }}</span>
      <button class="toolbar-action" type="button" @click="emit('toggle-toolbar')">
        {{ showToolbar ? t('terminal.hideProfile') : t('terminal.newProfile') }}
      </button>
      <button class="toolbar-action" type="button" :disabled="!primaryTerminalId" @click="emit('toggle-split')">
        {{ splitEnabled ? t('terminal.unsplit') : t('terminal.split') }}
      </button>
      <button
        class="toolbar-action is-accent"
        type="button"
        :title="t('terminal.create')"
        :aria-label="t('terminal.create')"
        @click="emit('create-terminal')"
      >
        +
      </button>
      <button class="toolbar-action" type="button" :disabled="!commandTerminalId" @click="emit('clear-output')">{{ t('terminal.clear') }}</button>
      <button class="toolbar-action danger" type="button" :disabled="!commandTerminalId" @click="emit('stop-active-terminal')">{{ t('terminal.kill') }}</button>
      <button v-if="terminalsCount > 1" class="toolbar-action danger" type="button" @click="emit('stop-all-terminals')">
        {{ t('terminal.killAll') }}
      </button>
    </div>
  </header>
</template>

<style scoped>
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

@media (max-width: 980px) {
  .terminal-toolbar {
    align-items: flex-start;
    flex-direction: column;
  }

  .toolbar-actions {
    width: 100%;
    justify-content: flex-start;
  }
}
</style>
