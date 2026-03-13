<script setup lang="ts">
withDefaults(defineProps<{
  align?: 'center' | 'end'
}>(), {
  align: 'center',
})
</script>

<template>
  <div :class="['ui-overlay z-[4200] flex', align === 'center' ? 'items-center justify-center' : 'justify-end']">
    <slot />
  </div>
</template>
