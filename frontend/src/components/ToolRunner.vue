<script setup lang="ts">
import { computed, reactive, watch } from 'vue'

import type { ToolItem } from '../types'
import { useI18n } from '../composables/useI18n'

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
  if (props.defaultPythonPath?.trim()) {
    return t('runner.pythonFallbackWithAppDefault')
  }

  return t('runner.pythonFallback')
})

const placeholders = computed(() => {
  const template = props.tool?.argsTemplate ?? ''
  const regex = /\{([a-zA-Z_][a-zA-Z0-9_]*)\}/g
  const found: string[] = []
  const seen = new Set<string>()

  for (const match of template.matchAll(regex)) {
    const key = match[1]
    if (!key) {
      continue
    }

    if (!seen.has(key)) {
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
  if (override) {
    if (override === (props.tool?.python ?? '').trim()) {
      return t('runner.pythonSourceTool')
    }

    if (override === (props.defaultPythonPath ?? '').trim()) {
      return t('runner.pythonSourceApp')
    }

    return t('runner.pythonSourceCustom')
  }

  return t('runner.pythonSourceSystem')
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
    python: props.tool.type === 'python'
      ? (props.pythonOverride?.trim() || undefined)
      : undefined,
  })

  closeDrawer()
}
</script>

<template>
  <teleport to="body">
    <div v-if="visible" class="runner-overlay" @click.self="closeDrawer">
      <aside class="runner-drawer">
        <header class="drawer-header">
          <h2>{{ t('runner.title') }}</h2>
          <button class="icon-button" type="button" @click="closeDrawer">x</button>
        </header>

        <div v-if="tool" class="drawer-body">
          <div class="runner-summary" :class="tool.valid ? 'is-ready' : 'is-danger'">
            <span class="summary-badge">{{ tool.valid ? t('tools.ready') : t('tools.invalidPath') }}</span>
            <div class="summary-copy">
              <strong>{{ runnerSummaryText }}</strong>
              <span>{{ tool.valid ? t('runner.commandPreviewHint') : t('runner.runDisabledHint') }}</span>
            </div>
          </div>

          <div v-if="!tool.valid" class="runner-alert danger">
            {{ tool.validationMessage || t('runner.invalidConfig') }}
          </div>

          <div class="tool-desc-table">
            <div class="desc-row">
              <span class="desc-label">{{ t('runner.name') }}</span>
              <span class="desc-value">{{ tool.name }}</span>
            </div>
            <div class="desc-row">
              <span class="desc-label">{{ t('runner.id') }}</span>
              <span class="desc-value">{{ tool.id }}</span>
            </div>
            <div class="desc-row">
              <span class="desc-label">{{ t('runner.type') }}</span>
              <span class="desc-value">{{ tool.type }}</span>
            </div>
            <div class="desc-row">
              <span class="desc-label">{{ t('runner.path') }}</span>
              <span class="desc-value">{{ tool.path }}</span>
            </div>
            <div class="desc-row">
              <span class="desc-label">{{ t('runner.cwd') }}</span>
              <span class="desc-value">{{ tool.cwd || '-' }}</span>
            </div>
            <div class="desc-row">
              <span class="desc-label">{{ t('runner.python') }}</span>
              <span class="desc-value">{{ tool.python || t('runner.systemPython') }}</span>
            </div>
            <div class="desc-row">
              <span class="desc-label">{{ t('runner.argsTemplate') }}</span>
              <span class="desc-value"><code>{{ tool.argsTemplate || t('runner.none') }}</code></span>
            </div>
            <div class="desc-row">
              <span class="desc-label">{{ t('runner.description') }}</span>
              <span class="desc-value">{{ tool.description || '-' }}</span>
            </div>
          </div>

          <div class="runner-form">
            <div v-if="tool.type === 'python'" class="form-item">
              <label>{{ t('runner.pythonInterpreter') }}</label>
              <div class="field-meta">
                <span class="meta-label">{{ t('runner.pythonSource') }}</span>
                <span class="meta-chip">{{ currentPythonSource }}</span>
              </div>
              <div class="python-picker">
                <input class="field-input" :value="pythonOverride || ''" readonly :placeholder="pythonFallbackText" />
                <div class="python-picker-actions">
                  <button class="action-button" type="button" @click="emit('pickPython')">{{ t('python.browse') }}</button>
                  <button class="action-button" type="button" @click="emit('update:pythonOverride', tool.python || '')">{{ t('runner.useToolDefault') }}</button>
                  <button class="action-button" type="button" :disabled="!canUseAppDefault" @click="emit('update:pythonOverride', defaultPythonPath || '')">
                    {{ t('runner.useAppDefault') }}
                  </button>
                  <button class="action-button" type="button" @click="emit('update:pythonOverride', '')">{{ t('runner.useSystemPython') }}</button>
                </div>
              </div>
              <div class="python-tip">
                {{ canUseAppDefault ? t('runner.pythonExample') : t('runner.appDefaultUnavailable') }}
              </div>
            </div>

            <div v-if="placeholders.length > 0" class="field-meta">
              <span class="meta-label">{{ t('runner.summaryArgs', { filled: filledPlaceholderCount, total: placeholders.length }) }}</span>
            </div>
            <div v-for="field in placeholders" :key="field" class="form-item">
              <label>{{ t('runner.argument', { field }) }}</label>
              <input v-model="formState[field]" class="field-input" type="text" :placeholder="t('runner.enterArgument', { field })" />
            </div>

            <div class="command-preview">
              <label>{{ t('runner.commandPreview') }}</label>
              <code class="command-preview-code">{{ commandPreview }}</code>
            </div>

            <div v-if="placeholders.length === 0" class="empty-args">
              <span class="empty-mark">RUN</span>
              <p>{{ t('runner.noDynamicArgs') }}</p>
            </div>
          </div>

          <div class="drawer-actions">
            <span v-if="!tool.valid" class="action-note">{{ t('runner.runDisabledHint') }}</span>
            <button class="action-button" type="button" @click="closeDrawer">{{ t('runner.cancel') }}</button>
            <button class="action-button primary" type="button" :disabled="!tool.valid" @click="runTool">
              {{ t('runner.runInTerminal') }}
            </button>
          </div>
        </div>
      </aside>
    </div>
  </teleport>
</template>

<style scoped>
.runner-overlay {
  position: fixed;
  inset: 0;
  z-index: 4200;
  display: flex;
  justify-content: flex-end;
  background: var(--overlay-backdrop);
  backdrop-filter: blur(2px);
}

.runner-drawer {
  width: min(520px, 100vw);
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--vscode-sidebar-bg);
  border-left: 1px solid var(--vscode-border-color);
  box-shadow: var(--shadow-drawer);
}

