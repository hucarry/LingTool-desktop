<script setup lang="ts">
defineProps<{
  label?: string
  hint?: string
  error?: string
}>()
</script>

<template>
  <label class="flex flex-col gap-2">
    <span v-if="label" class="text-xs font-medium text-foreground">{{ label }}</span>
    <slot />
    <span v-if="error || hint" :class="['text-xs leading-5', error ? 'text-danger' : 'text-muted']">
      {{ error || hint }}
    </span>
  </label>
</template>
