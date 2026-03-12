<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

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
    const res = window.confirm(t('tools.confirmDeleteMessage'))
    if (!res) {
      return
    }
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
    alert(validationText.value.invalidId)
    return
  }

  if (!name) {
    alert(validationText.value.missingName)
    return
  }

  if (!path) {
    alert(validationText.value.missingPath)
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
        <d-button size="sm" @click="emit('refresh')">{{ t('python.refresh') }}</d-button>
        <d-button size="sm" :disabled="selectedCount !== 1" @click="openEditSelected">{{ t('tools.editSelected') }}</d-button>
        <d-button size="sm" color="danger" :disabled="selectedCount === 0" :loading="deleting" @click="deleteSelected">
          {{ t('tools.deleteSelected') }}
        </d-button>
      </div>
    </header>

    <d-search v-model="keyword" :placeholder="t('tools.search')" :is-keyup-search="true" :delay="200" icon-position="left" />

    <div class="list-scroll">
      <d-scrollbar height="100%">
        <d-row :gutter="16" class="tool-rows" v-loading="loading">
          <d-col
            v-for="tool in filteredTools"
            :key="tool.id"
            :span="6"
          >
            <d-card
              class="tool-card"
              shadow="hover"
              @click="openTool(tool)"
            >
              <template #title>
                <div class="card-header">
                  <div class="card-title-group">
                    <d-checkbox
                      :model-value="selectedSet.has(tool.id)"
                      @click.stop
                      @change="(value: string | number | boolean) => toggleSelect(tool.id, Boolean(value))"
                    />
                    <h3>{{ tool.name }}</h3>
                  </div>
                  <div class="card-badges">
                    <d-tag size="sm">{{ tool.type }}</d-tag>
                    <d-badge
                      :status="tool.valid ? 'success' : 'danger'"
                      :count="tool.valid ? t('tools.ready') : t('tools.invalidPath')"
                    />
                  </div>
                </div>
              </template>

              <div class="card-body">
                <p class="tool-id">{{ tool.id }}</p>
                <d-tooltip :content="tool.path" position="top">
                  <p class="tool-path">{{ tool.path }}</p>
                </d-tooltip>
                
                <div class="tool-tags" v-if="tool.tags && tool.tags.length">
                  <d-tag v-for="tag in tool.tags" :key="tag" size="sm" color="#7693f5">{{ tag }}</d-tag>
                </div>
              </div>

              <template #actions>
                <div class="card-footer" @click.stop>
                  <div class="action-buttons">
                    <d-button size="sm" :loading="updating && editForm.id === tool.id" @click.stop="openEdit(tool)">
                      {{ t('tools.edit') }}
                    </d-button>
                    <d-button
                      color="primary"
                      size="sm"
                      :disabled="!tool.valid"
                      @click.stop="runTool(tool)"
                    >
                      {{ t('tools.run') }}
                    </d-button>
                  </div>
                </div>
              </template>
            </d-card>
          </d-col>

          <d-col :span="24" v-if="filteredTools.length === 0 && !loading">
            <d-empty :description="t('tools.noMatch')" />
          </d-col>
        </d-row>
      </d-scrollbar>
    </div>

    <d-modal v-model="editVisible" :title="t('tools.editDialog')" append-to-body>
      <d-form label-position="top">
        <d-form-item label="ID">
          <d-input v-model="editForm.id" disabled />
        </d-form-item>

        <d-form-item :label="t('tools.name')">
          <d-input v-model="editForm.name" />
        </d-form-item>

        <d-form-item :label="t('tools.type')">
          <d-segmented v-model="editForm.type" :options="[{ label: 'python', value: 'python' }, { label: 'exe', value: 'exe' }]" />
        </d-form-item>

        <d-form-item :label="t('tools.path')">
          <div class="path-row">
            <d-input v-model="editForm.path" />
            <d-button @click="browseEditToolPath">{{ t('tools.browse') }}</d-button>
          </div>
        </d-form-item>

        <d-form-item v-if="isPythonEdit" :label="t('tools.python')">
          <div class="path-row">
            <d-input v-model="editForm.python" />
            <d-button @click="browseEditToolPython">{{ t('tools.browse') }}</d-button>
          </div>
        </d-form-item>

        <d-form-item :label="t('tools.cwd')">
          <d-input v-model="editForm.cwd" />
        </d-form-item>

        <d-form-item :label="t('tools.argsTemplate')">
          <d-input v-model="editForm.argsTemplate" />
        </d-form-item>

        <d-form-item :label="t('tools.tags')">
          <d-input v-model="editForm.tagsText" />
        </d-form-item>

        <d-form-item :label="t('tools.description')">
          <d-input v-model="editForm.description" type="textarea" :rows="3" />
        </d-form-item>
      </d-form>

      <template #footer>
        <div class="dialog-footer">
          <d-button @click="editVisible = false">{{ t('tools.cancel') }}</d-button>
          <d-button type="primary" :loading="updating" @click="saveEdit">{{ t('tools.save') }}</d-button>
        </div>
      </template>
    </d-modal>
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
  padding: 8px 4px 16px;
  min-width: 0;
}

/* ---- 卡片 d-card 样式覆盖 ---- */
.tool-card {
  cursor: pointer;
  margin-bottom: 16px;
  transition: border-color 0.2s ease, box-shadow 0.2s ease;
}

.tool-card :deep(.devui-card) {
  background: var(--vscode-sidebar-bg);
  border: 1px solid var(--vscode-border-color);
  border-radius: 6px;
}

.tool-card:hover :deep(.devui-card) {
  border-color: var(--vscode-accent-color);
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}

.card-title-group {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.card-title-group h3 {
  font-size: 15px;
  font-weight: 600;
  color: var(--vscode-text-primary);
  white-space: nowrap;
  text-overflow: ellipsis;
  overflow: hidden;
}

.card-badges {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-shrink: 0;
}

.card-body {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.card-footer {
  display: flex;
  justify-content: flex-end;
  width: 100%;
}

.tool-id {
  font-family: var(--vscode-font-mono);
  font-size: 11px;
  color: var(--vscode-text-muted);
}

.tool-path {
  padding: 6px 8px;
  border: 1px solid var(--vscode-border-color);
  background: var(--vscode-editor-bg);
  color: var(--vscode-text-muted);
  border-radius: 3px;
  font-family: var(--vscode-font-mono);
  font-size: 11px;
  white-space: nowrap;
  text-overflow: ellipsis;
  overflow: hidden;
  cursor: pointer;
}

.action-buttons {
  display: flex;
  gap: 8px;
}

.tool-tags {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: auto;
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
