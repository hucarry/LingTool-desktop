<script setup lang="ts">
defineProps<{
  leftItems: string[]
  rightItems: Array<{ label: string; title?: string }>
}>()
</script>

<template>
  <footer class="flex h-[22px] shrink-0 items-center justify-between gap-2 overflow-hidden bg-statusbar px-1.5 text-[11px] text-statusbar-fg sm:px-2">
    <div class="flex min-w-0 flex-1 items-center overflow-hidden">
      <span
        v-for="item in leftItems"
        :key="item"
        class="flex h-full min-w-0 items-center truncate px-1.5 whitespace-nowrap transition-colors hover:bg-[var(--statusbar-hover-bg)] sm:px-2"
      >
        {{ item }}
      </span>
    </div>

    <div class="flex min-w-0 max-w-[48%] items-center justify-end overflow-hidden">
      <span
        v-for="item in rightItems"
        :key="item.label"
        :title="item.title"
        class="flex h-full min-w-0 items-center truncate px-1.5 whitespace-nowrap transition-colors hover:bg-[var(--statusbar-hover-bg)] sm:px-2"
      >
        {{ item.label }}
      </span>
    </div>
  </footer>
</template>
