<script setup lang="ts">
import { computed, reactive, watch } from 'vue'

import UiBadge from './ui/UiBadge.vue'
import UiButton from './ui/UiButton.vue'
import UiDrawer from './ui/UiDrawer.vue'
import UiField from './ui/UiField.vue'
import UiInput from './ui/UiInput.vue'
import UiOverlay from './ui/UiOverlay.vue'
import { useI18n } from '../composables/useI18n'
import type { ToolItem } from '../types'

const props = defineProps<{
  visible: boolean
  tool: ToolItem | null
  pythonOverride?: string
  defaultPythonPath?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'update:pythonOverride', value: string): void
  (e: 'pickPython'): void
  (e: 'run', payload: { toolId: string; args: Record<string, string>; python?: string }): void
}>()

const { t } = useI18n()
const formState = reactive<Record<string, string>>({})
const canUseAppDefault = computed(() => Boolean(props.defaultPythonPath?.trim()))

const pythonFallbackText = computed(() => {
  return props.defaultPythonPath?.trim()
    ? t('runner.pythonFallbackWithAppDefault')
    : t('runner.pythonFallback')
})

const placeholders = computed(() => {
  const template = props.tool?.argsTemplate ?? ''
  const regex = /\{([a-zA-Z_][a-zA-Z0-9_]*)\}/g
  const found: string[] = []
  const seen = new Set<string>()

  for (const match of template.matchAll(regex)) {
    const key = match[1]
    if (key && !seen.has(key)) {
      seen.add(key)
      found.push(key)
    }
  }

  return found
})

const filledPlaceholderCount = computed(() => {
  return placeholders.value.filter((field) => formState[field]?.trim()).length
})

const currentPythonSource = computed(() => {
  if (props.tool?.type !== 'python') {
    return null
  }

  const override = props.pythonOverride?.trim() ?? ''
  if (!override) {
    return t('runner.pythonSourceSystem')
  }

  if (override === (props.tool.python ?? '').trim()) {
    return t('runner.pythonSourceTool')
  }

  if (override === (props.defaultPythonPath ?? '').trim()) {
    return t('runner.pythonSourceApp')
  }

  return t('runner.pythonSourceCustom')
})

const runnerSummaryText = computed(() => {
  if (!props.tool) {
    return ''
  }

  if (!props.tool.valid) {
    return t('runner.summaryBlocked')
  }

  if (placeholders.value.length === 0) {
    return t('runner.summaryReady')
  }

  return t('runner.summaryArgs', {
    filled: filledPlaceholderCount.value,
    total: placeholders.value.length,
  })
})

const commandPreview = computed(() => {
  if (!props.tool) {
    return ''
  }

  if (!props.tool.argsTemplate.trim()) {
    return props.tool.path
  }

  const renderedArgs = props.tool.argsTemplate.replace(/\{([a-zA-Z_][a-zA-Z0-9_]*)\}/g, (_, key: string) => {
    const value = formState[key]?.trim()
    return value ? value : `{${key}}`
  })

  return `${props.tool.path} ${renderedArgs}`.trim()
})

watch(
  () => [props.tool?.id, props.visible, placeholders.value.join('|')],
  () => {
    Object.keys(formState).forEach((key) => delete formState[key])
    placeholders.value.forEach((key) => {
      formState[key] = ''
    })

    if (props.tool?.type === 'python') {
      emit('update:pythonOverride', props.tool.python ?? props.defaultPythonPath ?? '')
    } else {
      emit('update:pythonOverride', '')
    }
  },
  { immediate: true },
)

function closeDrawer(): void {
  emit('update:visible', false)
}

function runTool(): void {
  if (!props.tool) {
    return
  }

  emit('run', {
    toolId: props.tool.id,
    args: { ...formState },
    python: props.tool.type === 'python' ? (props.pythonOverride?.trim() || undefined) : undefined,
  })

  closeDrawer()
}
</script>

