<script setup lang="ts">
import { computed } from 'vue'

import UiButton from '../ui/UiButton.vue'
import UiField from '../ui/UiField.vue'
import UiInput from '../ui/UiInput.vue'
import UiSelect from '../ui/UiSelect.vue'

const model = defineModel<string>({ default: '' })

const props = defineProps<{
  label: string
  hint?: string
  error?: string
  invalid?: boolean
  placeholder?: string
  browseLabel: string
  desktopPath?: string
  appRootPath?: string
  desktopLabel: string
  appRootLabel: string
  customLabel: string
  browseVisible?: boolean
}>()

const emit = defineEmits<{
  (e: 'browse'): void
  (e: 'blur'): void
}>()

const presetOptions = computed(() => {
  return [
    props.desktopPath?.trim()
      ? { label: props.desktopLabel, value: props.desktopPath.trim() }
      : null,
    props.appRootPath?.trim()
      ? { label: props.appRootLabel, value: props.appRootPath.trim() }
      : null,
  ].filter((option): option is { label: string; value: string } => Boolean(option))
})

const selectedPreset = computed(() => {
  const currentValue = model.value.trim()
  return presetOptions.value.some((option) => option.value === currentValue)
    ? currentValue
    : ''
})

const layoutClass = computed(() => {
  if (presetOptions.value.length > 0 && props.browseVisible) {
    return 'md:grid-cols-[180px_minmax(0,1fr)_auto]'
  }

  if (presetOptions.value.length > 0) {
    return 'md:grid-cols-[180px_minmax(0,1fr)]'
  }

  if (props.browseVisible) {
    return 'md:grid-cols-[minmax(0,1fr)_auto]'
  }

  return ''
})

function handlePresetChange(nextValue: string): void {
  if (!nextValue) {
    return
  }

  model.value = nextValue
}
</script>

<template>
  <UiField :label="label" :hint="hint" :error="error">
    <div class="grid gap-2" :class="layoutClass">
      <UiSelect
        v-if="presetOptions.length > 0"
        :model-value="selectedPreset"
        :invalid="invalid"
        @update:model-value="handlePresetChange"
      >
        <option value="">{{ customLabel }}</option>
        <option v-for="option in presetOptions" :key="option.value" :value="option.value">
          {{ option.label }}
        </option>
      </UiSelect>

      <UiInput
        v-model="model"
        :invalid="invalid"
        :placeholder="placeholder"
        @blur="emit('blur')"
      />

      <UiButton v-if="browseVisible" @click="emit('browse')">
        {{ browseLabel }}
      </UiButton>
    </div>
  </UiField>
</template>
