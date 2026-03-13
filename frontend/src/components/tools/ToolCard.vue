<script setup lang="ts">
import UiBadge from '../ui/UiBadge.vue'
import UiButton from '../ui/UiButton.vue'
import { useI18n } from '../../composables/useI18n'
import type { ToolItem } from '../../types'

defineProps<{
  tool: ToolItem
  selected: boolean
  busy?: boolean
}>()

const emit = defineEmits<{
  (e: 'open'): void
  (e: 'toggleSelect', checked: boolean): void
  (e: 'edit'): void
  (e: 'run'): void
}>()

const { t } = useI18n()
</script>

<template>
  <article
    class="flex min-h-36 cursor-pointer flex-col rounded-panel border border-border bg-sidebar p-3 transition-transform transition-colors hover:-translate-y-0.5 hover:border-accent"
    @click="emit('open')"
  >
    <header class="flex items-start justify-between gap-2">
      <div class="flex min-w-0 items-center gap-1.5">
        <input
          type="checkbox"
          :checked="selected"
          @click.stop
          @change="emit('toggleSelect', ($event.target as HTMLInputElement).checked)"
        />
        <h3 class="truncate text-[13px] font-semibold text-foreground">{{ tool.name }}</h3>
      </div>

      <div class="flex shrink-0 items-center justify-end gap-1.5">
        <span
          class="inline-flex h-7 w-7 items-center justify-center rounded-chip border border-border bg-editor text-muted"
          :title="tool.type"
          :aria-label="tool.type"
        >
          <svg
            v-if="tool.type === 'python'"
            viewBox="0 0 24 24"
            class="h-3.5 w-3.5"
            fill="none"
            stroke="currentColor"
            stroke-linecap="round"
            stroke-linejoin="round"
            stroke-width="1.7"
            aria-hidden="true"
          >
            <path d="M9 4h3.5A3.5 3.5 0 0 1 16 7.5V10H9a2 2 0 0 0-2 2v1.5A3.5 3.5 0 0 0 10.5 17H14" />
            <path d="M15 20h-3.5A3.5 3.5 0 0 1 8 16.5V14h7a2 2 0 0 0 2-2v-1.5A3.5 3.5 0 0 0 13.5 7H10" />
            <circle cx="10" cy="7.5" r=".75" fill="currentColor" stroke="none" />
            <circle cx="14" cy="16.5" r=".75" fill="currentColor" stroke="none" />
          </svg>
          <svg
            v-else
            viewBox="0 0 24 24"
            class="h-3.5 w-3.5"
            fill="none"
            stroke="currentColor"
            stroke-linecap="round"
            stroke-linejoin="round"
            stroke-width="1.7"
            aria-hidden="true"
          >
            <rect x="4" y="5" width="16" height="14" rx="2" />
            <path d="M8 10l2 2-2 2M13 14h3" />
          </svg>
        </span>

        <span
          class="inline-flex h-2.5 w-2.5 rounded-full"
          :class="tool.valid ? 'bg-success' : 'bg-danger'"
          :title="tool.valid ? t('tools.ready') : t('tools.invalidPath')"
          :aria-label="tool.valid ? t('tools.ready') : t('tools.invalidPath')"
        />
      </div>
    </header>

    <div class="mt-3 flex flex-1 flex-col gap-2">
      <div v-if="!tool.valid" class="rounded-field border border-warning/40 bg-warning-soft p-2">
        <UiBadge tone="warning">{{ t('tools.invalidPath') }}</UiBadge>
        <p class="mt-1.5 text-[11px] leading-4 text-foreground">{{ t('tools.invalidHint') }}</p>
      </div>

    </div>

    <footer class="mt-3 flex items-end justify-between gap-2" @click.stop>
      <div class="flex min-w-0 flex-wrap gap-1">
        <UiBadge v-for="tag in tool.tags" :key="tag" tone="accent">{{ tag }}</UiBadge>
      </div>

      <div class="flex shrink-0 gap-1.5">
        <UiButton size="sm" class="min-h-7 px-2" :disabled="busy" @click="emit('edit')">{{ t('tools.edit') }}</UiButton>
        <UiButton
          size="sm"
          class="min-h-7 px-2"
          :variant="tool.valid ? 'primary' : 'default'"
          :disabled="busy"
          @click="tool.valid ? emit('run') : emit('edit')"
        >
          {{ tool.valid ? t('tools.run') : t('tools.fixPath') }}
        </UiButton>
      </div>
    </footer>
  </article>
</template>
