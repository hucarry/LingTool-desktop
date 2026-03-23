<script setup lang="ts">
const props = withDefaults(defineProps<{
  variant?: 'default' | 'primary' | 'danger' | 'ghost'
  size?: 'sm' | 'md'
  type?: 'button' | 'submit' | 'reset'
  block?: boolean
}>(), {
  variant: 'default',
  size: 'md',
  type: 'button',
  block: false,
})

import { computed } from 'vue'

const classes = computed(() => [
  'ui-btn',
  props.variant === 'primary' ? 'ui-btn-primary' : '',
  props.variant === 'danger' ? 'ui-btn-danger' : '',
  props.variant === 'ghost' ? 'border-transparent bg-transparent' : '',
  props.size === 'sm' ? 'min-h-[26px] px-2 text-xs' : '',
  props.block ? 'w-full' : '',
])
</script>

<template>
  <button :type="type" :class="classes">
    <slot />
  </button>
</template>
