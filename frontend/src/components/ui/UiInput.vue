<script setup lang="ts">
const props = defineProps<{
  modelValue?: string | number
  invalid?: boolean
  readonly?: boolean
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
}>()
</script>

<template>
  <input
    v-bind="$attrs"
    :value="props.modelValue"
    :readonly="readonly"
    :class="[
      'ui-field min-h-[32px] text-[13px]',
      invalid ? 'border-danger focus:border-danger focus:ring-danger/30' : '',
      readonly ? 'ui-field-readonly' : '',
    ]"
    @input="emit('update:modelValue', ($event.target as HTMLInputElement).value)"
  />
</template>
