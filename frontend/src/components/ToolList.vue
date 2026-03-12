<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { AddToolPayload, ToolItem } from '../types'
import { useI18n } from '../composables/useI18n'

const props = defineProps<{
  tools: ToolItem[]
  loading?: boolean
  updating?: boolean
  deleting?: boolean
  editToolPathSelection?: string
  editToolPythonSelection?: string
}>()

const emit = defineEmits<{
  (e: 'openTool', tool: ToolItem): void
  (e: 'runTool', tool: ToolItem): void
  (e: 'refresh'): void
  (e: 'updateTool', payload: AddToolPayload): void
  (e: 'deleteTools', toolIds: string[]): void
  (e: 'pickEditToolPath', payload: { defaultPath?: string; toolType?: string }): void
  (e: 'pickEditToolPython', payload: { defaultPath?: string }): void
}>()

const keyword = ref('')
const selectedIds = ref<string[]>([])
const editVisible = ref(false)

const editForm = reactive({
  id: '',
  name: '',
  type: 'python',
  path: '',
  python: '',
  cwd: '',
  argsTemplate: '',
  tagsText: '',
  description: '',
})

const { locale, t } = useI18n()

const validationText = computed(() => {
  if (locale.value === 'zh-CN') {
    return {
      invalidId: '工具 ID 格式不合法',
      missingName: '工具名称不能为空',
      missingPath: '工具路径不能为空',
    }
  }

  return {
    invalidId: 'Tool ID format is invalid',
    missingName: 'Tool name is required',
    missingPath: 'Tool path is required',
  }
})


const filteredTools = computed(() => {
  const text = keyword.value.trim().toLowerCase()
  if (!text) {
    return props.tools
  }

  return props.tools.filter((tool) => {
    const hitName = tool.name.toLowerCase().includes(text)
    const hitTag = tool.tags.some((tag) => tag.toLowerCase().includes(text))
    return hitName || hitTag
  })
})

const selectedCount = computed(() => selectedIds.value.length)
const selectedSet = computed(() => new Set(selectedIds.value))
const isPythonEdit = computed(() => editForm.type === 'python')

watch(
  () => props.tools,
  (nextTools) => {
    const liveIds = new Set(nextTools.map((item) => item.id))
    selectedIds.value = selectedIds.value.filter((id) => liveIds.has(id))
  },
)

watch(
  () => props.editToolPathSelection,
  (path) => {
    if (!editVisible.value || !path?.trim()) {
      return
    }

    editForm.path = path
  },
)

watch(
  () => props.editToolPythonSelection,
  (path) => {
    if (!editVisible.value || !path?.trim()) {
      return
    }

    editForm.python = path
  },
)

watch(
  () => editForm.type,
  (nextType) => {
    if (nextType !== 'python') {
      editForm.python = ''
    }
  },
)

function openTool(tool: ToolItem): void {
  emit('openTool', tool)
}

function runTool(tool: ToolItem): void {
  emit('runTool', tool)
}

function toggleSelect(toolId: string, checked: boolean): void {
  const next = new Set(selectedIds.value)
  if (checked) {
    next.add(toolId)
  } else {
    next.delete(toolId)
  }

  selectedIds.value = Array.from(next)
}

function openEdit(tool: ToolItem): void {
  editForm.id = tool.id
  editForm.name = tool.name
  editForm.type = tool.type === 'python' ? 'python' : 'exe'
  editForm.path = tool.path
  editForm.python = tool.python ?? ''
  editForm.cwd = tool.cwd ?? ''
  editForm.argsTemplate = tool.argsTemplate ?? ''
  editForm.tagsText = tool.tags.join(', ')
  editForm.description = tool.description ?? ''
  editVisible.value = true
}

function openEditSelected(): void {
  if (selectedIds.value.length !== 1) {
    return
  }

  const target = props.tools.find((tool) => tool.id === selectedIds.value[0])
  if (!target) {
    return
  }

  openEdit(target)
}

async function deleteSelected(): Promise<void> {
  if (selectedIds.value.length === 0) {
    return
  }

  try {
    await ElMessageBox.confirm(t('tools.confirmDeleteMessage'), t('tools.confirmDeleteTitle'), {
      type: 'warning',
      confirmButtonText: t('tools.deleteSelected'),
      cancelButtonText: t('tools.cancel'),
    })
  } catch {
    return
  }

  emit('deleteTools', [...selectedIds.value])
  selectedIds.value = []
}

function browseEditToolPath(): void {
  emit('pickEditToolPath', {
    defaultPath: editForm.path || editForm.cwd || undefined,
    toolType: editForm.type,
  })
}

