<script setup lang="ts">
defineProps<{
  visible: boolean
  x: number
  y: number
  terminalId: string
  t: (key: string) => string
}>()

const emit = defineEmits<{
  (e: 'split-with-terminal', terminalId: string): void
  (e: 'clear-output'): void
  (e: 'stop-terminal', terminalId: string): void
  (e: 'stop-other-terminals', terminalId: string): void
  (e: 'copy-terminal-id', terminalId: string): void
}>()
</script>

<template>
  <teleport to="body">
    <div
      v-if="visible"
      class="terminal-context-menu"
      :style="{ left: `${x}px`, top: `${y}px` }"
      @mousedown.stop
    >
      <button class="menu-item" type="button" @click="emit('split-with-terminal', terminalId)">{{ t('terminal.split') }}</button>
      <button class="menu-item" type="button" @click="emit('clear-output')">{{ t('terminal.clearActive') }}</button>
      <button class="menu-item danger" type="button" @click="emit('stop-terminal', terminalId)">{{ t('terminal.kill') }}</button>
      <button class="menu-item" type="button" @click="emit('stop-other-terminals', terminalId)">{{ t('terminal.killOthers') }}</button>
      <button class="menu-item" type="button" @click="emit('copy-terminal-id', terminalId)">{{ t('terminal.copyId') }}</button>
    </div>
  </teleport>
</template>

<style scoped>
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
</style>