.drawer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 18px;
  border-bottom: 1px solid var(--vscode-border-color);
}

.drawer-header h2 {
  font-size: 14px;
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
  font-size: 14px;
  line-height: 1;
}

.icon-button:hover {
  background: var(--vscode-hover-bg);
  color: var(--vscode-text-primary);
}

.drawer-body {
  flex: 1;
  overflow: auto;
  padding: 14px 18px;
}

.runner-summary {
  display: flex;
  gap: 12px;
  align-items: flex-start;
  margin-bottom: 14px;
  padding: 12px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 8px;
  background: var(--surface-muted);
}

.runner-summary.is-ready {
  border-color: color-mix(in srgb, var(--status-success) 35%, var(--vscode-border-color));
  background: var(--status-success-soft);
}

.runner-summary.is-danger {
  border-color: color-mix(in srgb, var(--status-danger) 35%, var(--vscode-border-color));
}

.summary-badge {
  display: inline-flex;
  align-items: center;
  min-height: 24px;
  padding: 0 10px;
  border-radius: 999px;
  background: var(--vscode-sidebar-bg);
  color: var(--vscode-text-primary);
  font-size: 11px;
  font-weight: 700;
  white-space: nowrap;
}

.summary-copy {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.summary-copy strong {
  font-size: 13px;
  color: var(--vscode-text-primary);
}

.summary-copy span {
  font-size: 12px;
  line-height: 1.45;
  color: var(--vscode-text-muted);
}

.runner-alert {
  margin-bottom: 14px;
  padding: 10px 12px;
  border-radius: 6px;
  border: 1px solid var(--vscode-border-color);
}

.runner-alert.danger {
  border-color: color-mix(in srgb, var(--status-danger) 45%, var(--vscode-border-color));
  background: color-mix(in srgb, var(--status-danger) 12%, transparent);
  color: var(--status-danger);
}

.tool-desc-table {
  border-radius: 3px;
  border: 1px solid var(--vscode-border-color);
  overflow: hidden;
  font-size: 13px;
}

.desc-row {
  display: flex;
  border-bottom: 1px solid var(--vscode-border-color);
}

.desc-row:last-child {
  border-bottom: none;
}

.desc-label {
  width: 130px;
  padding: 8px 12px;
  background: var(--vscode-sidebar-bg);
  border-right: 1px solid var(--vscode-border-color);
  font-weight: 600;
  color: var(--vscode-text-muted);
}

.desc-value {
  flex: 1;
  padding: 8px 12px;
  color: var(--vscode-text-primary);
  word-break: break-all;
}

.runner-form {
  margin-top: 18px;
}

.form-item {
  margin-bottom: 18px;
}

.form-item > label {
  display: block;
  font-size: 13px;
  margin-bottom: 8px;
  color: var(--vscode-text-primary);
}

.field-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  margin-bottom: 8px;
}

