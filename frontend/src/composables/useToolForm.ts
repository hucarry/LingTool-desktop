import { computed, reactive, ref } from 'vue'

import { useI18n } from './useI18n'
import type { AddToolPayload, ToolItem, ToolType } from '../types'

export interface ToolFormState {
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

export function useToolForm(defaultType: ToolType = 'python') {
  const { t } = useI18n()
  const form = reactive<ToolFormState>({
    id: '',
    name: '',
    type: defaultType,
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
  const firstValidationMessage = computed(() => {
    return validationErrors.value.id || validationErrors.value.name || validationErrors.value.path || ''
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

  function resetValidationState(): void {
    touchedFields.id = false
    touchedFields.name = false
    touchedFields.path = false
    touchedFields.python = false
    submitAttempted.value = false
  }

  function resetForm(): void {
    form.id = ''
    form.name = ''
    form.type = defaultType
    form.path = ''
    form.python = ''
    form.cwd = ''
    form.argsTemplate = ''
    form.tagsText = ''
    form.description = ''
    resetValidationState()
  }

  function setFromTool(tool: ToolItem): void {
    form.id = tool.id
    form.name = tool.name
    form.type = tool.type === 'python' ? 'python' : 'exe'
    form.path = tool.path
    form.python = tool.python ?? ''
    form.cwd = tool.cwd ?? ''
    form.argsTemplate = tool.argsTemplate ?? ''
    form.tagsText = tool.tags.join(', ')
    form.description = tool.description ?? ''
    resetValidationState()
  }

  function applyPathSuggestion(path: string, autoFill = true): void {
    if (!path.trim()) {
      return
    }

    form.path = path
    if (autoFill && !form.cwd.trim()) {
      fillWorkingDirectory(path)
    }
    if (autoFill && !form.id.trim()) {
      form.id = createIdFromPath(path)
    }
    if (autoFill && !form.name.trim()) {
      form.name = createNameFromPath(path)
    }
  }

  function applyPythonSuggestion(path: string): void {
    if (!path.trim()) {
      return
    }

    form.python = path
  }

  function parseTags(tagsText: string): string[] {
    return tagsText
      .split(',')
      .map((item) => item.trim())
      .filter((item, index, array) => item.length > 0 && array.indexOf(item) === index)
  }

  function createPayload(): AddToolPayload {
    return {
      id: form.id.trim(),
      name: form.name.trim(),
      type: form.type,
      path: form.path.trim(),
      python: isPythonTool.value ? form.python.trim() || undefined : undefined,
      cwd: form.cwd.trim() || undefined,
      argsTemplate: form.argsTemplate.trim(),
      tags: parseTags(form.tagsText),
      description: form.description.trim() || undefined,
    }
  }

  function markTouched(field: keyof typeof touchedFields): void {
    touchedFields[field] = true
  }

  function shouldShowError(field: keyof typeof validationErrors.value): boolean {
    const touched = field in touchedFields ? touchedFields[field as keyof typeof touchedFields] : false
    return Boolean(validationErrors.value[field] && (submitAttempted.value || touched))
  }

  return {
    form,
    touchedFields,
    submitAttempted,
    isPythonTool,
    validationErrors,
    firstValidationMessage,
    pathHint,
    pythonHint,
    fillWorkingDirectory,
    createNameFromPath,
    createIdFromPath,
    resetValidationState,
    resetForm,
    setFromTool,
    applyPathSuggestion,
    applyPythonSuggestion,
    parseTags,
    createPayload,
    markTouched,
    shouldShowError,
  }
}
