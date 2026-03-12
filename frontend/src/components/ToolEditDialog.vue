<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

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
const touchedFields = reactive({
  name: false,
  path: false,
  python: false,
})
const submitAttempted = ref(false)

const { t } = useI18n()
const notify = useNotify()

const isPythonEdit = computed(() => editForm.type === 'python')
const validationErrors = computed(() => {
  const id = editForm.id.trim()
  const name = editForm.name.trim()
  const path = editForm.path.trim()

  return {
    id: !id ? t('tools.validationId') : /^[a-zA-Z0-9._-]+$/.test(id) ? '' : t('tools.validationIdFormat'),
    name: name ? '' : t('tools.validationName'),
    path: path ? '' : t('tools.validationPath'),
  }
})
const invalidToolSummary = computed(() => {
  if (!props.tool || props.tool.valid) {
    return ''
  }

  return props.tool.validationMessage?.trim() || t('tools.invalidHint')
})
const pathHint = computed(() => {
  if (validationErrors.value.path) {
    return validationErrors.value.path
  }

  return editForm.type === 'python' ? t('addTool.pyHint') : t('addTool.exeHint')
})
const pythonHint = computed(() => {
  if (!isPythonEdit.value) {
    return ''
  }

  return editForm.python.trim() ? t('tools.pythonOverrideHint') : t('addTool.pythonHelp')
})
const cwdHint = computed(() => t('tools.cwdInlineHint'))
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
    resetValidationState()
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

function resetValidationState(): void {
  touchedFields.name = false
  touchedFields.path = false
  touchedFields.python = false
  submitAttempted.value = false
}

function markTouched(field: keyof typeof touchedFields): void {
  touchedFields[field] = true
}

function shouldShowError(field: keyof typeof validationErrors.value): boolean {
  if (field === 'id') {
    return Boolean(validationErrors.value.id)
  }

  const touched = field in touchedFields ? touchedFields[field as keyof typeof touchedFields] : false
  return Boolean(validationErrors.value[field] && (submitAttempted.value || touched))
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
  submitAttempted.value = true
  touchedFields.name = true
  touchedFields.path = true
  touchedFields.python = true

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
          <div v-if="invalidToolSummary" class="validation-banner">
            <strong>{{ t('tools.invalidPath') }}</strong>
            <span>{{ invalidToolSummary }}</span>
          </div>

          <label class="form-field" :class="{ 'has-error': shouldShowError('id') }">
            <span>ID</span>
            <input v-model="editForm.id" type="text" disabled />
            <small class="field-hint" :class="{ 'is-error': shouldShowError('id') }">
              {{ shouldShowError('id') ? validationErrors.id : t('addTool.idHint') }}
            </small>
          </label>

          <label class="form-field" :class="{ 'has-error': shouldShowError('name') }">
            <span>{{ t('tools.name') }}</span>
            <input v-model="editForm.name" type="text" @blur="markTouched('name')" />
            <small class="field-hint" :class="{ 'is-error': shouldShowError('name') }">
              {{ shouldShowError('name') ? validationErrors.name : t('tools.nameInlineHint') }}
            </small>
          </label>

          <label class="form-field">
            <span>{{ t('tools.type') }}</span>
            <select v-model="editForm.type">
              <option value="python">python</option>
              <option value="exe">exe</option>
            </select>
          </label>

          <label class="form-field" :class="{ 'has-error': shouldShowError('path') }">
            <span>{{ t('tools.path') }}</span>
            <div class="path-row">
              <input v-model="editForm.path" type="text" @blur="markTouched('path')" />
              <button class="action-button" type="button" @click="browseEditToolPath">{{ t('tools.browse') }}</button>
            </div>
            <small class="field-hint" :class="{ 'is-error': shouldShowError('path') }">{{ pathHint }}</small>
          </label>

          <label v-if="isPythonEdit" class="form-field">
            <span>{{ t('tools.python') }}</span>
            <div class="path-row">
              <input v-model="editForm.python" type="text" @blur="markTouched('python')" />
              <button class="action-button" type="button" @click="browseEditToolPython">{{ t('tools.browse') }}</button>
            </div>
            <small class="field-hint">{{ pythonHint }}</small>
          </label>

          <label class="form-field">
            <span>{{ t('tools.cwd') }}</span>
            <input v-model="editForm.cwd" type="text" />
            <small class="field-hint">{{ cwdHint }}</small>
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

.validation-banner {
  grid-column: 1 / -1;
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 11px 12px;
  border: 1px solid color-mix(in srgb, var(--status-warning) 38%, transparent);
  border-radius: 8px;
  background: var(--status-warning-soft);
  color: var(--vscode-text-primary);
}

.validation-banner strong {
  color: var(--status-warning);
  font-size: 12px;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-field.has-error input,
.form-field.has-error select,
.form-field.has-error textarea {
  border-color: var(--status-danger);
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

.field-hint {
  font-size: 11px;
  line-height: 1.45;
  color: var(--vscode-text-muted);
}

.field-hint.is-error {
  color: var(--status-danger);
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