function browseEditToolPython(): void {
  emit('pickEditToolPython', {
    defaultPath: editForm.python || editForm.cwd || editForm.path || undefined,
  })
}

function saveEdit(): void {
  const id = editForm.id.trim()
  const name = editForm.name.trim()
  const path = editForm.path.trim()

  if (!/^[a-zA-Z0-9._-]+$/.test(id)) {
    ElMessage.error(validationText.value.invalidId)
    return
  }

  if (!name) {
    ElMessage.error(validationText.value.missingName)
    return
  }

  if (!path) {
    ElMessage.error(validationText.value.missingPath)
    return
  }

  const payload: AddToolPayload = {
    id,
    name,
    type: editForm.type,
    path,
    python: editForm.type === 'python' ? (editForm.python.trim() || undefined) : undefined,
    cwd: editForm.cwd.trim() || undefined,
    argsTemplate: editForm.argsTemplate.trim(),
    tags: editForm.tagsText
      .split(',')
      .map((item) => item.trim())
      .filter((item, index, arr) => item.length > 0 && arr.indexOf(item) === index),
    description: editForm.description.trim() || undefined,
  }

  emit('updateTool', payload)
  editVisible.value = false
}
</script>

<template>
  <section class="tool-list">
    <header class="tool-list-header">
      <div>
        <h2>{{ t('tools.catalog') }}</h2>
        <p>{{ t('tools.items', { filtered: filteredTools.length, total: tools.length }) }}</p>
      </div>

      <div class="header-actions">
        <span class="selected-count">{{ t('tools.selected') }}: {{ selectedCount }}</span>
        <el-button size="small" @click="emit('refresh')">{{ t('python.refresh') }}</el-button>
        <el-button size="small" :disabled="selectedCount !== 1" @click="openEditSelected">{{ t('tools.editSelected') }}</el-button>
        <el-button size="small" type="danger" :disabled="selectedCount === 0" :loading="deleting" @click="deleteSelected">
          {{ t('tools.deleteSelected') }}
        </el-button>
      </div>
    </header>

    <el-input v-model="keyword" clearable :placeholder="t('tools.search')" />

    <div class="list-scroll">
      <el-scrollbar height="100%">
        <div class="tool-rows" v-loading="loading">
          <article
            v-for="tool in filteredTools"
            :key="tool.id"
            class="tool-row"
            role="button"
            tabindex="0"
            @click="openTool(tool)"
            @keydown.enter.prevent="openTool(tool)"
            @keydown.space.prevent="openTool(tool)"
          >
            <div class="row-select" @click.stop>
              <el-checkbox
                :model-value="selectedSet.has(tool.id)"
                @change="(value: string | number | boolean) => toggleSelect(tool.id, Boolean(value))"
              />
            </div>

            <div class="row-main">
              <h3>{{ tool.name }}</h3>
              <p class="tool-id">{{ tool.id }}</p>
              <p class="tool-path" :title="tool.path">{{ tool.path }}</p>
            </div>

            <div class="row-meta">
              <span class="tool-type">{{ tool.type }}</span>
              <span class="tool-status" :class="{ invalid: !tool.valid }">
                {{ tool.valid ? t('tools.ready') : t('tools.invalidPath') }}
              </span>
            </div>

            <div class="row-actions" @click.stop>
              <div class="tool-tags">
                <span v-for="tag in tool.tags" :key="tag" class="tag">{{ tag }}</span>
              </div>

              <div class="action-buttons">
                <el-button size="small" :loading="updating && editForm.id === tool.id" @click.stop="openEdit(tool)">
                  {{ t('tools.edit') }}
                </el-button>
                <el-button
                  type="primary"
                  size="small"
                  :disabled="!tool.valid"
                  @click.stop="runTool(tool)"
                >
                  {{ t('tools.run') }}
                </el-button>
              </div>
            </div>
          </article>

          <el-empty v-if="filteredTools.length === 0 && !loading" :description="t('tools.noMatch')" />
        </div>
      </el-scrollbar>
    </div>

    <el-dialog v-model="editVisible" :title="t('tools.editDialog')" width="620px">
      <el-form label-position="top">
        <el-form-item label="ID">
          <el-input v-model="editForm.id" disabled />
        </el-form-item>

        <el-form-item :label="t('tools.name')">
          <el-input v-model="editForm.name" />
        </el-form-item>

        <el-form-item :label="t('tools.type')">
          <el-segmented v-model="editForm.type" :options="[{ label: 'python', value: 'python' }, { label: 'exe', value: 'exe' }]" />
        </el-form-item>

        <el-form-item :label="t('tools.path')">
          <div class="path-row">
            <el-input v-model="editForm.path" />
            <el-button @click="browseEditToolPath">{{ t('tools.browse') }}</el-button>
          </div>
        </el-form-item>

        <el-form-item v-if="isPythonEdit" :label="t('tools.python')">
          <div class="path-row">
            <el-input v-model="editForm.python" />
            <el-button @click="browseEditToolPython">{{ t('tools.browse') }}</el-button>
          </div>
        </el-form-item>

        <el-form-item :label="t('tools.cwd')">
          <el-input v-model="editForm.cwd" />
        </el-form-item>

        <el-form-item :label="t('tools.argsTemplate')">
          <el-input v-model="editForm.argsTemplate" />
        </el-form-item>

        <el-form-item :label="t('tools.tags')">
          <el-input v-model="editForm.tagsText" />
        </el-form-item>

        <el-form-item :label="t('tools.description')">
          <el-input v-model="editForm.description" type="textarea" :rows="3" />
        </el-form-item>
      </el-form>

      <template #footer>
        <div class="dialog-footer">
          <el-button @click="editVisible = false">{{ t('tools.cancel') }}</el-button>
          <el-button type="primary" :loading="updating" @click="saveEdit">{{ t('tools.save') }}</el-button>
        </div>
      </template>
    </el-dialog>
  </section>
