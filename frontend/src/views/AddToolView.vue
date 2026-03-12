<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { useToolHub } from '../composables/useToolHub'
import { useI18n } from '../composables/useI18n'
import { useNotify } from '../composables/useNotify'
import type { AddToolPayload, ToolType } from '../types'

const hub = useToolHub()
const { t } = useI18n()
const notify = useNotify()

interface AddToolFormState {
  id: string
  name: string
  type: ToolType
  path: string
  python: string
  cwd: string
  argsTemplate: string
  tagsText: string
  description: string
}

const form = reactive<AddToolFormState>({
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
  id: false,
  name: false,
  path: false,
  python: false,
})
const submitAttempted = ref(false)

const isPythonTool = computed(() => form.type === 'python')
const submitting = computed(() => hub.addingTool.value)
const validationErrors = computed(() => {
  const id = form.id.trim()
  const name = form.name.trim()
  const path = form.path.trim()

  return {
    id: !id ? t('addTool.validationId') : /^[a-zA-Z0-9._-]+$/.test(id) ? '' : t('addTool.validationIdFormat'),
    name: name ? '' : t('addTool.validationName'),
    path: path ? '' : t('addTool.validationPath'),
  }
})
const pathHint = computed(() => {
  if (shouldShowError('path')) {
    return validationErrors.value.path
  }

  return isPythonTool.value ? t('addTool.pyHint') : t('addTool.exeHint')
})
const pythonHint = computed(() => {
  return form.python.trim() ? t('addTool.pythonOverrideHint') : t('addTool.pythonHelp')
})
const firstValidationMessage = computed(() => {
  return validationErrors.value.id || validationErrors.value.name || validationErrors.value.path || ''
})

watch(
  () => hub.addToolPathSelection.value,
  (path) => {
    if (!path?.trim()) {
      return
    }

    form.path = path
    if (!form.cwd.trim()) {
      fillWorkingDirectory(path)
    }

    if (!form.id.trim()) {
      form.id = createIdFromPath(path)
    }

    if (!form.name.trim()) {
      form.name = createNameFromPath(path)
    }
  },
)

watch(
  () => hub.addToolPythonSelection.value,
  (path) => {
    if (!path?.trim()) {
      return
    }

    form.python = path
  },
)

watch(
  () => hub.lastAddedToolId.value,
  (toolId) => {
    if (!toolId) {
      return
    }

    resetForm()
  },
)

function fillWorkingDirectory(path: string): void {
  const normalized = path.replace(/\\/g, '/')
  const idx = normalized.lastIndexOf('/')
  if (idx > 0) {
    form.cwd = normalized.slice(0, idx)
  }
}

function createNameFromPath(path: string): string {
  const normalized = path.replace(/\\/g, '/')
  const filename = normalized.slice(normalized.lastIndexOf('/') + 1)
  const dot = filename.lastIndexOf('.')
  const stem = dot > 0 ? filename.slice(0, dot) : filename
  return stem || 'new_tool'
}

function createIdFromPath(path: string): string {
  const name = createNameFromPath(path)
  const normalized = name
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9._-]+/g, '_')
    .replace(/^[_\-.]+|[_\-.]+$/g, '')

  return normalized || 'new_tool'
}

function resetForm(): void {
  form.id = ''
  form.name = ''
  form.type = 'python'
  form.path = ''
  form.python = ''
  form.cwd = ''
  form.argsTemplate = ''
  form.tagsText = ''
  form.description = ''
  resetValidationState()
}

function resetValidationState(): void {
  touchedFields.id = false
  touchedFields.name = false
  touchedFields.path = false
  touchedFields.python = false
  submitAttempted.value = false
}

function markTouched(field: keyof typeof touchedFields): void {
  touchedFields[field] = true
}

function shouldShowError(field: keyof typeof validationErrors.value): boolean {
  const touched = field in touchedFields ? touchedFields[field as keyof typeof touchedFields] : false
  return Boolean(validationErrors.value[field] && (submitAttempted.value || touched))
}

function browseToolPath(): void {
  const defaultPath = form.path.trim() || form.cwd.trim() || undefined
  hub.pickAddToolPath(defaultPath, form.type)
}

function browsePythonPath(): void {
  const defaultPath = form.python.trim() || form.cwd.trim() || form.path.trim() || undefined
  hub.pickAddToolPython(defaultPath)
}

function parseTags(tagsText: string): string[] {
  return tagsText
    .split(',')
    .map((item) => item.trim())
    .filter((item, index, arr) => item.length > 0 && arr.indexOf(item) === index)
}

function submit(): void {
  submitAttempted.value = true
  touchedFields.id = true
  touchedFields.name = true
  touchedFields.path = true
  touchedFields.python = true

  if (firstValidationMessage.value) {
    notify.warning(firstValidationMessage.value, {
      groupKey: 'addTool.validation',
      mergeMode: 'replace',
    })
    return
  }

  const id = form.id.trim()
  const name = form.name.trim()
  const path = form.path.trim()

  const payload: AddToolPayload = {
    id,
    name,
    type: form.type,
    path,
    python: isPythonTool.value ? form.python.trim() || undefined : undefined,
    cwd: form.cwd.trim() || undefined,
    argsTemplate: form.argsTemplate.trim(),
    tags: parseTags(form.tagsText),
    description: form.description.trim() || undefined,
  }

  hub.addTool(payload)
}

