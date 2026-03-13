import { computed, reactive, ref } from 'vue'

import { useI18n } from './useI18n'
import type { AddToolPayload, ToolItem, ToolType } from '../types'
import {
  isCommandToolType,
  isScriptToolType,
  isUrlToolType,
  normalizeToolType,
  supportsPathBrowse,
  isValidHttpUrl,
} from '../utils/toolTypes'

export interface ToolFormState {
  id: string
  name: string
  type: ToolType
  path: string
  runtimePath: string
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
    runtimePath: '',
    cwd: '',
    argsTemplate: '',
    tagsText: '',
    description: '',
  })
  const touchedFields = reactive({
    id: false,
    name: false,
    path: false,
    runtimePath: false,
  })
  const submitAttempted = ref(false)

  const isScriptTool = computed(() => isScriptToolType(form.type))
  const needsRuntimePath = computed(() => isScriptTool.value)
  const isUrlTool = computed(() => isUrlToolType(form.type))
  const isCommandTool = computed(() => isCommandToolType(form.type))
  const validationErrors = computed(() => {
    const id = form.id.trim()
    const name = form.name.trim()
    const path = form.path.trim()

    let pathError = ''
    if (!path) {
      pathError = t('addTool.validationPath')
    } else if (isUrlTool.value && !isValidHttpUrl(path)) {
      pathError = t('addTool.validationUrl')
    }

    return {
      id: !id ? t('addTool.validationId') : /^[a-zA-Z0-9._-]+$/.test(id) ? '' : t('addTool.validationIdFormat'),
      name: name ? '' : t('addTool.validationName'),
      path: pathError,
    }
  })
  const firstValidationMessage = computed(() => {
    return validationErrors.value.id || validationErrors.value.name || validationErrors.value.path || ''
  })
  const pathHint = computed(() => {
    if (shouldShowError('path')) {
      return validationErrors.value.path
    }

    if (form.type === 'python') {
      return t('addTool.pyHint')
    }

    if (form.type === 'node') {
      return t('addTool.nodeScriptHint')
    }

    if (form.type === 'executable') {
      return t('addTool.exeHint')
    }

    if (form.type === 'command') {
      return t('addTool.commandHint')
    }

    return t('addTool.urlHint')
  })
  const runtimeHint = computed(() => {
    if (!needsRuntimePath.value) {
      return ''
    }

    if (form.runtimePath.trim()) {
      return t('addTool.runtimeOverrideHint')
    }

    return form.type === 'python'
      ? t('addTool.pythonHelp')
      : t('addTool.nodeRuntimeHelp')
  })

  function fillWorkingDirectory(path: string): void {
    const normalized = path.replace(/\\/g, '/')
    const idx = normalized.lastIndexOf('/')
    if (idx > 0) {
      form.cwd = normalized.slice(0, idx)
    }
  }

  function createNameFromInput(input: string): string {
    if (isUrlTool.value) {
      try {
        const url = new URL(input)
        const segment = url.pathname.split('/').filter(Boolean).pop()
        return segment || url.hostname || 'new_tool'
      } catch {
        return 'new_tool'
      }
    }

    if (isCommandTool.value) {
      const [command] = input.trim().split(/\s+/, 1)
      return command || 'new_tool'
    }

    const normalized = input.replace(/\\/g, '/')
    const filename = normalized.slice(normalized.lastIndexOf('/') + 1)
    const dot = filename.lastIndexOf('.')
    const stem = dot > 0 ? filename.slice(0, dot) : filename
    return stem || 'new_tool'
  }

  function createIdFromInput(input: string): string {
    const name = createNameFromInput(input)
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
    touchedFields.runtimePath = false
    submitAttempted.value = false
  }

  function resetForm(): void {
    form.id = ''
    form.name = ''
    form.type = defaultType
    form.path = ''
    form.runtimePath = ''
    form.cwd = ''
    form.argsTemplate = ''
    form.tagsText = ''
    form.description = ''
    resetValidationState()
  }

  function setFromTool(tool: ToolItem): void {
    form.id = tool.id
    form.name = tool.name
    form.type = normalizeToolType(tool.type)
    form.path = tool.path
    form.runtimePath = tool.runtimePath ?? ''
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
    if (autoFill && supportsPathBrowse(form.type) && !form.cwd.trim()) {
      fillWorkingDirectory(path)
    }
    if (autoFill && !form.id.trim()) {
      form.id = createIdFromInput(path)
    }
    if (autoFill && !form.name.trim()) {
      form.name = createNameFromInput(path)
    }
  }

  function applyRuntimeSuggestion(path: string): void {
    if (!path.trim()) {
      return
    }

    form.runtimePath = path
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
      type: normalizeToolType(form.type),
      path: form.path.trim(),
      runtimePath: needsRuntimePath.value ? form.runtimePath.trim() || undefined : undefined,
      cwd: isUrlTool.value ? undefined : form.cwd.trim() || undefined,
      argsTemplate: isUrlTool.value ? '' : form.argsTemplate.trim(),
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
    isScriptTool,
    needsRuntimePath,
    isUrlTool,
    isCommandTool,
    validationErrors,
    firstValidationMessage,
    pathHint,
    runtimeHint,
    fillWorkingDirectory,
    createNameFromInput,
    createIdFromInput,
    resetValidationState,
    resetForm,
    setFromTool,
    applyPathSuggestion,
    applyRuntimeSuggestion,
    parseTags,
    createPayload,
    markTouched,
    shouldShowError,
  }
}
