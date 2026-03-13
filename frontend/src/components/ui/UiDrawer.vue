<script setup lang="ts">
withDefaults(defineProps<{
  widthClass?: string
}>(), {
  widthClass: 'w-[min(520px,100vw)]',
})
</script>

<template>
  <aside :class="['h-full border-l border-border bg-sidebar shadow-drawer', widthClass]">
    <slot />
  </aside>
</template>
