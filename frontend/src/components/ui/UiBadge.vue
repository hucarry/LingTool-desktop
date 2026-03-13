<script setup lang="ts">
const props = withDefaults(defineProps<{
  tone?: 'default' | 'accent' | 'success' | 'warning' | 'danger'
}>(), {
  tone: 'default',
})

const toneClasses = {
  default: 'text-muted',
  accent: 'border-accent/30 bg-accent-soft text-accent',
  success: 'border-success/30 bg-success-soft text-success',
  warning: 'border-warning/30 bg-warning-soft text-warning',
  danger: 'border-danger/30 bg-danger-soft text-danger',
}
</script>

<template>
  <span :class="['ui-badge', toneClasses[props.tone]]">
    <slot />
  </span>
</template>
