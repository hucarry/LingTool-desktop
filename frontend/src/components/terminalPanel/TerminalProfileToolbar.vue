<script setup lang="ts">
const shellInput = defineModel<string>('shellInput', { required: true })
const cwdInput = defineModel<string>('cwdInput', { required: true })

defineProps<{
  visible: boolean
  hasTerminalSessions: boolean
  t: (key: string) => string
}>()

const emit = defineEmits<{
  (e: 'create-terminal'): void
  (e: 'use-current-shell'): void
  (e: 'use-current-cwd'): void
  (e: 'clear-profile-inputs'): void
}>()
</script>

<template>
  <div v-show="visible" class="advanced-toolbar">
    <label class="profile-field">
      <span>{{ t('terminal.shell') }}</span>
      <input v-model="shellInput" type="text" placeholder="powershell.exe" />
    </label>

    <label class="profile-field">
      <span>{{ t('terminal.workingDir') }}</span>
      <input v-model="cwdInput" type="text" placeholder="C:\\project" />
    </label>

    <button class="profile-create" type="button" @click="emit('create-terminal')">
      {{ t('terminal.create') }}
    </button>

    <div v-if="hasTerminalSessions" class="profile-shortcuts">
      <button class="profile-shortcut" type="button" @click="emit('use-current-shell')">Shell &lt;- current</button>
      <button class="profile-shortcut" type="button" @click="emit('use-current-cwd')">CWD &lt;- current</button>
      <button class="profile-shortcut" type="button" @click="emit('clear-profile-inputs')">Clear</button>
    </div>
  </div>
</template>

<style scoped>
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

@media (max-width: 980px) {
  .advanced-toolbar {
    grid-template-columns: 1fr;
  }
}
</style>
