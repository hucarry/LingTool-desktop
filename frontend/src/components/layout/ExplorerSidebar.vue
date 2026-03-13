<script setup lang="ts">
interface MenuItem {
  path: '/python' | '/tools' | '/tools/new' | '/settings'
  title: string
}

defineProps<{
  items: MenuItem[]
  activePath: MenuItem['path']
  width: number
}>()

const emit = defineEmits<{
  (e: 'navigate', path: MenuItem['path']): void
}>()
</script>

<template>
  <aside
    aria-label="Explorer"
    class="flex shrink-0 flex-col border-r border-border bg-sidebar"
    :style="{ width: `${width}px` }"
  >
    <header class="flex h-[35px] items-center border-b border-border px-3 text-[11px] tracking-[0.08em] text-muted">
      EXPLORER
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
