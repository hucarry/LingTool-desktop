<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import { Button as DButton } from 'vue-devui/button'
import { Form as DForm, FormItem as DFormItem } from 'vue-devui/form'
import { Input as DInput } from 'vue-devui/input'
import { Select as DSelect } from 'vue-devui/select'
import 'vue-devui/button/style.css'
import 'vue-devui/form/style.css'
import 'vue-devui/input/style.css'
import 'vue-devui/select/style.css'

import { useToolHub } from '../composables/useToolHub'
import { useI18n } from '../composables/useI18n'
import { useNotify } from '../composables/useNotify'
import type { AddToolPayload, ToolType } from '../types'

const hub = useToolHub()
const { t } = useI18n()
const notify = useNotify()

const toolTypeOptions = [
  { label: 'Python', value: 'python' },
  { label: 'EXE', value: 'exe' },
]

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

const isPythonTool = computed(() => form.type === 'python')
const submitting = computed(() => hub.addingTool.value)

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
  const id = form.id.trim()
  const name = form.name.trim()
  const path = form.path.trim()

  if (!id) {
    notify.warning(t('addTool.validationId'))
    return
  }

  if (!/^[a-zA-Z0-9._-]+$/.test(id)) {
    notify.warning(t('addTool.validationIdFormat'))
    return
  }

  if (!name) {
    notify.warning(t('addTool.validationName'))
    return
  }

  if (!path) {
    notify.warning(t('addTool.validationPath'))
    return
  }

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
  notify.info(t('addTool.resetDone'))
}
</script>

<template>
  <section class="add-tool-view">
    <header class="view-header">
      <h2>{{ t('addTool.title') }}</h2>
      <p>{{ t('addTool.subtitle') }}</p>
    </header>

    <d-form label-position="top" class="add-tool-form">
      <div class="form-grid">
        <d-form-item :label="t('addTool.toolType')">
          <d-select
            v-model="form.type"
            :options="toolTypeOptions"
            :allow-clear="false"
          />
        </d-form-item>

        <d-form-item :label="t('addTool.toolId')">
          <d-input v-model="form.id" :placeholder="t('addTool.idHint')" clearable />
        </d-form-item>

        <d-form-item :label="t('addTool.toolName')">
          <d-input v-model="form.name" clearable />
        </d-form-item>

        <d-form-item :label="t('addTool.toolPath')" class="path-item">
          <div class="path-input-row">
            <d-input
              v-model="form.path"
              :placeholder="isPythonTool ? t('addTool.pyHint') : t('addTool.exeHint')"
              clearable
            />
            <d-button @click="browseToolPath">{{ t('tools.browse') }}</d-button>
          </div>
        </d-form-item>

        <d-form-item v-if="isPythonTool" :label="t('addTool.pythonPath')" class="path-item">
          <div class="path-input-row">
            <d-input v-model="form.python" clearable />
            <d-button @click="browsePythonPath">{{ t('tools.browse') }}</d-button>
          </div>
          <p class="tip-text">{{ t('addTool.pythonHelp') }}</p>
        </d-form-item>

        <d-form-item :label="t('addTool.cwd')">
          <d-input v-model="form.cwd" clearable />
        </d-form-item>

        <d-form-item :label="t('addTool.argsTemplate')">
          <d-input v-model="form.argsTemplate" clearable />
        </d-form-item>

        <d-form-item :label="t('addTool.tags')">
          <d-input v-model="form.tagsText" :placeholder="t('addTool.tagsHint')" clearable />
        </d-form-item>

        <d-form-item :label="t('addTool.description')" class="description-item">
          <d-input v-model="form.description" type="textarea" :rows="3" />
        </d-form-item>
      </div>

      <div class="actions">
        <d-button @click="clearForm">{{ t('addTool.clear') }}</d-button>
        <d-button type="primary" :loading="submitting" @click="submit">{{ t('addTool.submit') }}</d-button>
      </div>
    </d-form>
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

.path-item,
.description-item {
  grid-column: 1 / -1;
}

.path-input-row {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 8px;
}

.tip-text {
  margin-top: 6px;
  color: var(--vscode-text-muted);
  font-size: 12px;
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
