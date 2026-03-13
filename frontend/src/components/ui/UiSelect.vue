<script setup lang="ts">
const props = defineProps<{
  modelValue?: string | number
  invalid?: boolean
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
}>()
</script>

<template>
  <select
    v-bind="$attrs"
    :value="props.modelValue"
    :class="[
      'ui-field h-10 appearance-none',
      invalid ? 'border-danger focus:border-danger focus:ring-danger/30' : '',
    ]"
    @change="emit('update:modelValue', ($event.target as HTMLSelectElement).value)"
  >
    <slot />
  </select>
</template>
