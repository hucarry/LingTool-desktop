<script setup lang="ts">
interface MenuItem {
  path: '/python' | '/tools' | '/tools/new' | '/settings' | '/ai-settings'
  title: string
}

defineProps<{
  items: MenuItem[]
  activePath: MenuItem['path']
  width: number
  overlay?: boolean
}>()

const emit = defineEmits<{
  (e: 'navigate', path: MenuItem['path']): void
  (e: 'close'): void
}>()
</script>

<template>
  <aside
    aria-label="Explorer"
    :class="[
      'flex shrink-0 flex-col border-r border-border bg-sidebar',
      overlay ? 'h-full shadow-dialog' : '',
    ]"
    :style="{ width: `${width}px` }"
  >
    <header class="flex h-[35px] items-center justify-between border-b border-border px-3 text-[11px] tracking-[0.08em] text-muted">
      <span>EXPLORER</span>
      <button
        v-if="overlay"
        type="button"
        class="inline-flex h-6 w-6 items-center justify-center rounded-sm text-muted transition-colors hover:bg-hovered hover:text-foreground"
        @click="emit('close')"
      >
        x
      </button>
    </header>

    <button
      v-for="item in items"
      :key="item.path"
      type="button"
      :class="[
        'flex flex-col gap-1 border-l-2 border-transparent px-3 py-2.5 text-left transition-colors hover:bg-hovered',
        activePath === item.path ? 'border-l-accent bg-active' : '',
      ]"
      @click="emit('navigate', item.path)"
    >
      <span class="text-xs text-foreground">{{ item.title }}</span>
      <span class="font-mono text-[11px] text-muted">{{ item.path }}</span>
    </button>
  </aside>
</template>
