<script setup lang="ts">
import { computed, defineAsyncComponent, ref, watch } from 'vue'

import type { AddToolPayload, ToolItem } from '../types'
import { useI18n } from '../composables/useI18n'

const ToolEditDialog = defineAsyncComponent(() => import('./ToolEditDialog.vue'))

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
  (e: 'createTool'): void
  (e: 'refresh'): void
  (e: 'updateTool', payload: AddToolPayload): void
  (e: 'deleteTools', toolIds: string[]): void
  (e: 'pickEditToolPath', payload: { defaultPath?: string; toolType?: string }): void
  (e: 'pickEditToolPython', payload: { defaultPath?: string }): void
}>()

const keyword = ref('')
const selectedIds = ref<string[]>([])
const editVisible = ref(false)
const editingToolId = ref('')

const { t } = useI18n()

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
const hasSelection = computed(() => selectedCount.value > 0)
const hasActiveSearch = computed(() => keyword.value.trim().length > 0)
const invalidFilteredCount = computed(() => filteredTools.value.filter((tool) => !tool.valid).length)
const allFilteredSelected = computed(() => {
  if (filteredTools.value.length === 0) {
    return false
  }

  return filteredTools.value.every((tool) => selectedSet.value.has(tool.id))
})
const editingTool = computed(() => {
  if (!editingToolId.value) {
    return null
  }

  return props.tools.find((tool) => tool.id === editingToolId.value) ?? null
})

