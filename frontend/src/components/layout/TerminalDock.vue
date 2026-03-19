<script setup lang="ts">
import { computed, defineAsyncComponent, ref, watch } from 'vue'

import type { TerminalInfo } from '../../types'
import {
  COLLAPSED_TERMINAL_HEIGHT,
} from '../../stores/workbench'

const TerminalPanel = defineAsyncComponent(() => import('../TerminalPanel.vue'))

const props = defineProps<{
  height: number
  expanded: boolean
  terminals: TerminalInfo[]
  activeTerminalId: string
  outputsByTerminal: Record<string, string[]>
  sessionCaption: string
  title: string
  toggleLabel: string
}>()

const emit = defineEmits<{
  (e: 'toggle'): void
  (e: 'expand'): void
  (e: 'dragStart', event: MouseEvent): void
  (e: 'selectTerminal', terminalId: string): void
  (e: 'createTerminal', payload: { shell?: string; cwd?: string }): void
  (e: 'stopTerminal', terminalId: string): void
  (e: 'stopAllTerminals'): void
  (e: 'input', payload: { terminalId: string; data: string }): void
  (e: 'resizeTerminal', payload: { terminalId: string; cols: number; rows: number }): void
  (e: 'clearOutput', terminalId: string): void
}>()

const panelHeight = computed(() => {
  return props.expanded ? `${props.height}px` : `${COLLAPSED_TERMINAL_HEIGHT}px`
})

const hasBeenExpanded = ref(props.expanded)
watch(
  () => props.expanded,
  (expanded) => {
    if (expanded) {
      hasBeenExpanded.value = true
    }
  },
  { immediate: true },
)
</script>

<template>
  <section class="relative flex shrink-0 flex-col border-t border-border bg-panel" :style="{ height: panelHeight }">
    <div class="absolute inset-x-0 -top-[3px] z-10 h-1.5 cursor-ns-resize" @mousedown="emit('dragStart', $event)">
      <div class="absolute inset-x-0 top-0.5 h-0.5 bg-transparent transition-colors hover:bg-accent" />
    </div>

    <header class="flex h-[35px] items-center justify-between border-b border-border pr-4">
      <div class="flex h-full items-center">
        <button
          type="button"
          class="h-full border-b border-accent px-3 text-[11px] tracking-[0.04em] text-foreground uppercase"
          @click="emit('expand')"
        >
          {{ title }}
        </button>
        <span class="ml-3 text-[11px] text-muted">{{ sessionCaption }}</span>
      </div>

      <button type="button" class="text-sm text-muted transition-colors hover:text-foreground" @click="emit('toggle')">
        {{ toggleLabel }}
      </button>
    </header>

    <div v-show="expanded" class="min-h-0 flex-1">
      <TerminalPanel
        v-if="hasBeenExpanded"
        :visible="expanded"
        :terminals="terminals"
        :active-terminal-id="activeTerminalId"
        :outputs-by-terminal="outputsByTerminal"
        @select-terminal="emit('selectTerminal', $event)"
        @create-terminal="emit('createTerminal', $event)"
        @stop-terminal="emit('stopTerminal', $event)"
        @stop-all-terminals="emit('stopAllTerminals')"
        @input="emit('input', $event)"
        @resize-terminal="emit('resizeTerminal', $event)"
        @clear-output="emit('clearOutput', $event)"
      />
    </div>
  </section>
</template>
