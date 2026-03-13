<script setup lang="ts">
import { computed, watch } from 'vue'

import UiButton from './ui/UiButton.vue'
import UiField from './ui/UiField.vue'
import UiInput from './ui/UiInput.vue'
import UiOverlay from './ui/UiOverlay.vue'
import UiPanel from './ui/UiPanel.vue'
import UiSelect from './ui/UiSelect.vue'
import UiTextarea from './ui/UiTextarea.vue'
import { useI18n } from '../composables/useI18n'
import { useNotify } from '../composables/useNotify'
import { useToolForm } from '../composables/useToolForm'
import type { AddToolPayload, ToolItem } from '../types'
import { supportsPathBrowse } from '../utils/toolTypes'

const props = defineProps<{
  visible: boolean
  tool: ToolItem | null
  updating?: boolean
  editToolPathSelection?: string
  editToolRuntimeSelection?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'save', payload: AddToolPayload): void
  (e: 'pickToolPath', payload: { defaultPath?: string; toolType?: string }): void
  (e: 'pickToolRuntime', payload: { defaultPath?: string; toolType?: string }): void
}>()

const { t } = useI18n()
const notify = useNotify()
const {
  form,
  submitAttempted,
  needsRuntimePath,
  isUrlTool,
  validationErrors,
  pathHint,
  runtimeHint,
  setFromTool,
  applyPathSuggestion,
  applyRuntimeSuggestion,
  createPayload,
  markTouched,
  resetValidationState,
  shouldShowError,
} = useToolForm('python')

const invalidToolSummary = computed(() => {
  if (!props.tool || props.tool.valid) {
    return ''
  }

  return props.tool.validationMessage?.trim() || t('tools.invalidHint')
})

const toolPathLabel = computed(() => {
  if (form.type === 'command') {
    return t('addTool.commandPath')
  }

  if (form.type === 'url') {
    return t('addTool.urlPath')
  }

  return t('tools.path')
})

const runtimeLabel = computed(() => {
  return form.type === 'python'
    ? t('addTool.pythonPath')
    : t('addTool.nodeRuntimePath')
})

watch(
  () => props.tool,
  (tool) => {
    if (tool) {
      setFromTool(tool)
    }
  },
  { immediate: true },
)

watch(
  () => props.editToolPathSelection,
  (path) => {
    if (props.visible && path?.trim()) {
      applyPathSuggestion(path, false)
    }
  },
)

watch(
  () => props.editToolRuntimeSelection,
  (path) => {
    if (props.visible && path?.trim()) {
      applyRuntimeSuggestion(path)
    }
  },
)

watch(
  () => form.type,
  (nextType) => {
    if (!needsRuntimePath.value) {
      form.runtimePath = ''
    }

    if (nextType === 'url') {
      form.cwd = ''
      form.argsTemplate = ''
    }
  },
)

function closeDialog(): void {
  emit('update:visible', false)
  resetValidationState()
}

function browseEditToolPath(): void {
  emit('pickToolPath', {
    defaultPath: form.path || form.cwd || undefined,
    toolType: form.type,
  })
}

function browseEditToolRuntime(): void {
  emit('pickToolRuntime', {
    defaultPath: form.runtimePath || form.cwd || form.path || undefined,
    toolType: form.type,
  })
}

function saveEdit(): void {
  submitAttempted.value = true
  markTouched('id')
  markTouched('name')
  markTouched('path')
  markTouched('runtimePath')

  if (!/^[a-zA-Z0-9._-]+$/.test(form.id.trim())) {
    notify.warning(t('tools.validationIdFormat'), {
      groupKey: 'tools.edit.validation',
      mergeMode: 'replace',
    })
    return
  }

  if (!form.name.trim()) {
    notify.warning(t('tools.validationName'), {
      groupKey: 'tools.edit.validation',
      mergeMode: 'replace',
    })
    return
  }

  if (!form.path.trim()) {
    notify.warning(t('tools.validationPath'), {
      groupKey: 'tools.edit.validation',
      mergeMode: 'replace',
    })
    return
  }

  if (isUrlTool.value && validationErrors.value.path) {
    notify.warning(validationErrors.value.path, {
      groupKey: 'tools.edit.validation',
      mergeMode: 'replace',
    })
    return
  }

  emit('save', createPayload())
  closeDialog()
}
</script>

