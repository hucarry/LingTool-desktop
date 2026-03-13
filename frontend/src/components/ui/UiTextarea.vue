<script setup lang="ts">
const props = defineProps<{
  modelValue?: string
  invalid?: boolean
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
}>()
</script>

<template>
  <textarea
    v-bind="$attrs"
    :value="props.modelValue"
    :class="[
      'ui-field min-h-28 resize-y py-2.5',
      invalid ? 'border-danger focus:border-danger focus:ring-danger/30' : '',
    ]"
    @input="emit('update:modelValue', ($event.target as HTMLTextAreaElement).value)"
  />
</template>