.meta-label {
  font-size: 12px;
  color: var(--vscode-text-muted);
}

.meta-chip {
  display: inline-flex;
  align-items: center;
  min-height: 24px;
  padding: 0 10px;
  border-radius: 999px;
  background: var(--accent-soft);
  color: var(--vscode-accent-color);
  font-size: 11px;
  font-weight: 600;
}

.field-input {
  width: 100%;
  height: var(--control-height);
  border: 1px solid var(--vscode-border-color);
  border-radius: var(--control-radius);
  background: var(--vscode-editor-bg);
  color: var(--vscode-text-primary);
  padding: 0 var(--control-padding-inline);
}

.command-preview {
  margin-top: 10px;
  padding: 12px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 8px;
  background: var(--surface-muted);
}

.command-preview label {
  display: block;
  margin-bottom: 8px;
  font-size: 12px;
  color: var(--vscode-text-muted);
}

.command-preview-code {
  display: block;
  width: 100%;
  white-space: pre-wrap;
  word-break: break-word;
  line-height: 1.5;
}

.empty-args {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px 0;
  color: var(--vscode-text-muted);
  gap: 12px;
}

.empty-mark {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 56px;
  height: 56px;
  border: 1px solid var(--border-muted);
  border-radius: 999px;
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.14em;
  opacity: 0.65;
}

.drawer-actions,
.python-picker-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.drawer-actions {
  margin-top: 22px;
  justify-content: flex-end;
  align-items: center;
}

.action-button {
  height: var(--control-height);
  border: 1px solid var(--vscode-border-color);
  border-radius: var(--control-radius);
  background: var(--vscode-sidebar-bg);
  color: var(--vscode-text-primary);
  padding: 0 var(--control-padding-inline);
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

code {
  font-family: var(--vscode-font-mono);
  background: var(--vscode-editor-bg);
  border: 1px solid var(--vscode-border-color);
  padding: 3px 7px;
  border-radius: 2px;
  font-size: 12px;
}

.python-tip {
  margin-top: 8px;
  color: var(--vscode-text-muted);
  font-size: 12px;
}

.action-note {
  margin-right: auto;
  font-size: 12px;
  color: var(--status-danger);
}
</style>