<template>
  <teleport to="body">
    <UiOverlay v-if="visible" @click.self="closeDialog">
      <UiPanel class="max-h-[calc(100vh-3rem)] w-[min(720px,calc(100vw-2rem))] overflow-hidden bg-sidebar shadow-dialog">
        <div class="flex items-center justify-between border-b border-border px-4 py-3">
          <h3 class="text-sm font-semibold text-foreground">{{ t('tools.editDialog') }}</h3>
          <UiButton variant="ghost" size="sm" @click="closeDialog">x</UiButton>
        </div>

        <div class="grid max-h-[calc(100vh-11rem)] gap-4 overflow-auto p-4 xl:grid-cols-2">
          <div v-if="invalidToolSummary" class="xl:col-span-2 rounded-field border border-warning/40 bg-warning-soft p-3">
            <strong class="block text-xs font-semibold text-warning">{{ t('tools.invalidPath') }}</strong>
            <span class="mt-1 block text-xs leading-5 text-foreground">{{ invalidToolSummary }}</span>
          </div>

          <UiField
            label="ID"
            :hint="shouldShowError('id') ? undefined : t('addTool.idHint')"
            :error="shouldShowError('id') ? validationErrors.id : ''"
          >
            <UiInput v-model="form.id" readonly :invalid="shouldShowError('id')" />
          </UiField>

          <UiField
            :label="t('tools.name')"
            :hint="shouldShowError('name') ? undefined : t('tools.nameInlineHint')"
            :error="shouldShowError('name') ? validationErrors.name : ''"
          >
            <UiInput v-model="form.name" :invalid="shouldShowError('name')" @blur="markTouched('name')" />
          </UiField>

          <UiField :label="t('tools.type')">
            <UiSelect v-model="form.type">
              <option value="python">python</option>
              <option value="node">node</option>
              <option value="command">command</option>
              <option value="executable">executable</option>
              <option value="url">url</option>
            </UiSelect>
          </UiField>

          <div class="xl:col-span-2">
            <UiField :label="toolPathLabel" :error="shouldShowError('path') ? validationErrors.path : ''" :hint="pathHint">
              <div class="grid gap-2" :class="supportsPathBrowse(form.type) ? 'md:grid-cols-[minmax(0,1fr)_auto]' : ''">
                <UiInput v-model="form.path" :invalid="shouldShowError('path')" @blur="markTouched('path')" />
                <UiButton v-if="supportsPathBrowse(form.type)" @click="browseEditToolPath">{{ t('tools.browse') }}</UiButton>
              </div>
            </UiField>
          </div>

          <div v-if="needsRuntimePath" class="xl:col-span-2">
            <UiField :label="runtimeLabel" :hint="runtimeHint">
              <div class="grid gap-2 md:grid-cols-[minmax(0,1fr)_auto]">
                <UiInput v-model="form.runtimePath" @blur="markTouched('runtimePath')" />
                <UiButton @click="browseEditToolRuntime">{{ t('tools.browse') }}</UiButton>
              </div>
            </UiField>
          </div>

          <UiField v-if="!isUrlTool" :label="t('tools.cwd')" :hint="t('tools.cwdInlineHint')">
            <UiInput v-model="form.cwd" />
          </UiField>

          <UiField v-if="!isUrlTool" :label="t('tools.argsTemplate')">
            <UiInput v-model="form.argsTemplate" />
          </UiField>

          <UiField :label="t('tools.tags')">
            <UiInput v-model="form.tagsText" />
          </UiField>

          <div class="xl:col-span-2">
            <UiField :label="t('tools.description')">
              <UiTextarea v-model="form.description" rows="4" />
            </UiField>
          </div>
        </div>

        <div class="flex justify-end gap-2 border-t border-border px-4 py-3">
          <UiButton @click="closeDialog">{{ t('tools.cancel') }}</UiButton>
          <UiButton variant="primary" :disabled="updating" @click="saveEdit">{{ t('tools.save') }}</UiButton>
        </div>
      </UiPanel>
    </UiOverlay>
  </teleport>
</template>