<template>
  <teleport to="body">
    <UiOverlay v-if="visible" align="end" @click.self="closeDrawer">
      <UiDrawer>
        <header class="flex items-center justify-between border-b border-border px-4 py-3">
          <h2 class="text-sm font-semibold text-foreground">{{ t('runner.title') }}</h2>
          <UiButton variant="ghost" size="sm" @click="closeDrawer">x</UiButton>
        </header>

        <div v-if="tool" class="flex h-full flex-col overflow-auto px-4 py-4">
          <div class="mb-4 flex gap-3 rounded-panel border border-border bg-soft p-3" :class="tool.valid ? 'bg-success-soft' : 'bg-danger-soft'">
            <UiBadge :tone="tool.valid ? 'success' : 'danger'">{{ tool.valid ? t('tools.ready') : t('tools.invalidPath') }}</UiBadge>
            <div class="space-y-1">
              <strong class="block text-sm text-foreground">{{ runnerSummaryText }}</strong>
              <span class="text-xs leading-5 text-muted">
                {{ tool.valid ? t('runner.commandPreviewHint') : t('runner.runDisabledHint') }}
              </span>
            </div>
          </div>

          <div v-if="!tool.valid" class="mb-4 rounded-field border border-danger/40 bg-danger-soft p-3 text-sm text-danger">
            {{ tool.validationMessage || t('runner.invalidConfig') }}
          </div>

          <div class="overflow-hidden rounded-panel border border-border text-sm">
            <div
              v-for="row in [
                [t('runner.name'), tool.name],
                [t('runner.id'), tool.id],
                [t('runner.type'), tool.type],
                [t('runner.path'), tool.path],
                [t('runner.cwd'), tool.cwd || '-'],
                [t('runner.python'), tool.python || t('runner.systemPython')],
                [t('runner.argsTemplate'), tool.argsTemplate || t('runner.none')],
                [t('runner.description'), tool.description || '-'],
              ]"
              :key="row[0]"
              class="grid grid-cols-[130px_minmax(0,1fr)] border-b border-border last:border-b-0"
            >
              <span class="bg-sidebar px-3 py-2 text-xs font-semibold text-muted">{{ row[0] }}</span>
              <span class="break-all px-3 py-2 text-sm text-foreground">{{ row[1] }}</span>
            </div>
          </div>

          <div class="mt-5 space-y-4">
            <div v-if="tool.type === 'python'" class="space-y-3">
              <UiField :label="t('runner.pythonInterpreter')" :hint="canUseAppDefault ? t('runner.pythonExample') : t('runner.appDefaultUnavailable')">
                <div class="mb-2 flex flex-wrap items-center gap-2">
                  <span class="text-xs text-muted">{{ t('runner.pythonSource') }}</span>
                  <UiBadge tone="accent">{{ currentPythonSource }}</UiBadge>
                </div>
                <UiInput :model-value="pythonOverride || ''" readonly :placeholder="pythonFallbackText" />
              </UiField>

              <div class="flex flex-wrap gap-2">
                <UiButton @click="emit('pickPython')">{{ t('python.browse') }}</UiButton>
                <UiButton @click="emit('update:pythonOverride', tool.python || '')">{{ t('runner.useToolDefault') }}</UiButton>
                <UiButton :disabled="!canUseAppDefault" @click="emit('update:pythonOverride', defaultPythonPath || '')">
                  {{ t('runner.useAppDefault') }}
                </UiButton>
                <UiButton @click="emit('update:pythonOverride', '')">{{ t('runner.useSystemPython') }}</UiButton>
              </div>
            </div>

            <div v-if="placeholders.length > 0" class="flex items-center gap-2">
              <span class="text-xs text-muted">{{ t('runner.summaryArgs', { filled: filledPlaceholderCount, total: placeholders.length }) }}</span>
            </div>

            <UiField v-for="field in placeholders" :key="field" :label="t('runner.argument', { field })">
              <UiInput v-model="formState[field]" :placeholder="t('runner.enterArgument', { field })" />
            </UiField>

            <UiField :label="t('runner.commandPreview')">
              <code class="block rounded-field border border-border bg-editor px-3 py-3 font-mono text-xs leading-6 text-foreground">
                {{ commandPreview }}
              </code>
            </UiField>

            <div v-if="placeholders.length === 0" class="flex flex-col items-center gap-3 py-8 text-muted">
              <span class="flex h-14 w-14 items-center justify-center rounded-full border border-border text-xs font-bold tracking-[0.14em]">
                RUN
              </span>
              <p class="text-sm">{{ t('runner.noDynamicArgs') }}</p>
            </div>
          </div>

          <div class="mt-6 flex flex-wrap items-center justify-end gap-2 border-t border-border pt-4">
            <span v-if="!tool.valid" class="mr-auto text-xs text-danger">{{ t('runner.runDisabledHint') }}</span>
            <UiButton @click="closeDrawer">{{ t('runner.cancel') }}</UiButton>
            <UiButton variant="primary" :disabled="!tool.valid" @click="runTool">{{ t('runner.runInTerminal') }}</UiButton>
          </div>
        </div>
      </UiDrawer>
    </UiOverlay>
  </teleport>
</template>
