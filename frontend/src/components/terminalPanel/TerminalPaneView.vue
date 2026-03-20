<script setup lang="ts">
import { defineAsyncComponent } from 'vue'

const TerminalViewport = defineAsyncComponent(() => import('../TerminalViewport.vue'))

defineProps<{
  visible: boolean
  terminalId: string
  outputs: string[]
  terminal: {
    cwd?: string
    status?: string
  } | null
  title: string
  pathTitle?: string
  pathText: string
  statusText: string
  focused: boolean
  target: boolean
  roleLabel?: string
  closeLabel?: string
  emptyTitle: string
  emptyDesc: string
}>()

const emit = defineEmits<{
  (e: 'focus-terminal', terminalId: string): void
  (e: 'input', payload: { terminalId: string; data: string }): void
  (e: 'resize', payload: { terminalId: string; cols: number; rows: number }): void
  (e: 'close-pane'): void
}>()
</script>

<template>
  <article class="terminal-pane" :class="{ focused }">
    <header class="pane-header">
      <div class="pane-heading">
        <span class="pane-title">{{ title }}</span>
        <span class="pane-path" :title="pathTitle || ''">{{ pathText }}</span>
      </div>
      <div class="pane-meta-group">
        <span v-if="roleLabel" class="pane-meta">{{ roleLabel }}</span>
        <span v-if="target" class="pane-target-tag">INPUT</span>
        <span class="pane-status" :class="terminal?.status">{{ statusText }}</span>
        <button v-if="closeLabel" class="pane-close" type="button" @click="emit('close-pane')">{{ closeLabel }}</button>
      </div>
    </header>

    <div class="pane-body">
      <TerminalViewport
        v-if="visible && terminalId"
        :terminal-id="terminalId"
        :outputs="outputs"
        @focus="emit('focus-terminal', $event)"
        @input="emit('input', $event)"
        @resize="emit('resize', $event)"
      />
      <div v-else class="terminal-state-result">
        <div class="terminal-empty-card">
          <span class="terminal-empty-title">{{ emptyTitle }}</span>
          <span class="terminal-empty-desc">{{ emptyDesc }}</span>
        </div>
      </div>
    </div>
  </article>
</template>

<style scoped>
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
</style>