function clearForm(): void {
  resetForm()
  notify.info(t('addTool.resetDone'), {
    groupKey: 'addTool.reset',
    mergeMode: 'replace',
  })
}
</script>

<template>
  <section class="add-tool-view">
    <header class="view-header">
      <h2>{{ t('addTool.title') }}</h2>
      <p>{{ t('addTool.subtitle') }}</p>
    </header>

    <form class="add-tool-form" @submit.prevent="submit">
      <div class="form-grid">
        <div class="form-summary">
          <strong>{{ t('addTool.summaryTitle') }}</strong>
          <span>{{ form.path.trim() ? t('addTool.summaryReady') : t('addTool.summaryEmpty') }}</span>
        </div>

        <label class="form-field">
          <span>{{ t('addTool.toolType') }}</span>
          <select v-model="form.type">
            <option value="python">Python</option>
            <option value="exe">EXE</option>
          </select>
        </label>

        <label class="form-field" :class="{ 'has-error': shouldShowError('id') }">
          <span>{{ t('addTool.toolId') }}</span>
          <input v-model="form.id" type="text" :placeholder="t('addTool.idHint')" @blur="markTouched('id')" />
          <p class="tip-text" :class="{ 'is-error': shouldShowError('id') }">
            {{ shouldShowError('id') ? validationErrors.id : t('addTool.idHint') }}
          </p>
        </label>

        <label class="form-field" :class="{ 'has-error': shouldShowError('name') }">
          <span>{{ t('addTool.toolName') }}</span>
          <input v-model="form.name" type="text" @blur="markTouched('name')" />
          <p class="tip-text" :class="{ 'is-error': shouldShowError('name') }">
            {{ shouldShowError('name') ? validationErrors.name : t('addTool.nameInlineHint') }}
          </p>
        </label>

        <label class="form-field path-item" :class="{ 'has-error': shouldShowError('path') }">
          <span>{{ t('addTool.toolPath') }}</span>
          <div class="path-input-row">
            <input
              v-model="form.path"
              type="text"
              :placeholder="isPythonTool ? t('addTool.pyHint') : t('addTool.exeHint')"
              @blur="markTouched('path')"
            />
            <button class="field-button" type="button" @click="browseToolPath">{{ t('tools.browse') }}</button>
          </div>
          <p class="tip-text" :class="{ 'is-error': shouldShowError('path') }">{{ pathHint }}</p>
        </label>

        <label v-if="isPythonTool" class="form-field path-item">
          <span>{{ t('addTool.pythonPath') }}</span>
          <div class="path-input-row">
            <input v-model="form.python" type="text" @blur="markTouched('python')" />
            <button class="field-button" type="button" @click="browsePythonPath">{{ t('tools.browse') }}</button>
          </div>
          <p class="tip-text">{{ pythonHint }}</p>
        </label>

        <label class="form-field">
          <span>{{ t('addTool.cwd') }}</span>
          <input v-model="form.cwd" type="text" />
          <p class="tip-text">{{ t('addTool.cwdInlineHint') }}</p>
        </label>

        <label class="form-field">
          <span>{{ t('addTool.argsTemplate') }}</span>
          <input v-model="form.argsTemplate" type="text" />
        </label>

        <label class="form-field">
          <span>{{ t('addTool.tags') }}</span>
          <input v-model="form.tagsText" type="text" :placeholder="t('addTool.tagsHint')" />
        </label>

        <label class="form-field description-item">
          <span>{{ t('addTool.description') }}</span>
          <textarea v-model="form.description" rows="4" />
        </label>
      </div>

      <div class="actions">
        <button class="action-button" type="button" @click="clearForm">{{ t('addTool.clear') }}</button>
        <button class="action-button primary" type="submit" :disabled="submitting">{{ t('addTool.submit') }}</button>
      </div>
    </form>
  </section>
</template>

<style scoped>
.add-tool-view {
  height: 100%;
  min-height: 0;
  border: 1px solid var(--vscode-border-color);
  background: var(--vscode-editor-bg);
  display: flex;
  flex-direction: column;
  overflow: auto;
}

.view-header {
  padding: 12px 14px 8px;
  border-bottom: 1px solid var(--vscode-border-color);
}

.view-header h2 {
  font-size: 16px;
  font-weight: 600;
  color: var(--vscode-text-primary);
}

.view-header p {
  margin-top: 4px;
  color: var(--vscode-text-muted);
  font-size: 12px;
}

.add-tool-form {
  padding: 12px 14px;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(240px, 1fr));
  gap: 10px 14px;
}

.form-summary {
  grid-column: 1 / -1;
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 11px 12px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 8px;
  background: var(--surface-muted);
}

.form-summary strong {
  font-size: 12px;
  color: var(--vscode-text-primary);
}

.form-summary span {
  font-size: 12px;
  color: var(--vscode-text-muted);
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
  background: var(--vscode-sidebar-bg);
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

.path-item,
.description-item {
  grid-column: 1 / -1;
}

.path-input-row {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 8px;
}

.field-button,
.action-button {
  height: 36px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 6px;
  background: var(--vscode-sidebar-bg);
  color: var(--vscode-text-primary);
  padding: 0 14px;
  cursor: pointer;
}

.field-button:hover,
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

.tip-text {
  margin-top: 6px;
  color: var(--vscode-text-muted);
  font-size: 12px;
}

.tip-text.is-error {
  color: var(--status-danger);
}

.actions {
  margin-top: 10px;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

@media (max-width: 960px) {
  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
