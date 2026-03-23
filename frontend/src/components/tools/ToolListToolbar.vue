<script setup lang="ts">
import UiBadge from '../ui/UiBadge.vue'
import UiButton from '../ui/UiButton.vue'
import UiInput from '../ui/UiInput.vue'
import { useI18n } from '../../composables/useI18n'

defineProps<{
  keyword: string
  selectedCount: number
  allVisibleSelected: boolean
  visibleCount: number
  totalCount: number
  readyCount: number
  invalidCount: number
  filterMode: 'all' | 'ready' | 'invalid'
  hasActiveSearch: boolean
  canEditSelection: boolean
  deleting?: boolean
}>()

const emit = defineEmits<{
  (e: 'update:keyword', value: string): void
  (e: 'toggleSelectAll', checked: boolean): void
  (e: 'setFilterMode', value: 'all' | 'ready' | 'invalid'): void
  (e: 'createTool'): void
  (e: 'refresh'): void
  (e: 'editSelected'): void
  (e: 'deleteSelected'): void
  (e: 'clearSearch'): void
}>()

const { t } = useI18n()
</script>

<template>
  <header class="space-y-3">
    <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
      <div class="space-y-1.5">
        <h2 class="text-lg font-semibold text-foreground">{{ t('tools.catalog') }}</h2>
        <p class="text-xs text-muted">{{ t('tools.items', { filtered: visibleCount, total: totalCount }) }}</p>
      </div>

      <div class="flex flex-wrap items-center justify-start gap-2 xl:justify-end">
        <label class="inline-flex items-center gap-2 text-xs text-muted">
          <input
            type="checkbox"
            :checked="allVisibleSelected"
            :disabled="visibleCount === 0"
            @change="emit('toggleSelectAll', ($event.target as HTMLInputElement).checked)"
          />
          <span>{{ t('tools.selectAll') }}</span>
        </label>

        <UiBadge>{{ t('tools.selected') }}: {{ selectedCount }}</UiBadge>
        <UiButton variant="primary" @click="emit('createTool')">{{ t('app.menu.addTool') }}</UiButton>
        <UiButton @click="emit('refresh')">{{ t('python.refresh') }}</UiButton>
        <UiButton :disabled="!canEditSelection" @click="emit('editSelected')">{{ t('tools.editSelected') }}</UiButton>
        <UiButton variant="danger" :disabled="selectedCount === 0 || deleting" @click="emit('deleteSelected')">
          {{ t('tools.deleteSelected') }}
        </UiButton>
      </div>
    </div>

    <UiInput
      :model-value="keyword"
      type="search"
      :placeholder="t('tools.search')"
      @update:model-value="emit('update:keyword', $event as string)"
      @input="emit('update:keyword', ($event.target as HTMLInputElement).value)"
    />

    <div class="flex flex-wrap items-center gap-2">
      <UiButton size="sm" :variant="filterMode === 'all' ? 'primary' : 'ghost'" @click="emit('setFilterMode', 'all')">
        {{ t('tools.filteredCount', { count: visibleCount }) }}
      </UiButton>
      <UiButton size="sm" :variant="filterMode === 'ready' ? 'primary' : 'ghost'" @click="emit('setFilterMode', 'ready')">
        {{ t('tools.readyCount', { count: readyCount }) }}
      </UiButton>
      <UiButton size="sm" :variant="filterMode === 'invalid' ? 'primary' : 'ghost'" @click="emit('setFilterMode', 'invalid')">
        {{ t('tools.invalidCount', { count: invalidCount }) }}
      </UiButton>
      <UiButton v-if="hasActiveSearch" size="sm" @click="emit('clearSearch')">{{ t('tools.clearSearch') }}</UiButton>
    </div>
  </header>
</template>
