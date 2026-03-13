<script setup lang="ts">
defineProps<{
  leftItems: string[]
  rightItems: Array<{ label: string; title?: string }>
}>()
</script>

<template>
  <footer class="flex h-[22px] shrink-0 items-center justify-between bg-statusbar px-2 text-[11px] text-statusbar-fg">
    <div class="flex min-w-0 items-center overflow-hidden">
      <span
        v-for="item in leftItems"
        :key="item"
        class="flex h-full items-center px-2 whitespace-nowrap transition-colors hover:bg-[var(--statusbar-hover-bg)]"
      >
        {{ item }}
      </span>
    </div>

    <div class="flex min-w-0 items-center overflow-hidden">
      <span
        v-for="item in rightItems"
        :key="item.label"
        :title="item.title"
        class="flex h-full items-center px-2 whitespace-nowrap transition-colors hover:bg-[var(--statusbar-hover-bg)]"
      >
        {{ item.label }}
      </span>
    </div>
  </footer>
</template>
