<script setup lang="ts">
import { computed, defineAsyncComponent, ref, watch } from 'vue'

import ToolCard from './tools/ToolCard.vue'
import ToolListToolbar from './tools/ToolListToolbar.vue'
import ToolSelectionBar from './tools/ToolSelectionBar.vue'
import { useI18n } from '../composables/useI18n'
import type { AddToolPayload, ToolItem } from '../types'

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
const filterMode = ref<'all' | 'ready' | 'invalid'>('all')
const pendingSelectionFallbackId = ref('')

const { t } = useI18n()

const searchedTools = computed(() => {
  const text = keyword.value.trim().toLowerCase()
  if (!text) {
    return props.tools
  }

  return props.tools.filter((tool) => {
    return tool.name.toLowerCase().includes(text)
      || tool.tags.some((tag) => tag.toLowerCase().includes(text))
  })
})

const visibleTools = computed(() => {
  if (filterMode.value === 'invalid') {
    return searchedTools.value.filter((tool) => !tool.valid)
  }

  if (filterMode.value === 'ready') {
    return searchedTools.value.filter((tool) => tool.valid)
  }

  return searchedTools.value
})

const selectedCount = computed(() => selectedIds.value.length)
const selectedSet = computed(() => new Set(selectedIds.value))
const hasSelection = computed(() => selectedCount.value > 0)
const selectedInvalidCount = computed(() => {
  return props.tools.filter((tool) => selectedSet.value.has(tool.id) && !tool.valid).length
})
const canFixSelected = computed(() => selectedCount.value === 1 && selectedInvalidCount.value === 1)
const hasActiveSearch = computed(() => keyword.value.trim().length > 0)
const invalidTotalCount = computed(() => props.tools.filter((tool) => !tool.valid).length)
const readyTotalCount = computed(() => props.tools.filter((tool) => tool.valid).length)
const allVisibleSelected = computed(() => {
  return visibleTools.value.length > 0 && visibleTools.value.every((tool) => selectedSet.value.has(tool.id))
})
const editingTool = computed(() => {
  return editingToolId.value
    ? props.tools.find((tool) => tool.id === editingToolId.value) ?? null
    : null
})

watch(
  () => props.tools,
  (nextTools) => {
    const liveIds = new Set(nextTools.map((item) => item.id))
    selectedIds.value = selectedIds.value.filter((id) => liveIds.has(id))

    if (pendingSelectionFallbackId.value) {
      if (liveIds.has(pendingSelectionFallbackId.value)) {
        selectedIds.value = [pendingSelectionFallbackId.value]
      }
      pendingSelectionFallbackId.value = ''
    }

    if (editingToolId.value && !liveIds.has(editingToolId.value)) {
      editingToolId.value = ''
      editVisible.value = false
    }
  },
)

function toggleSelect(toolId: string, checked: boolean): void {
  const next = new Set(selectedIds.value)
  if (checked) {
    next.add(toolId)
  } else {
    next.delete(toolId)
  }

  selectedIds.value = Array.from(next)
}

function toggleSelectAllVisible(checked: boolean): void {
  if (!checked) {
    const visibleIds = new Set(visibleTools.value.map((tool) => tool.id))
    selectedIds.value = selectedIds.value.filter((id) => !visibleIds.has(id))
    return
  }

  const next = new Set(selectedIds.value)
  visibleTools.value.forEach((tool) => next.add(tool.id))
  selectedIds.value = Array.from(next)
}

function clearSelection(): void {
  selectedIds.value = []
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
  if (target) {
    openEdit(target)
  }
}

function deleteSelected(): void {
  if (selectedIds.value.length === 0) {
    return
  }

  if (typeof window !== 'undefined' && !window.confirm(t('tools.confirmDeleteMessage'))) {
    return
  }

  const deletingIds = new Set(selectedIds.value)
  pendingSelectionFallbackId.value = visibleTools.value.find((tool) => !deletingIds.has(tool.id))?.id ?? ''
  emit('deleteTools', [...selectedIds.value])
  selectedIds.value = []
}
</script>

<template>
  <section class="ui-panel flex h-full min-h-0 flex-col gap-3 overflow-hidden bg-editor p-3">
    <ToolListToolbar
      :keyword="keyword"
      :selected-count="selectedCount"
      :all-visible-selected="allVisibleSelected"
      :visible-count="visibleTools.length"
      :total-count="tools.length"
      :ready-count="readyTotalCount"
      :invalid-count="invalidTotalCount"
      :filter-mode="filterMode"
      :has-active-search="hasActiveSearch"
      :can-edit-selection="selectedCount === 1"
      :deleting="deleting"
      @update:keyword="keyword = $event"
      @toggle-select-all="toggleSelectAllVisible"
      @set-filter-mode="filterMode = $event"
      @create-tool="emit('createTool')"
      @refresh="emit('refresh')"
      @edit-selected="openEditSelected"
      @delete-selected="deleteSelected"
      @clear-search="keyword = ''"
    />

    <ToolSelectionBar
      v-if="hasSelection"
      :selected-count="selectedCount"
      :can-fix-selected="canFixSelected"
      :can-edit-selected="selectedCount === 1"
      :deleting="deleting"
      @edit-selected="openEditSelected"
      @fix-selected="openEditSelected"
      @delete-selected="deleteSelected"
      @clear-selection="clearSelection"
    />

    <div class="relative min-h-0 flex-1 overflow-auto">
      <div class="grid grid-cols-1 gap-4 p-1 md:grid-cols-2 2xl:grid-cols-3">
        <ToolCard
          v-for="tool in visibleTools"
          :key="tool.id"
          :tool="tool"
          :selected="selectedSet.has(tool.id)"
          :busy="updating && editingToolId === tool.id"
          @open="emit('openTool', tool)"
          @toggle-select="toggleSelect(tool.id, $event)"
          @edit="openEdit(tool)"
          @run="emit('runTool', tool)"
        />

        <div
          v-if="visibleTools.length === 0 && !loading"
          class="col-span-full flex min-h-56 items-center justify-center rounded-panel border border-dashed border-border bg-empty p-6"
        >
          <div class="flex max-w-md flex-col items-center gap-3 text-center">
            <p class="text-sm font-semibold text-foreground">{{ hasActiveSearch ? t('tools.noMatch') : t('tools.emptyTitle') }}</p>
            <p class="text-sm leading-6 text-muted">
              {{
                hasActiveSearch
                  ? t('tools.emptySearchHint')
                  : filterMode === 'invalid'
                    ? t('tools.emptyInvalidHint')
                    : filterMode === 'ready'
                      ? t('tools.emptyReadyHint')
                      : t('tools.emptyHint')
              }}
            </p>
            <div class="flex flex-wrap justify-center gap-2">
              <UiButton v-if="hasActiveSearch" @click="keyword = ''">{{ t('tools.clearSearch') }}</UiButton>
              <UiButton v-if="filterMode !== 'all'" @click="filterMode = 'all'">{{ t('tools.showAll') }}</UiButton>
              <UiButton @click="emit('refresh')">{{ t('python.refresh') }}</UiButton>
              <UiButton variant="primary" @click="emit('createTool')">{{ t('app.menu.addTool') }}</UiButton>
            </div>
          </div>
        </div>
      </div>

      <div v-if="loading" class="absolute inset-0 flex items-center justify-center rounded-panel bg-overlay backdrop-blur-sm">
        <span class="h-7 w-7 animate-spin rounded-full border-2 border-border-soft border-t-accent" />
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
