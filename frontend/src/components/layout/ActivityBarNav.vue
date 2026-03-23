<script setup lang="ts">
interface MenuItem {
  path: '/python' | '/tools' | '/tools/new' | '/settings' | '/ai-settings'
  glyph: string
  title: string
}

defineProps<{
  items: MenuItem[]
  activePath: MenuItem['path']
  compact?: boolean
}>()

const emit = defineEmits<{
  (e: 'navigate', path: MenuItem['path']): void
}>()
</script>

<template>
  <nav
    aria-label="Activity Bar"
    :class="[
      'flex shrink-0 flex-col border-r border-border bg-activity',
      compact ? 'w-10' : 'w-12',
    ]"
  >
    <div :class="compact ? 'h-8 w-full' : 'h-[35px] w-full'" />

    <button
      v-for="item in items"
      :key="item.path"
      type="button"
      :title="item.title"
      :aria-label="item.title"
      :class="[
        'relative flex w-full items-center justify-center text-muted transition-colors hover:text-foreground',
        compact ? 'h-9' : 'h-[42px]',
        activePath === item.path ? 'text-foreground' : '',
      ]"
      @click="emit('navigate', item.path)"
    >
      <span
        v-if="activePath === item.path"
        :class="compact ? 'absolute inset-y-[6px] left-0 w-0.5 bg-accent' : 'absolute inset-y-[7px] left-0 w-0.5 bg-accent'"
      />
      <span
        :class="[
          'inline-flex items-center justify-center font-bold tracking-[0.08em]',
          compact ? 'min-w-4 text-[9px]' : 'min-w-5 text-[10px]',
        ]"
      >
        {{ item.glyph }}
      </span>
    </button>
  </nav>
</template>
