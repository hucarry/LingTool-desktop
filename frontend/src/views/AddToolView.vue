<script setup lang="ts">
import { computed, watch } from 'vue'
import { storeToRefs } from 'pinia'

import UiButton from '../components/ui/UiButton.vue'
import UiField from '../components/ui/UiField.vue'
import UiInput from '../components/ui/UiInput.vue'
import UiPanel from '../components/ui/UiPanel.vue'
import UiSelect from '../components/ui/UiSelect.vue'
import UiTextarea from '../components/ui/UiTextarea.vue'
import { useI18n } from '../composables/useI18n'
import { useNotify } from '../composables/useNotify'
import { useToolForm } from '../composables/useToolForm'
import { useToolsStore } from '../stores/tools'
import { supportsPathBrowse } from '../utils/toolTypes'

const toolsStore = useToolsStore()
const { addToolPathSelection, addToolRuntimeSelection, lastAddedToolId, addingTool } = storeToRefs(toolsStore)
const { t } = useI18n()
const notify = useNotify()
const {
  form,
  submitAttempted,
  isScriptTool,
  needsRuntimePath,
  isUrlTool,
  validationErrors,
  firstValidationMessage,
  pathHint,
  runtimeHint,
  resetForm,
  applyPathSuggestion,
  applyRuntimeSuggestion,
  createPayload,
  markTouched,
  shouldShowError,
} = useToolForm('python')

const toolPathLabel = computed(() => {
  if (form.type === 'command') {
    return t('addTool.commandPath')
  }

  if (form.type === 'url') {
    return t('addTool.urlPath')
  }

  return t('addTool.toolPath')
})

const runtimeLabel = computed(() => {
  return form.type === 'python'
    ? t('addTool.pythonPath')
    : t('addTool.nodeRuntimePath')
})

watch(addToolPathSelection, (path) => {
  if (path?.trim()) {
    applyPathSuggestion(path, true)
  }
})

watch(addToolRuntimeSelection, (path) => {
  if (path?.trim()) {
    applyRuntimeSuggestion(path)
  }
})

watch(lastAddedToolId, (toolId) => {
  if (toolId) {
    resetForm()
  }
})

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

function browseToolPath(): void {
  if (!supportsPathBrowse(form.type)) {
    return
  }

  const defaultPath = form.path.trim() || form.cwd.trim() || undefined
  toolsStore.pickAddToolPath(defaultPath, form.type)
}

function browseRuntimePath(): void {
  const defaultPath = form.runtimePath.trim() || form.cwd.trim() || form.path.trim() || undefined
  toolsStore.pickAddToolRuntime(defaultPath, form.type)
}

function submit(): void {
  submitAttempted.value = true
  markTouched('id')
  markTouched('name')
  markTouched('path')
  markTouched('runtimePath')

  if (firstValidationMessage.value) {
    notify.warning(firstValidationMessage.value, {
      groupKey: 'addTool.validation',
      mergeMode: 'replace',
    })
    return
  }

  toolsStore.addTool(createPayload())
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
  <section class="h-full overflow-auto p-3">
    <UiPanel class="space-y-4">
      <header class="space-y-1 border-b border-border pb-3">
        <h2 class="text-lg font-semibold text-foreground">{{ t('addTool.title') }}</h2>
        <p class="text-sm text-muted">{{ t('addTool.subtitle') }}</p>
      </header>

      <form class="space-y-4" @submit.prevent="submit">
        <div class="ui-panel space-y-1 bg-soft p-3">
          <strong class="text-xs font-semibold text-foreground">{{ t('addTool.summaryTitle') }}</strong>
          <span class="text-xs text-muted">
            {{ form.path.trim() ? t('addTool.summaryReady') : t('addTool.summaryEmpty') }}
          </span>
        </div>

        <div class="grid gap-4 xl:grid-cols-2">
          <UiField :label="t('addTool.toolType')">
            <UiSelect v-model="form.type">
              <option value="python">Python</option>
              <option value="node">Node.js</option>
              <option value="command">Command</option>
              <option value="executable">Executable</option>
              <option value="url">URL</option>
            </UiSelect>
          </UiField>

          <UiField
            :label="t('addTool.toolId')"
            :hint="shouldShowError('id') ? undefined : t('addTool.idHint')"
            :error="shouldShowError('id') ? validationErrors.id : ''"
          >
            <UiInput v-model="form.id" :invalid="shouldShowError('id')" @blur="markTouched('id')" />
          </UiField>

          <UiField
            :label="t('addTool.toolName')"
            :hint="shouldShowError('name') ? undefined : t('addTool.nameInlineHint')"
            :error="shouldShowError('name') ? validationErrors.name : ''"
          >
            <UiInput v-model="form.name" :invalid="shouldShowError('name')" @blur="markTouched('name')" />
          </UiField>

          <div class="xl:col-span-2">
            <UiField :label="toolPathLabel" :error="shouldShowError('path') ? validationErrors.path : ''" :hint="pathHint">
              <div class="grid gap-2" :class="supportsPathBrowse(form.type) ? 'md:grid-cols-[minmax(0,1fr)_auto]' : ''">
                <UiInput
                  v-model="form.path"
                  :invalid="shouldShowError('path')"
                  :placeholder="pathHint"
                  @blur="markTouched('path')"
                />
                <UiButton v-if="supportsPathBrowse(form.type)" @click="browseToolPath">{{ t('tools.browse') }}</UiButton>
              </div>
            </UiField>
          </div>

          <div v-if="needsRuntimePath" class="xl:col-span-2">
            <UiField :label="runtimeLabel" :hint="runtimeHint">
              <div class="grid gap-2 md:grid-cols-[minmax(0,1fr)_auto]">
                <UiInput v-model="form.runtimePath" @blur="markTouched('runtimePath')" />
                <UiButton @click="browseRuntimePath">{{ t('tools.browse') }}</UiButton>
              </div>
            </UiField>
          </div>

          <UiField v-if="!isUrlTool" :label="t('addTool.cwd')" :hint="t('addTool.cwdInlineHint')">
            <UiInput v-model="form.cwd" />
          </UiField>

          <UiField v-if="!isUrlTool" :label="t('addTool.argsTemplate')">
            <UiInput v-model="form.argsTemplate" :placeholder="isScriptTool ? t('addTool.argsScriptHint') : t('addTool.argsHint')" />
          </UiField>

          <UiField :label="t('addTool.tags')">
            <UiInput v-model="form.tagsText" :placeholder="t('addTool.tagsHint')" />
          </UiField>

          <div class="xl:col-span-2">
            <UiField :label="t('addTool.description')">
              <UiTextarea v-model="form.description" rows="4" />
            </UiField>
          </div>
        </div>

        <div class="flex justify-end gap-2 border-t border-border pt-4">
          <UiButton @click="clearForm">{{ t('addTool.clear') }}</UiButton>
          <UiButton type="submit" variant="primary" :disabled="addingTool">{{ t('addTool.submit') }}</UiButton>
        </div>
      </form>
    </UiPanel>
  </section>
</template>
