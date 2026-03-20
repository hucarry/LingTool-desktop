<script setup lang="ts">
import type { TerminalTab } from './types'

defineProps<{
  splitEnabled: boolean
  splitStatusText: string
  commandTerminalId: string
  primaryTerminalId: string
  resolvedSecondaryTerminalId: string
  secondaryTerminalId: string
  splitCandidates: TerminalTab[]
  getLabel: (terminalId: string) => string
  t: (key: string) => string
}>()

const emit = defineEmits<{
  (e: 'focus-terminal', terminalId: string): void
  (e: 'change-secondary-terminal', event: Event): void
}>()
</script>

<template>
  <div v-if="splitEnabled" class="split-toolbar">
    <span class="split-status">{{ splitStatusText }}</span>

    <div v-if="resolvedSecondaryTerminalId" class="split-targets">
      <button
        class="split-target-chip"
        :class="{ active: commandTerminalId === primaryTerminalId }"
        type="button"
        @click="emit('focus-terminal', primaryTerminalId)"
      >
        {{ getLabel(primaryTerminalId) }}
      </button>
      <button
        class="split-target-chip"
        :class="{ active: commandTerminalId === resolvedSecondaryTerminalId }"
        type="button"
        @click="emit('focus-terminal', resolvedSecondaryTerminalId)"
      >
        {{ getLabel(resolvedSecondaryTerminalId) }}
      </button>
    </div>

    <label v-if="splitCandidates.length > 0" class="split-selector">
      <span>{{ t('terminal.split') }}</span>
      <select :value="resolvedSecondaryTerminalId || secondaryTerminalId" @change="emit('change-secondary-terminal', $event)">
        <option v-for="terminal in splitCandidates" :key="terminal.terminalId" :value="terminal.terminalId">
          {{ terminal.label }}
        </option>
      </select>
    </label>
  </div>
</template>

<style scoped>
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

@media (max-width: 980px) {
  .split-toolbar {
    align-items: flex-start;
    flex-direction: column;
  }

  .split-selector {
    width: 100%;
    justify-content: space-between;
  }

  .split-selector select {
    min-width: 0;
    flex: 1;
  }
}
</style>
