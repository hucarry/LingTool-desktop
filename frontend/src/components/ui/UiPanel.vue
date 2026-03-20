<script setup lang="ts">
withDefaults(defineProps<{
  padded?: boolean
}>(), {
  padded: true,
})
</script>

<template>
  <section :class="['ui-panel', padded ? 'p-3' : '']">
    <slot />
  </section>
</template>
