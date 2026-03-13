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
    class="flex min-h-48 cursor-pointer flex-col rounded-panel border border-border bg-sidebar p-3.5 transition-transform transition-colors hover:-translate-y-0.5 hover:border-accent"
    @click="emit('open')"
  >
    <header class="flex items-start justify-between gap-3">
      <div class="flex min-w-0 items-center gap-2">
        <input
          type="checkbox"
          :checked="selected"
          @click.stop
          @change="emit('toggleSelect', ($event.target as HTMLInputElement).checked)"
        />
        <h3 class="truncate text-sm font-semibold text-foreground">{{ tool.name }}</h3>
      </div>

      <div class="flex flex-wrap items-center justify-end gap-1.5">
        <UiBadge>{{ tool.type }}</UiBadge>
        <UiBadge :tone="tool.valid ? 'success' : 'danger'">
          {{ tool.valid ? t('tools.ready') : t('tools.invalidPath') }}
        </UiBadge>
      </div>
    </header>

    <div class="mt-4 flex flex-1 flex-col gap-2">
      <p class="font-mono text-[11px] text-muted">{{ tool.id }}</p>
      <p class="truncate rounded-field border border-border bg-editor px-2.5 py-2 font-mono text-[11px] text-muted" :title="tool.path">
        {{ tool.path }}
      </p>

      <div class="flex flex-wrap gap-1.5">
        <UiBadge v-if="tool.cwd">{{ t('tools.cwdShort') }}: {{ tool.cwd }}</UiBadge>
        <UiBadge v-if="tool.description">{{ t('tools.descriptionShort') }}</UiBadge>
      </div>

      <div v-if="!tool.valid" class="rounded-field border border-warning/40 bg-warning-soft p-2.5">
        <UiBadge tone="warning">{{ t('tools.invalidPath') }}</UiBadge>
        <p class="mt-2 text-xs leading-5 text-foreground">{{ t('tools.invalidHint') }}</p>
      </div>

      <p v-if="tool.description" class="text-xs leading-5 text-muted">{{ tool.description }}</p>

      <div v-if="tool.tags.length" class="mt-auto flex flex-wrap gap-1.5">
        <UiBadge v-for="tag in tool.tags" :key="tag" tone="accent">{{ tag }}</UiBadge>
      </div>
    </div>

    <footer class="mt-4 flex justify-end gap-2" @click.stop>
      <UiButton size="sm" :disabled="busy" @click="emit('edit')">{{ t('tools.edit') }}</UiButton>
      <UiButton
        size="sm"
        :variant="tool.valid ? 'primary' : 'default'"
        :disabled="busy"
        @click="tool.valid ? emit('run') : emit('edit')"
      >
        {{ tool.valid ? t('tools.run') : t('tools.fixPath') }}
      </UiButton>
    </footer>
  </article>
</template>