</template>

<style scoped>
.tool-list {
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 12px;
}

.tool-list-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.tool-list-header h2 {
  font-size: 16px;
  font-weight: 600;
  color: var(--vscode-text-primary);
}

.tool-list-header p {
  margin-top: 3px;
  font-size: 12px;
  color: var(--vscode-text-muted);
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  justify-content: flex-end;
}

.selected-count {
  font-size: 12px;
  color: var(--vscode-text-muted);
  margin-right: 4px;
}

.list-scroll {
  flex: 1;
  min-height: 0;
  overflow-x: auto;
}

.tool-rows {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 4px 4px 10px;
  min-width: 1100px;
}

.tool-row {
  border: 1px solid var(--vscode-border-color);
  background: var(--vscode-sidebar-bg);
  border-radius: 3px;
  padding: 10px;
  display: grid;
  grid-template-columns: 32px minmax(0, 1fr) 120px minmax(280px, 320px);
  gap: 12px;
  align-items: center;
  cursor: pointer;
  transition: border-color 0.16s ease;
  width: 100%;
  min-width: 0;
}

.tool-row:hover {
  border-color: var(--vscode-accent-color);
}

.row-select {
  display: flex;
  align-items: center;
  justify-content: center;
}

.row-main h3 {
  font-size: 14px;
  font-weight: 600;
  color: var(--vscode-text-primary);
}

.row-main {
  min-width: 0;
}

.tool-id {
  margin-top: 2px;
  font-family: var(--vscode-font-mono);
  font-size: 11px;
  color: var(--vscode-text-muted);
}

.tool-path {
  margin-top: 8px;
  padding: 4px 6px;
  border: 1px solid var(--vscode-border-color);
  background: var(--vscode-editor-bg);
  color: var(--vscode-text-muted);
  border-radius: 2px;
  font-family: var(--vscode-font-mono);
  font-size: 11px;
  white-space: nowrap;
  text-overflow: ellipsis;
  overflow: hidden;
}

.row-meta {
  display: flex;
  flex-direction: column;
  gap: 7px;
}

.tool-type,
.tool-status {
  display: inline-flex;
  align-items: center;
  width: fit-content;
  border: 1px solid var(--vscode-border-color);
  border-radius: 999px;
  padding: 2px 8px;
  font-size: 11px;
  color: var(--vscode-text-muted);
}

.tool-status {
  border-color: var(--el-color-success);
  color: var(--el-color-success);
}

.tool-status.invalid {
  border-color: var(--el-color-danger);
  color: var(--el-color-danger);
}

.row-actions {
  min-width: 0;
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: center;
  gap: 10px;
}

.action-buttons {
  display: inline-flex;
  gap: 8px;
  justify-self: end;
  flex-shrink: 0;
}

.tool-tags {
  min-width: 0;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  flex-wrap: nowrap;
  gap: 6px;
  overflow: hidden;
}

.tag {
  display: inline-flex;
  align-items: center;
  height: 20px;
  padding: 0 8px;
  border-radius: 999px;
  font-size: 11px;
  color: var(--vscode-text-muted);
  background: var(--vscode-editor-bg);
  border: 1px solid var(--vscode-border-color);
  flex: 0 0 auto;
}

.path-row {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 8px;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
</style>
