<script setup lang="ts">
import { computed, reactive, watch } from 'vue'

import UiBadge from './ui/UiBadge.vue'
import UiButton from './ui/UiButton.vue'
import UiDrawer from './ui/UiDrawer.vue'
import UiField from './ui/UiField.vue'
import UiInput from './ui/UiInput.vue'
import UiOverlay from './ui/UiOverlay.vue'
import UiSelect from './ui/UiSelect.vue'
import { useI18n } from '../composables/useI18n'
import type { ToolItem } from '../types'
import { getDefaultRuntimeCommand, getDefaultRuntimeForTool, isScriptToolType, isUrlToolType } from '../utils/toolTypes'
import { buildArgumentTokens, buildLegacyArgsTemplate, extractArgumentFields } from '../utils/argsSpec'

const props = defineProps<{
  visible: boolean
  tool: ToolItem | null
  runtimeOverride?: string
  defaultPythonPath?: string
  defaultNodePath?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'update:runtimeOverride', value: string): void
  (e: 'pickRuntime'): void
  (e: 'run', payload: { toolId: string; args: Record<string, string>; runtimePath?: string }): void
  (e: 'openUrl', toolId: string): void
}>()

const { t } = useI18n()
const formState = reactive<Record<string, string>>({})

const isScriptTool = computed(() => (props.tool ? isScriptToolType(props.tool.type) : false))
const isUrlTool = computed(() => (props.tool ? isUrlToolType(props.tool.type) : false))
const appDefaultRuntimePath = computed(() => {
  if (!props.tool) {
    return ''
  }

  if (props.tool.type === 'python') {
    return props.defaultPythonPath?.trim() ?? ''
  }

  if (props.tool.type === 'node') {
    return props.defaultNodePath?.trim() ?? ''
  }

  return ''
})
const canUseAppDefault = computed(() => Boolean(appDefaultRuntimePath.value))

const runtimeFallbackText = computed(() => {
  if (!props.tool || !isScriptTool.value) {
    return ''
  }

  if (props.tool.type === 'python') {
    return appDefaultRuntimePath.value
      ? t('runner.pythonFallbackWithAppDefault')
      : t('runner.pythonFallback')
  }

  return appDefaultRuntimePath.value
    ? t('runner.nodeFallbackWithAppDefault')
    : t('runner.nodeFallback')
})

const runtimeFieldLabel = computed(() => {
  if (!props.tool || !isScriptTool.value) {
    return ''
  }

  return props.tool.type === 'python'
    ? t('runner.pythonInterpreter')
    : t('runner.nodeRuntime')
})

const argumentFields = computed(() => {
  if (!props.tool) {
    return []
  }

  return extractArgumentFields(props.tool)
})

const filledPlaceholderCount = computed(() => {
  return argumentFields.value.filter((field) => {
    const value = formState[field.name]?.trim() ?? ''
    if (field.kind === 'flag') {
      return value === 'true'
    }

    return value.length > 0
  }).length
})

const currentRuntimeSource = computed(() => {
  if (!props.tool || !isScriptTool.value) {
    return null
  }

  const override = props.runtimeOverride?.trim() ?? ''
  if (!override) {
    return props.tool.type === 'python'
      ? t('runner.pythonSourceSystem')
      : t('runner.nodeSourceSystem')
  }

  if (override === (props.tool.runtimePath ?? '').trim()) {
    return props.tool.type === 'python'
      ? t('runner.pythonSourceTool')
      : t('runner.nodeSourceTool')
  }

  if (override === appDefaultRuntimePath.value) {
    return props.tool.type === 'python'
      ? t('runner.pythonSourceApp')
      : t('runner.nodeSourceApp')
  }

  return props.tool.type === 'python'
    ? t('runner.pythonSourceCustom')
    : t('runner.nodeSourceCustom')
})

const runnerSummaryText = computed(() => {
  if (!props.tool) {
    return ''
  }

  if (!props.tool.valid) {
    return t('runner.summaryBlocked')
  }

  if (isUrlTool.value) {
    return t('runner.summaryUrlReady')
  }

  if (argumentFields.value.length === 0) {
    return t('runner.summaryReady')
  }

  return t('runner.summaryArgs', {
    filled: filledPlaceholderCount.value,
    total: argumentFields.value.length,
  })
})

const commandPreview = computed(() => {
  if (!props.tool) {
    return ''
  }

  const renderedTokens = buildArgumentTokens(props.tool, { ...formState })
  const renderedArgs = renderedTokens.join(' ')

  if (isUrlTool.value) {
    return props.tool.path
  }

  if (isScriptTool.value) {
    const runtime = props.runtimeOverride?.trim()
      || getDefaultRuntimeForTool(props.tool, {
        defaultPythonPath: props.defaultPythonPath,
        defaultNodePath: props.defaultNodePath,
      })
      || getDefaultRuntimeCommand(props.tool.type)

    return `${runtime} ${props.tool.path} ${renderedArgs}`.trim()
  }

  if (!props.tool.argsTemplate.trim()) {
    return props.tool.path
  }

  return `${props.tool.path} ${renderedArgs}`.trim()
})

const infoRows = computed(() => {
  if (!props.tool) {
    return []
  }

  const rows: Array<[string, string]> = [
    [t('runner.name'), props.tool.name],
    [t('runner.id'), props.tool.id],
    [t('runner.type'), props.tool.type],
    [t('runner.path'), props.tool.path],
  ]

  if (!isUrlTool.value) {
    rows.push([t('runner.cwd'), props.tool.cwd || '-'])
  }

  if (isScriptTool.value) {
    rows.push([runtimeFieldLabel.value, props.tool.runtimePath || t(props.tool.type === 'python' ? 'runner.systemPython' : 'runner.systemNode')])
  }

  if (!isUrlTool.value) {
    rows.push([
      t('runner.argsTemplate'),
      props.tool.argsTemplate || buildLegacyArgsTemplate(props.tool.argsSpec, '') || t('runner.none'),
    ])
  }

  rows.push([t('runner.description'), props.tool.description || '-'])
  return rows
})

