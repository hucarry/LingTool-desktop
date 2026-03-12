<script setup lang="ts">
import { computed, reactive, watch } from 'vue'

import { useI18n } from '../composables/useI18n'
import { useNotify } from '../composables/useNotify'
import type { AddToolPayload, ToolItem } from '../types'

const props = defineProps<{
  visible: boolean
  tool: ToolItem | null
  updating?: boolean
  editToolPathSelection?: string
  editToolPythonSelection?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'save', payload: AddToolPayload): void
  (e: 'pickToolPath', payload: { defaultPath?: string; toolType?: string }): void
  (e: 'pickToolPython', payload: { defaultPath?: string }): void
}>()

const editForm = reactive({
  id: '',
  name: '',
  type: 'python',
  path: '',
  python: '',
  cwd: '',
  argsTemplate: '',
  tagsText: '',
  description: '',
})

const { t } = useI18n()
const notify = useNotify()

const isPythonEdit = computed(() => editForm.type === 'python')

watch(
  () => props.tool,
  (tool) => {
    if (!tool) {
      return
    }

    editForm.id = tool.id
    editForm.name = tool.name
    editForm.type = tool.type === 'python' ? 'python' : 'exe'
    editForm.path = tool.path
    editForm.python = tool.python ?? ''
    editForm.cwd = tool.cwd ?? ''
    editForm.argsTemplate = tool.argsTemplate ?? ''
    editForm.tagsText = tool.tags.join(', ')
    editForm.description = tool.description ?? ''
  },
  { immediate: true },
)

watch(
  () => props.editToolPathSelection,
  (path) => {
    if (!props.visible || !path?.trim()) {
      return
    }

    editForm.path = path
  },
)

watch(
  () => props.editToolPythonSelection,
  (path) => {
    if (!props.visible || !path?.trim()) {
      return
    }

    editForm.python = path
  },
)

watch(
  () => editForm.type,
  (nextType) => {
    if (nextType !== 'python') {
      editForm.python = ''
    }
  },
)

function closeDialog(): void {
  emit('update:visible', false)
}

function browseEditToolPath(): void {
  emit('pickToolPath', {
    defaultPath: editForm.path || editForm.cwd || undefined,
    toolType: editForm.type,
  })
}

function browseEditToolPython(): void {
  emit('pickToolPython', {
    defaultPath: editForm.python || editForm.cwd || editForm.path || undefined,
  })
}

function saveEdit(): void {
  const id = editForm.id.trim()
  const name = editForm.name.trim()
  const path = editForm.path.trim()

  if (!/^[a-zA-Z0-9._-]+$/.test(id)) {
    notify.warning(t('tools.validationIdFormat'), {
      groupKey: 'tools.edit.validation',
      mergeMode: 'replace',
    })
    return
  }

  if (!name) {
    notify.warning(t('tools.validationName'), {
      groupKey: 'tools.edit.validation',
      mergeMode: 'replace',
    })
    return
  }

  if (!path) {
    notify.warning(t('tools.validationPath'), {
      groupKey: 'tools.edit.validation',
      mergeMode: 'replace',
    })
    return
  }

  emit('save', {
    id,
    name,
    type: editForm.type,
    path,
    python: editForm.type === 'python' ? (editForm.python.trim() || undefined) : undefined,
    cwd: editForm.cwd.trim() || undefined,
    argsTemplate: editForm.argsTemplate.trim(),
    tags: editForm.tagsText
      .split(',')
      .map((item) => item.trim())
      .filter((item, index, arr) => item.length > 0 && arr.indexOf(item) === index),
    description: editForm.description.trim() || undefined,
  })
  closeDialog()
}
</script>

