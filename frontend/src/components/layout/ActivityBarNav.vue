<script setup lang="ts">
interface MenuItem {
  path: '/python' | '/tools' | '/tools/new' | '/settings'
  glyph: string
  title: string
}

defineProps<{
  items: MenuItem[]
  activePath: MenuItem['path']
}>()

const emit = defineEmits<{
  (e: 'navigate', path: MenuItem['path']): void
}>()
</script>

<template>
  <nav aria-label="Activity Bar" class="flex w-12 shrink-0 flex-col border-r border-border bg-activity">
    <div class="h-[35px] w-full" />

    <button
      v-for="item in items"
      :key="item.path"
      type="button"
      :title="item.title"
      :aria-label="item.title"
      :class="[
        'relative flex h-[42px] w-full items-center justify-center text-muted transition-colors hover:text-foreground',
        activePath === item.path ? 'text-foreground' : '',
      ]"
      @click="emit('navigate', item.path)"
    >
      <span v-if="activePath === item.path" class="absolute inset-y-[7px] left-0 w-0.5 bg-accent" />
      <span class="inline-flex min-w-5 items-center justify-center text-[10px] font-bold tracking-[0.08em]">
        {{ item.glyph }}
      </span>
    </button>
  </nav>
</template>
