<script setup lang="ts">
import { watch } from 'vue'
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

const toolsStore = useToolsStore()
const { addToolPathSelection, addToolPythonSelection, lastAddedToolId, addingTool } = storeToRefs(toolsStore)
const { t } = useI18n()
const notify = useNotify()
const {
  form,
  submitAttempted,
  isPythonTool,
  validationErrors,
  firstValidationMessage,
  pathHint,
  pythonHint,
  resetForm,
  applyPathSuggestion,
  applyPythonSuggestion,
  createPayload,
  markTouched,
  shouldShowError,
} = useToolForm('python')

watch(addToolPathSelection, (path) => {
  if (path?.trim()) {
    applyPathSuggestion(path, true)
  }
})

watch(addToolPythonSelection, (path) => {
  if (path?.trim()) {
    applyPythonSuggestion(path)
  }
})

watch(lastAddedToolId, (toolId) => {
  if (toolId) {
    resetForm()
  }
})

function browseToolPath(): void {
  const defaultPath = form.path.trim() || form.cwd.trim() || undefined
  toolsStore.pickAddToolPath(defaultPath, form.type)
}

function browsePythonPath(): void {
  const defaultPath = form.python.trim() || form.cwd.trim() || form.path.trim() || undefined
  toolsStore.pickAddToolPython(defaultPath)
}

function submit(): void {
  submitAttempted.value = true
  markTouched('id')
  markTouched('name')
  markTouched('path')
  markTouched('python')

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
              <option value="exe">EXE</option>
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
            <UiField :label="t('addTool.toolPath')" :error="shouldShowError('path') ? validationErrors.path : ''" :hint="pathHint">
              <div class="grid gap-2 md:grid-cols-[minmax(0,1fr)_auto]">
                <UiInput
                  v-model="form.path"
                  :invalid="shouldShowError('path')"
                  :placeholder="isPythonTool ? t('addTool.pyHint') : t('addTool.exeHint')"
                  @blur="markTouched('path')"
                />
                <UiButton @click="browseToolPath">{{ t('tools.browse') }}</UiButton>
              </div>
            </UiField>
          </div>

          <div v-if="isPythonTool" class="xl:col-span-2">
            <UiField :label="t('addTool.pythonPath')" :hint="pythonHint">
              <div class="grid gap-2 md:grid-cols-[minmax(0,1fr)_auto]">
                <UiInput v-model="form.python" @blur="markTouched('python')" />
                <UiButton @click="browsePythonPath">{{ t('tools.browse') }}</UiButton>
              </div>
            </UiField>
          </div>

          <UiField :label="t('addTool.cwd')" :hint="t('addTool.cwdInlineHint')">
            <UiInput v-model="form.cwd" />
          </UiField>

          <UiField :label="t('addTool.argsTemplate')">
            <UiInput v-model="form.argsTemplate" />
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
