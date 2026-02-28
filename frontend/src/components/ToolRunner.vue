<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import type { ToolItem } from '../types'

const props = defineProps<{
  visible: boolean
  tool: ToolItem | null
  pythonOverride?: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'update:pythonOverride', value: string): void
  (e: 'pickPython'): void
  (e: 'run', payload: { toolId: string; args: Record<string, string>; python?: string }): void
}>()

const formState = reactive<Record<string, string>>({})

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
      emit('update:pythonOverride', props.tool.python ?? '')
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
  <el-drawer
    :model-value="visible"
    title="Tool Details"
    size="520px"
    destroy-on-close
    @close="closeDrawer"
    @update:model-value="(value: boolean) => emit('update:visible', value)"
  >
    <template v-if="tool">
      <el-alert v-if="!tool.valid" type="error" :closable="false" show-icon>
        <template #title>
          {{ tool.validationMessage || 'Invalid tool configuration. Running is disabled.' }}
        </template>
      </el-alert>

      <el-descriptions :column="1" border class="tool-desc">
        <el-descriptions-item label="Name">{{ tool.name }}</el-descriptions-item>
        <el-descriptions-item label="ID">{{ tool.id }}</el-descriptions-item>
        <el-descriptions-item label="Type">{{ tool.type }}</el-descriptions-item>
        <el-descriptions-item label="Path">{{ tool.path }}</el-descriptions-item>
        <el-descriptions-item label="Working Directory">{{ tool.cwd || '-' }}</el-descriptions-item>
        <el-descriptions-item label="Python">{{ tool.python || 'system python' }}</el-descriptions-item>
        <el-descriptions-item label="Args Template">
          <code>{{ tool.argsTemplate || '(none)' }}</code>
        </el-descriptions-item>
        <el-descriptions-item label="Description">{{ tool.description || '-' }}</el-descriptions-item>
      </el-descriptions>

      <el-form label-position="top" class="runner-form">
        <el-form-item v-if="tool.type === 'python'" label="Python Interpreter">
          <div class="python-picker">
            <el-input
              :model-value="pythonOverride || ''"
              readonly
              placeholder="Fallback order: custom override -> tools.json python -> system python"
            />
            <div class="python-picker-actions">
              <el-button @click="emit('pickPython')">Browse...</el-button>
              <el-button @click="emit('update:pythonOverride', tool.python || '')">Use Tool Default</el-button>
              <el-button @click="emit('update:pythonOverride', '')">Use System Python</el-button>
            </div>
          </div>
          <div class="python-tip">Example: C:\\project\\.venv\\Scripts\\python.exe</div>
        </el-form-item>

        <el-form-item v-for="field in placeholders" :key="field" :label="`Argument ${field}`">
          <el-input v-model="formState[field]" :placeholder="`Enter ${field}`" clearable />
        </el-form-item>

        <el-empty
          v-if="placeholders.length === 0"
          description="This tool has no dynamic arguments. You can run it directly."
          :image-size="92"
        />
      </el-form>

      <div class="drawer-actions">
        <el-button @click="closeDrawer">Cancel</el-button>
        <el-button type="primary" :disabled="!tool.valid" @click="runTool">Run In Terminal</el-button>
      </div>
    </template>
  </el-drawer>
</template>

<style scoped>
.tool-desc {
  margin-top: 14px;
  border-radius: 3px;
  overflow: hidden;
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
  background: #1e1e1e;
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
  background: #252526;
}
</style>