<template>
  <teleport to="body">
    <div v-if="visible" class="dialog-overlay" @click.self="closeDialog">
      <section class="dialog-shell">
        <header class="dialog-header">
          <h3>{{ t('tools.editDialog') }}</h3>
          <button class="icon-button" type="button" aria-label="Close" @click="closeDialog">x</button>
        </header>

        <div class="dialog-body">
          <label class="form-field">
            <span>ID</span>
            <input v-model="editForm.id" type="text" disabled />
          </label>

          <label class="form-field">
            <span>{{ t('tools.name') }}</span>
            <input v-model="editForm.name" type="text" />
          </label>

          <label class="form-field">
            <span>{{ t('tools.type') }}</span>
            <select v-model="editForm.type">
              <option value="python">python</option>
              <option value="exe">exe</option>
            </select>
          </label>

          <label class="form-field">
            <span>{{ t('tools.path') }}</span>
            <div class="path-row">
              <input v-model="editForm.path" type="text" />
              <button class="action-button" type="button" @click="browseEditToolPath">{{ t('tools.browse') }}</button>
            </div>
          </label>

          <label v-if="isPythonEdit" class="form-field">
            <span>{{ t('tools.python') }}</span>
            <div class="path-row">
              <input v-model="editForm.python" type="text" />
              <button class="action-button" type="button" @click="browseEditToolPython">{{ t('tools.browse') }}</button>
            </div>
          </label>

          <label class="form-field">
            <span>{{ t('tools.cwd') }}</span>
            <input v-model="editForm.cwd" type="text" />
          </label>

          <label class="form-field">
            <span>{{ t('tools.argsTemplate') }}</span>
            <input v-model="editForm.argsTemplate" type="text" />
          </label>

          <label class="form-field">
            <span>{{ t('tools.tags') }}</span>
            <input v-model="editForm.tagsText" type="text" />
          </label>

          <label class="form-field">
            <span>{{ t('tools.description') }}</span>
            <textarea v-model="editForm.description" rows="4" />
          </label>
        </div>

        <footer class="dialog-footer">
          <button class="action-button" type="button" @click="closeDialog">{{ t('tools.cancel') }}</button>
          <button class="action-button primary" type="button" :disabled="updating" @click="saveEdit">{{ t('tools.save') }}</button>
        </footer>
      </section>
    </div>
  </teleport>
</template>

<style scoped>
.dialog-overlay {
  position: fixed;
  inset: 0;
  z-index: 4300;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--overlay-backdrop);
  backdrop-filter: blur(2px);
}

.dialog-shell {
  width: min(680px, calc(100vw - 32px));
  max-height: calc(100vh - 48px);
  display: flex;
  flex-direction: column;
  border: 1px solid var(--vscode-border-color);
  border-radius: 10px;
  background: var(--vscode-sidebar-bg);
  box-shadow: var(--shadow-dialog);
}

.dialog-header,
.dialog-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 14px 16px;
}

.dialog-header {
  border-bottom: 1px solid var(--vscode-border-color);
}

.dialog-footer {
  justify-content: flex-end;
  border-top: 1px solid var(--vscode-border-color);
}

.dialog-header h3 {
  font-size: 15px;
  font-weight: 600;
  color: var(--vscode-text-primary);
}

.icon-button {
  width: 28px;
  height: 28px;
  border: 1px solid transparent;
  border-radius: 4px;
  background: transparent;
  color: var(--vscode-text-muted);
  cursor: pointer;
}

.icon-button:hover {
  background: var(--vscode-hover-bg);
  color: var(--vscode-text-primary);
}

.dialog-body {
  overflow: auto;
  padding: 16px;
  display: grid;
  grid-template-columns: repeat(2, minmax(220px, 1fr));
  gap: 12px 14px;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-field span {
  font-size: 12px;
  color: var(--vscode-text-primary);
}

.form-field input,
.form-field select,
.form-field textarea {
  width: 100%;
  border: 1px solid var(--vscode-border-color);
  border-radius: 6px;
  background: var(--vscode-editor-bg);
  color: var(--vscode-text-primary);
  padding: 0 12px;
  font: inherit;
}

.form-field input,
.form-field select {
  height: 36px;
}

.form-field textarea {
  min-height: 112px;
  padding-top: 10px;
  padding-bottom: 10px;
  resize: vertical;
}

.path-row {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 8px;
}

.action-button {
  height: 36px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 6px;
  background: var(--vscode-sidebar-bg);
  color: var(--vscode-text-primary);
  padding: 0 14px;
  cursor: pointer;
}

.action-button:hover:not(:disabled) {
  border-color: var(--vscode-accent-color);
  background: var(--vscode-hover-bg);
}

.action-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.action-button.primary {
  border-color: var(--vscode-accent-color);
  background: var(--vscode-accent-color);
  color: #ffffff;
}

@media (max-width: 760px) {
  .dialog-body {
    grid-template-columns: 1fr;
  }
}
</style>