watch(
  () => [props.tool?.id, props.visible, argumentFields.value.map((field) => field.name).join('|')],
  () => {
    Object.keys(formState).forEach((key) => delete formState[key])
    argumentFields.value.forEach((field) => {
      formState[field.name] = field.defaultValue
    })

    if (props.tool && isScriptToolType(props.tool.type)) {
      emit('update:runtimeOverride', getDefaultRuntimeForTool(props.tool, {
        defaultPythonPath: props.defaultPythonPath,
        defaultNodePath: props.defaultNodePath,
      }))
    } else {
      emit('update:runtimeOverride', '')
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

  if (isUrlTool.value) {
    emit('openUrl', props.tool.id)
    closeDrawer()
    return
  }

  emit('run', {
    toolId: props.tool.id,
    args: { ...formState },
    runtimePath: isScriptTool.value ? (props.runtimeOverride?.trim() || undefined) : undefined,
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
                {{ tool.valid ? (isUrlTool ? t('runner.openLinkHint') : t('runner.commandPreviewHint')) : t('runner.runDisabledHint') }}
              </span>
            </div>
          </div>

          <div v-if="!tool.valid" class="mb-4 rounded-field border border-danger/40 bg-danger-soft p-3 text-sm text-danger">
            {{ tool.validationMessage || t('runner.invalidConfig') }}
          </div>

          <div class="overflow-hidden rounded-panel border border-border text-sm">
            <div
              v-for="row in infoRows"
              :key="row[0]"
              class="grid grid-cols-[130px_minmax(0,1fr)] border-b border-border last:border-b-0"
            >
              <span class="bg-sidebar px-3 py-2 text-xs font-semibold text-muted">{{ row[0] }}</span>
              <span class="break-all px-3 py-2 text-sm text-foreground">{{ row[1] }}</span>
            </div>
          </div>

          <div class="mt-5 space-y-4">
            <div v-if="isScriptTool" class="space-y-3">
              <UiField :label="runtimeFieldLabel" :hint="props.tool?.type === 'python' ? t('runner.pythonExample') : t('runner.nodeExample')">
                <div class="mb-2 flex flex-wrap items-center gap-2">
                  <span class="text-xs text-muted">{{ t('runner.runtimeSource') }}</span>
                  <UiBadge tone="accent">{{ currentRuntimeSource }}</UiBadge>
                </div>
                <UiInput :model-value="runtimeOverride || ''" readonly :placeholder="runtimeFallbackText" />
              </UiField>

              <div class="flex flex-wrap gap-2">
                <UiButton @click="emit('pickRuntime')">{{ t('python.browse') }}</UiButton>
                <UiButton @click="emit('update:runtimeOverride', tool.runtimePath || '')">{{ t('runner.useToolDefault') }}</UiButton>
                <UiButton :disabled="!canUseAppDefault" @click="emit('update:runtimeOverride', appDefaultRuntimePath || '')">
                  {{ t('runner.useAppDefault') }}
                </UiButton>
                <UiButton @click="emit('update:runtimeOverride', '')">
                  {{ tool.type === 'python' ? t('runner.useSystemPython') : t('runner.useSystemNode') }}
                </UiButton>
              </div>
            </div>

            <div v-if="argumentFields.length > 0" class="flex items-center gap-2">
              <span class="text-xs text-muted">{{ t('runner.summaryArgs', { filled: filledPlaceholderCount, total: argumentFields.length }) }}</span>
            </div>

            <UiField
              v-for="field in argumentFields"
              :key="field.name"
              :label="field.label || t('runner.argument', { field: field.name })"
              :hint="field.description"
            >
              <UiSelect v-if="field.kind === 'select' || field.kind === 'flag'" v-model="formState[field.name]">
                <option v-for="option in field.options" :key="option.value" :value="option.value">
                  {{ option.label }}
                </option>
              </UiSelect>
              <UiInput
                v-else
                v-model="formState[field.name]"
                :type="field.kind === 'number' ? 'number' : field.kind === 'secret' ? 'password' : 'text'"
                :placeholder="field.placeholder || t('runner.enterArgument', { field: field.name })"
              />
            </UiField>

            <UiField :label="isUrlTool ? t('runner.linkPreview') : t('runner.commandPreview')">
              <code class="block rounded-field border border-border bg-editor px-3 py-3 font-mono text-xs leading-6 text-foreground">
                {{ commandPreview }}
              </code>
            </UiField>

            <div v-if="argumentFields.length === 0 && !isUrlTool" class="flex flex-col items-center gap-3 py-8 text-muted">
              <span class="flex h-14 w-14 items-center justify-center rounded-full border border-border text-xs font-bold tracking-[0.14em]">
                RUN
              </span>
              <p class="text-sm">{{ t('runner.noDynamicArgs') }}</p>
            </div>
          </div>

          <div class="mt-6 flex flex-wrap items-center justify-end gap-2 border-t border-border pt-4">
            <span v-if="!tool.valid" class="mr-auto text-xs text-danger">{{ t('runner.runDisabledHint') }}</span>
            <UiButton @click="closeDrawer">{{ t('runner.cancel') }}</UiButton>
            <UiButton variant="primary" :disabled="!tool.valid" @click="runTool">
              {{ isUrlTool ? t('runner.openLink') : t('runner.runInTerminal') }}
            </UiButton>
          </div>
        </div>
      </UiDrawer>
    </UiOverlay>
  </teleport>
</template>