watch(
  () => props.tools,
  (nextTools) => {
    const liveIds = new Set(nextTools.map((item) => item.id))
    selectedIds.value = selectedIds.value.filter((id) => liveIds.has(id))

    if (editingToolId.value && !liveIds.has(editingToolId.value)) {
      editingToolId.value = ''
      editVisible.value = false
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

function toggleSelectAllFiltered(checked: boolean): void {
  if (!checked) {
    const filteredIds = new Set(filteredTools.value.map((tool) => tool.id))
    selectedIds.value = selectedIds.value.filter((id) => !filteredIds.has(id))
    return
  }

  const next = new Set(selectedIds.value)
  filteredTools.value.forEach((tool) => {
    next.add(tool.id)
  })
  selectedIds.value = Array.from(next)
}

function clearSelection(): void {
  selectedIds.value = []
}

function clearSearch(): void {
  keyword.value = ''
}

function openEdit(tool: ToolItem): void {
  editingToolId.value = tool.id
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

function deleteSelected(): void {
  if (selectedIds.value.length === 0) {
    return
  }

  if (typeof window !== 'undefined' && !window.confirm(t('tools.confirmDeleteMessage'))) {
    return
  }

  emit('deleteTools', [...selectedIds.value])
  selectedIds.value = []
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
        <label class="select-all-toggle">
          <input
            type="checkbox"
            :checked="allFilteredSelected"
            :disabled="filteredTools.length === 0"
            @change="(event) => toggleSelectAllFiltered((event.target as HTMLInputElement).checked)"
          />
          <span>{{ t('tools.selectAll') }}</span>
        </label>
        <span class="selected-count">{{ t('tools.selected') }}: {{ selectedCount }}</span>
        <button class="header-button primary" type="button" @click="emit('createTool')">{{ t('app.menu.addTool') }}</button>
        <button class="header-button" type="button" @click="emit('refresh')">{{ t('python.refresh') }}</button>
        <button class="header-button" type="button" :disabled="selectedCount !== 1" @click="openEditSelected">
          {{ t('tools.editSelected') }}
        </button>
        <button class="header-button danger" type="button" :disabled="selectedCount === 0 || deleting" @click="deleteSelected">
          {{ t('tools.deleteSelected') }}
        </button>
      </div>
    </header>

    <label class="search-box">
      <span class="search-icon" aria-hidden="true">/</span>
      <input v-model="keyword" type="search" :placeholder="t('tools.search')" />
    </label>

    <div class="tool-stats">
      <span class="stat-chip">{{ t('tools.filteredCount', { count: filteredTools.length }) }}</span>
      <span class="stat-chip">{{ t('tools.invalidCount', { count: invalidFilteredCount }) }}</span>
      <button v-if="hasActiveSearch" class="stat-chip action" type="button" @click="clearSearch">
        {{ t('tools.clearSearch') }}
      </button>
    </div>

    <div v-if="hasSelection" class="selection-bar">
      <span class="selection-summary">{{ t('tools.selectionSummary', { count: selectedCount }) }}</span>
      <div class="selection-actions">
        <button class="selection-button" type="button" :disabled="selectedCount !== 1" @click="openEditSelected">
          {{ t('tools.editSelected') }}
        </button>
        <button class="selection-button danger" type="button" :disabled="deleting" @click="deleteSelected">
          {{ t('tools.deleteSelected') }}
        </button>
        <button class="selection-button" type="button" @click="clearSelection">{{ t('tools.clearSelection') }}</button>
      </div>
    </div>

    <div class="list-scroll">
      <div class="tool-rows">
        <article v-for="tool in filteredTools" :key="tool.id" class="tool-card" @click="openTool(tool)">
          <header class="card-header">
            <div class="card-title-group">
              <input
                class="card-checkbox"
                type="checkbox"
                :checked="selectedSet.has(tool.id)"
                @click.stop
                @change="(event) => toggleSelect(tool.id, (event.target as HTMLInputElement).checked)"
              />
              <h3>{{ tool.name }}</h3>
            </div>
            <div class="card-badges">
              <span class="tool-type-chip">{{ tool.type }}</span>
              <span class="tool-status-chip" :class="tool.valid ? 'is-valid' : 'is-invalid'">
                {{ tool.valid ? t('tools.ready') : t('tools.invalidPath') }}
              </span>
            </div>
          </header>

          <div class="card-body">
            <p class="tool-id">{{ tool.id }}</p>
            <p class="tool-path" :title="tool.path">{{ tool.path }}</p>
            <div class="tool-meta">
              <span v-if="tool.cwd" class="tool-meta-item">{{ t('tools.cwdShort') }}: {{ tool.cwd }}</span>
              <span v-if="tool.description" class="tool-meta-item">{{ t('tools.descriptionShort') }}</span>
            </div>
            <p v-if="tool.description" class="tool-description">{{ tool.description }}</p>

            <div v-if="tool.tags && tool.tags.length" class="tool-tags">
              <span v-for="tag in tool.tags" :key="tag" class="tool-tag">{{ tag }}</span>
            </div>
          </div>

          <footer class="card-footer" @click.stop>
            <div class="action-buttons">
              <button class="card-button" type="button" :disabled="updating && editingToolId === tool.id" @click.stop="openEdit(tool)">
                {{ t('tools.edit') }}
              </button>
              <button class="card-button primary" type="button" :disabled="!tool.valid" @click.stop="runTool(tool)">
                {{ t('tools.run') }}
              </button>
            </div>
          </footer>
        </article>

        <div v-if="filteredTools.length === 0 && !loading" class="empty-state">
          <div class="empty-state-content">
            <p class="empty-state-title">{{ hasActiveSearch ? t('tools.noMatch') : t('tools.emptyTitle') }}</p>
            <p class="empty-state-text">{{ hasActiveSearch ? t('tools.emptySearchHint') : t('tools.emptyHint') }}</p>
            <div class="empty-state-actions">
              <button v-if="hasActiveSearch" class="header-button" type="button" @click="clearSearch">
                {{ t('tools.clearSearch') }}
              </button>
              <button class="header-button" type="button" @click="emit('refresh')">{{ t('python.refresh') }}</button>
              <button class="header-button primary" type="button" @click="emit('createTool')">{{ t('app.menu.addTool') }}</button>
            </div>
          </div>
        </div>

        <div v-if="loading" class="loading-overlay">
          <span class="loading-spinner" />
        </div>
      </div>
    </div>

    <ToolEditDialog
      v-if="editVisible"
      v-model:visible="editVisible"
      :tool="editingTool"
      :updating="updating"
      :edit-tool-path-selection="editToolPathSelection"
      :edit-tool-python-selection="editToolPythonSelection"
      @save="(payload: AddToolPayload) => emit('updateTool', payload)"
      @pick-tool-path="(payload) => emit('pickEditToolPath', payload)"
      @pick-tool-python="(payload) => emit('pickEditToolPython', payload)"
    />
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

.select-all-toggle {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--vscode-text-muted);
}

.select-all-toggle input {
  accent-color: var(--vscode-accent-color);
}

.selected-count {
  font-size: 12px;
  color: var(--vscode-text-muted);
  margin-right: 4px;
}

.header-button,
.card-button {
  height: 30px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 4px;
  background: var(--vscode-sidebar-bg);
  color: var(--vscode-text-primary);
  padding: 0 12px;
  cursor: pointer;
}

.header-button:hover:not(:disabled),
.card-button:hover:not(:disabled) {
  border-color: var(--vscode-accent-color);
  background: var(--vscode-hover-bg);
}

.header-button:disabled,
.card-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.header-button.danger:hover:not(:disabled) {
  border-color: var(--status-danger);
  color: var(--status-danger);
}

.card-button.primary {
  border-color: var(--vscode-accent-color);
  background: var(--vscode-accent-color);
  color: #ffffff;
}

.search-box {
  position: relative;
  display: flex;
  align-items: center;
}

.search-box input {
  width: 100%;
  height: 34px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 6px;
  background: var(--vscode-sidebar-bg);
  color: var(--vscode-text-primary);
  padding: 0 12px 0 34px;
}

.search-icon {
  position: absolute;
  left: 12px;
  color: var(--vscode-text-muted);
  font-size: 12px;
  pointer-events: none;
}

.tool-stats {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.stat-chip {
  display: inline-flex;
  align-items: center;
  min-height: 26px;
  padding: 0 10px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 999px;
  background: var(--surface-muted);
  color: var(--vscode-text-muted);
  font-size: 12px;
}

.stat-chip.action {
  cursor: pointer;
}

.stat-chip.action:hover {
  border-color: var(--vscode-accent-color);
  color: var(--vscode-text-primary);
}

.selection-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
  padding: 10px 12px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 8px;
  background: var(--surface-muted);
}

.selection-summary {
  font-size: 12px;
  color: var(--vscode-text-primary);
}

.selection-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.selection-button {
  height: 30px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 4px;
  background: var(--vscode-editor-bg);
  color: var(--vscode-text-primary);
  padding: 0 12px;
  cursor: pointer;
}

.selection-button:hover:not(:disabled) {
  border-color: var(--vscode-accent-color);
  background: var(--vscode-hover-bg);
}

.selection-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.selection-button.danger:hover:not(:disabled) {
  border-color: var(--status-danger);
  color: var(--status-danger);
}

.list-scroll {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.tool-rows {
  position: relative;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 16px;
  padding: 8px 4px 16px;
}

.tool-card {
  display: flex;
  flex-direction: column;
  min-height: 188px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 8px;
  background: var(--vscode-sidebar-bg);
  padding: 14px;
  cursor: pointer;
  transition: border-color 0.2s ease, transform 0.2s ease;
}

.tool-card:hover {
  border-color: var(--vscode-accent-color);
  transform: translateY(-1px);
}

.card-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
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

.card-checkbox {
  accent-color: var(--vscode-accent-color);
}

.card-badges {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
  justify-content: flex-end;
}

.tool-type-chip,
.tool-status-chip,
.tool-tag {
  display: inline-flex;
  align-items: center;
  height: 22px;
  padding: 0 8px;
  border-radius: 999px;
  font-size: 11px;
  white-space: nowrap;
}

.tool-type-chip {
  border: 1px solid var(--vscode-border-color);
  color: var(--vscode-text-muted);
}

.tool-status-chip.is-valid {
  background: var(--status-success-soft);
  color: var(--status-success);
}

.tool-status-chip.is-invalid {
  background: var(--status-danger-soft);
  color: var(--status-danger);
}

.card-body {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-top: 14px;
  min-height: 0;
  flex: 1;
}

.tool-id {
  font-family: var(--vscode-font-mono);
  font-size: 11px;
  color: var(--vscode-text-muted);
}

.tool-path {
  padding: 8px 10px;
  border: 1px solid var(--vscode-border-color);
  background: var(--vscode-editor-bg);
  color: var(--vscode-text-muted);
  border-radius: 4px;
  font-family: var(--vscode-font-mono);
  font-size: 11px;
  white-space: nowrap;
  text-overflow: ellipsis;
  overflow: hidden;
}

.tool-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.tool-meta-item {
  display: inline-flex;
  align-items: center;
  max-width: 100%;
  min-height: 20px;
  padding: 0 8px;
  border-radius: 999px;
  background: var(--surface-muted);
  color: var(--vscode-text-muted);
  font-size: 11px;
  white-space: nowrap;
  text-overflow: ellipsis;
  overflow: hidden;
}

.tool-description {
  color: var(--vscode-text-secondary);
  font-size: 12px;
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.tool-tags {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: auto;
}

.tool-tag {
  background: var(--accent-soft);
  color: var(--vscode-accent-color);
}

.card-footer {
  display: flex;
  justify-content: flex-end;
  margin-top: 14px;
}

.action-buttons {
  display: flex;
  gap: 8px;
}

.empty-state {
  grid-column: 1 / -1;
  min-height: 220px;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px dashed var(--vscode-border-color);
  border-radius: 8px;
  background: var(--surface-empty);
}

.empty-state-content {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  text-align: center;
  max-width: 420px;
  padding: 0 16px;
}

.empty-state-title {
  color: var(--vscode-text-primary);
  font-size: 15px;
  font-weight: 600;
}

.empty-state-text {
  color: var(--vscode-text-muted);
  font-size: 13px;
  line-height: 1.5;
}

.empty-state-actions {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  flex-wrap: wrap;
}

.loading-overlay {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--surface-overlay);
  border-radius: 8px;
  backdrop-filter: blur(2px);
}

.loading-spinner {
  width: 28px;
  height: 28px;
  border: 2px solid var(--border-soft);
  border-top-color: var(--vscode-accent-color);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 900px) {
  .tool-list-header {
    flex-direction: column;
  }

  .header-actions {
    justify-content: flex-start;
  }

  .selection-bar {
    align-items: flex-start;
  }
}
</style>
