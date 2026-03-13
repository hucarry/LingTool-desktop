<script setup lang="ts">
import UiButton from '../ui/UiButton.vue'
import { useI18n } from '../../composables/useI18n'

defineProps<{
  selectedCount: number
  canFixSelected: boolean
  canEditSelected: boolean
  deleting?: boolean
}>()

const emit = defineEmits<{
  (e: 'editSelected'): void
  (e: 'fixSelected'): void
  (e: 'deleteSelected'): void
  (e: 'clearSelection'): void
}>()

const { t } = useI18n()
</script>

<template>
  <div class="flex flex-col gap-3 rounded-panel border border-border bg-soft p-3 xl:flex-row xl:items-center xl:justify-between">
    <span class="text-xs text-foreground">{{ t('tools.selectionSummary', { count: selectedCount }) }}</span>

    <div class="flex flex-wrap items-center gap-2">
      <UiButton v-if="canFixSelected" size="sm" @click="emit('fixSelected')">{{ t('tools.fixPath') }}</UiButton>
      <UiButton size="sm" :disabled="!canEditSelected" @click="emit('editSelected')">{{ t('tools.editSelected') }}</UiButton>
      <UiButton size="sm" variant="danger" :disabled="deleting" @click="emit('deleteSelected')">
        {{ t('tools.deleteSelected') }}
      </UiButton>
      <UiButton size="sm" @click="emit('clearSelection')">{{ t('tools.clearSelection') }}</UiButton>
    </div>
  </div>
</template>
