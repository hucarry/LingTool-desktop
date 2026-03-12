<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import { Alert as DAlert } from 'vue-devui/alert'
import { Button as DButton } from 'vue-devui/button'
import { Drawer as DDrawer } from 'vue-devui/drawer'
import { Input as DInput } from 'vue-devui/input'
import 'vue-devui/alert/style.css'
import 'vue-devui/button/style.css'
import 'vue-devui/drawer/style.css'
import 'vue-devui/input/style.css'
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
  <d-drawer
    :model-value="visible"
    :title="t('runner.title')"
    width="520px"
    @update:model-value="(v: boolean) => emit('update:visible', v)"
    @close="closeDrawer"
  >
    <template v-if="tool">
      <d-alert v-if="!tool.valid" type="danger" :showIcon="true">
        {{ tool.validationMessage || t('runner.invalidConfig') }}
      </d-alert>

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
          <div class="python-picker">
            <d-input
              :model-value="pythonOverride || ''"
              readonly
              :placeholder="pythonFallbackText"
            />
            <div class="python-picker-actions">
              <d-button size="sm" @click="emit('pickPython')">{{ t('python.browse') }}</d-button>
              <d-button size="sm" @click="emit('update:pythonOverride', tool.python || '')">{{ t('runner.useToolDefault') }}</d-button>
              <d-button size="sm" @click="emit('update:pythonOverride', defaultPythonPath || '')">{{ t('runner.useAppDefault') }}</d-button>
              <d-button size="sm" @click="emit('update:pythonOverride', '')">{{ t('runner.useSystemPython') }}</d-button>
            </div>
          </div>
          <div class="python-tip">{{ t('runner.pythonExample') }}</div>
        </div>

        <div v-for="field in placeholders" :key="field" class="form-item">
          <label>{{ t('runner.argument', { field }) }}</label>
          <d-input v-model="formState[field]" :placeholder="t('runner.enterArgument', { field })" clearable />
        </div>

        <div class="empty-args" v-if="placeholders.length === 0">
          <i class="icon-refresh" style="font-size: 48px; color: var(--vscode-text-muted); opacity: 0.5;"></i>
          <p>{{ t('runner.noDynamicArgs') }}</p>
        </div>
      </div>

      <div class="drawer-actions">
        <d-button @click="closeDrawer">{{ t('runner.cancel') }}</d-button>
        <d-button color="primary" variant="solid" :disabled="!tool.valid" @click="runTool">{{ t('runner.runInTerminal') }}</d-button>
      </div>
    </template>
  </d-drawer>
</template>

<style scoped>
.tool-desc-table {
  margin-top: 14px;
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

.form-item {
  margin-bottom: 18px;
}
.form-item > label {
  display: block;
  font-size: 13px;
  margin-bottom: 8px;
  color: var(--vscode-text-primary);
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

.runner-form {
  margin-top: 18px;
}

.drawer-actions {
  margin-top: 22px;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
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

.python-picker {
  width: 100%;
}

.python-picker-actions {
  margin-top: 10px;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

:deep(.el-drawer__header) {
  margin-bottom: 0;
  padding: 14px 18px;
  border-bottom: 1px solid var(--vscode-border-color);
  color: var(--vscode-text-primary);
  font-weight: 600;
  font-size: 14px;
}

:deep(.el-drawer__body) {
  padding: 14px 18px;
  background: var(--vscode-sidebar-bg);
}

:deep(.el-descriptions__cell) {
  background: var(--vscode-editor-bg);
}
</style>
