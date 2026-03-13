<script setup lang="ts">
import { computed, ref } from 'vue'

import UiBadge from './ui/UiBadge.vue'
import UiButton from './ui/UiButton.vue'
import UiField from './ui/UiField.vue'
import UiInput from './ui/UiInput.vue'
import UiPanel from './ui/UiPanel.vue'
import { useI18n } from '../composables/useI18n'
import type { PythonPackageItem } from '../types'

const props = defineProps<{
  pythonPath: string
  packages: PythonPackageItem[]
  loading: boolean
  processing: boolean
  processingPackage: string
  processingAction: 'install' | 'uninstall' | ''
  statusText: string
}>()

const emit = defineEmits<{
  (e: 'browsePython'): void
  (e: 'useSystemPython'): void
  (e: 'refreshPackages'): void
  (e: 'installPackage', packageName: string): void
  (e: 'uninstallPackage', packageName: string): void
}>()

const packageKeyword = ref('')
const packageToInstall = ref('')
const { t } = useI18n()

const filteredPackages = computed(() => {
  const text = packageKeyword.value.trim().toLowerCase()
  if (!text) {
    return props.packages
  }

  return props.packages.filter((item) => {
    return item.name.toLowerCase().includes(text) || item.version.toLowerCase().includes(text)
  })
})

const statusTone = computed(() => {
  if (props.processing) {
    return 'accent'
  }

  const text = props.statusText.toLowerCase()
  return text.includes('fail') || text.includes('failed') || text.includes('澶辫触') ? 'danger' : 'success'
})

function installPackage(): void {
  const name = packageToInstall.value.trim()
  if (!name) {
    return
  }

  emit('installPackage', name)
  packageToInstall.value = ''
}

function confirmUninstall(name: string): void {
  if (!name.trim()) {
    return
  }

  if (typeof window !== 'undefined' && !window.confirm(t('python.uninstallConfirm', { name }))) {
    return
  }

  emit('uninstallPackage', name.trim())
}

function isRowBusy(name: string): boolean {
  return props.processing && props.processingAction === 'uninstall' && props.processingPackage === name
}
</script>

<template>
  <UiPanel class="flex h-full min-h-0 flex-col gap-4 bg-editor">
    <header class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
      <div class="space-y-1">
        <h2 class="text-base font-semibold text-foreground">{{ t('python.managerTitle') }}</h2>
        <p class="text-xs text-muted">{{ t('python.managerDesc') }}</p>
      </div>
      <UiButton :disabled="loading" @click="emit('refreshPackages')">{{ t('python.refresh') }}</UiButton>
    </header>

    <div class="grid gap-3 xl:grid-cols-[minmax(0,1fr)_auto_auto]">
      <UiInput :model-value="pythonPath || 'python'" readonly :placeholder="t('python.currentInterpreter')" />
      <UiButton @click="emit('browsePython')">{{ t('python.browse') }}</UiButton>
      <UiButton @click="emit('useSystemPython')">{{ t('python.systemPython') }}</UiButton>
    </div>

    <div class="grid gap-3 xl:grid-cols-[minmax(0,1fr)_auto]">
      <UiInput
        v-model="packageToInstall"
        :placeholder="t('python.installInput')"
        @keyup.enter="installPackage"
      />
      <UiButton variant="primary" :disabled="processing && processingAction === 'install'" @click="installPackage">
        {{ t('python.install') }}
      </UiButton>
    </div>

    <div class="rounded-panel border border-border p-3" :class="statusTone === 'success' ? 'bg-success-soft' : statusTone === 'danger' ? 'bg-danger-soft' : 'bg-accent-soft'">
      <p class="text-sm leading-6 text-foreground">{{ statusText || t('python.ready') }}</p>
    </div>

    <div class="grid gap-3 xl:grid-cols-[minmax(0,1fr)_auto] xl:items-center">
      <UiField>
        <UiInput v-model="packageKeyword" type="search" :placeholder="t('python.searchInstalled')" />
      </UiField>
      <UiBadge>{{ filteredPackages.length }} / {{ packages.length }}</UiBadge>
    </div>

    <div class="relative min-h-0 flex-1 overflow-auto rounded-panel border border-border bg-sidebar">
      <div v-if="loading" class="absolute inset-0 flex items-center justify-center bg-overlay backdrop-blur-sm">
        <span class="h-7 w-7 animate-spin rounded-full border-2 border-border-soft border-t-accent" />
      </div>

      <table v-else-if="filteredPackages.length > 0" class="min-w-full border-collapse text-sm">
        <thead class="sticky top-0 bg-editor text-left text-xs text-muted">
          <tr>
            <th class="border-b border-border px-3 py-2.5">{{ t('python.name') }}</th>
            <th class="border-b border-border px-3 py-2.5">{{ t('python.version') }}</th>
            <th class="border-b border-border px-3 py-2.5">{{ t('python.action') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in filteredPackages" :key="item.name">
            <td class="border-b border-border px-3 py-2.5 font-mono text-foreground">{{ item.name }}</td>
            <td class="border-b border-border px-3 py-2.5 text-foreground">{{ item.version }}</td>
            <td class="border-b border-border px-3 py-2.5">
              <UiButton
                size="sm"
                variant="danger"
                :disabled="processing && !isRowBusy(item.name)"
                @click="confirmUninstall(item.name)"
              >
                {{ t('python.uninstall') }}
              </UiButton>
            </td>
          </tr>
        </tbody>
      </table>

      <div v-else class="flex min-h-56 items-center justify-center p-6">
        <div class="flex flex-col items-center gap-3 text-center">
          <p class="text-sm text-muted">{{ t('python.noPackageData') }}</p>
          <div class="flex flex-wrap justify-center gap-2">
            <UiButton @click="emit('refreshPackages')">{{ t('python.refresh') }}</UiButton>
            <UiButton @click="emit('useSystemPython')">{{ t('python.systemPython') }}</UiButton>
          </div>
        </div>
      </div>
    </div>
  </UiPanel>
</template>
