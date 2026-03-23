<script setup lang="ts">
import { computed, ref } from 'vue'

import UiBadge from './ui/UiBadge.vue'
import UiButton from './ui/UiButton.vue'
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

const packageColumns = computed<[PythonPackageItem[], PythonPackageItem[]]>(() => {
  const list = filteredPackages.value
  const half = Math.ceil(list.length / 2)
  return [
    list.slice(0, half),
    list.slice(half)
  ]
})

const statusTone = computed(() => {
  if (props.processing) {
    return 'accent'
  }

  const text = props.statusText.toLowerCase()
  return text.includes('fail') || text.includes('failed') ? 'danger' : 'success'
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
  <UiPanel class="flex h-full min-h-0 flex-col gap-3 bg-editor p-3">
    <header class="flex flex-col gap-2 min-[500px]:flex-row min-[500px]:items-start min-[500px]:justify-between">
      <div class="space-y-1">
        <h2 class="text-xl font-semibold tracking-tight text-foreground">{{ t('python.managerTitle') }}</h2>
        <p class="text-sm text-muted">{{ t('python.managerDesc') }}</p>
      </div>
      <UiButton class="min-[500px]:self-start" :disabled="loading" @click="emit('refreshPackages')">{{ t('python.refresh') }}</UiButton>
    </header>

    <section class="space-y-2">
      <div class="grid gap-2 min-[500px]:grid-cols-[minmax(0,1fr)_auto_auto]">
        <UiInput
          :model-value="pythonPath || 'python'"
          readonly
          :placeholder="t('python.currentInterpreter')"
          class="font-mono min-[500px]:min-w-0"
        />
        <div class="grid gap-2 min-[500px]:contents">
          <UiButton @click="emit('browsePython')">{{ t('python.browse') }}</UiButton>
          <UiButton @click="emit('useSystemPython')">{{ t('python.systemPython') }}</UiButton>
        </div>
      </div>

      <div class="grid gap-2 min-[500px]:grid-cols-[minmax(0,1fr)_auto]">
        <UiInput
          v-model="packageToInstall"
          class="min-[500px]:min-w-0"
          :placeholder="t('python.installInput')"
          @keyup.enter="installPackage"
        />
        <UiButton
          variant="primary"
          :disabled="processing && processingAction === 'install'"
          @click="installPackage"
        >
          {{ t('python.install') }}
        </UiButton>
      </div>

      <div
        class="text-sm font-medium"
        :class="statusTone === 'success' ? 'text-success' : statusTone === 'danger' ? 'text-danger' : 'text-accent'"
      >
        {{ statusText || t('python.ready') }}
      </div>
    </section>

    <section class="flex min-h-0 flex-1 flex-col gap-2">
      <div class="grid gap-2 min-[500px]:grid-cols-[minmax(0,1fr)_auto] min-[500px]:items-center">
        <UiInput
          v-model="packageKeyword"
          class="min-[500px]:min-w-0"
          type="search"
          :placeholder="t('python.searchInstalled')"
        />
        <UiBadge class="justify-center min-[500px]:min-w-24">{{ filteredPackages.length }} / {{ packages.length }}</UiBadge>
      </div>

      <div class="relative min-h-0 flex-1 overflow-auto rounded-panel border border-border bg-sidebar">
        <div v-if="loading" class="absolute inset-0 flex items-center justify-center bg-overlay backdrop-blur-sm">
          <span class="h-7 w-7 animate-spin rounded-full border-2 border-border-soft border-t-accent" />
        </div>

        <div v-if="filteredPackages.length > 0" class="grid relative grid-cols-1 min-[900px]:grid-cols-2">
          <div v-if="packageColumns[1].length > 0" class="pointer-events-none absolute inset-y-0 left-1/2 z-20 hidden w-px bg-border min-[900px]:block"></div>
          <table
            v-for="(col, index) in packageColumns"
            :key="index"
            v-show="col.length > 0"
            class="min-w-full border-collapse text-sm"
          >
            <thead class="sticky top-0 z-10 bg-editor text-left text-xs font-semibold text-muted">
              <tr>
                <th class="border-b border-border px-4 py-2" :class="{ 'min-[900px]:pl-6': index === 1 }">{{ t('python.name') }}</th>
                <th class="border-b border-border px-4 py-2">{{ t('python.version') }}</th>
                <th class="border-b border-border px-4 py-2 text-right" :class="{ 'min-[900px]:pr-6': index === 0 }">{{ t('python.action') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="(item, rowIdx) in col"
                :key="item.name"
                v-memo="[item.name, item.version, processing && processingAction === 'uninstall' && processingPackage === item.name]"
                :class="rowIdx % 2 !== 0 ? 'bg-white/3' : 'bg-transparent'"
                class="hover:bg-hovered/60"
              >
                <td class="border-b border-border px-4 py-1.5 font-mono text-[0.9rem] text-foreground break-all" :class="{ 'min-[900px]:pl-6': index === 1 }">{{ item.name }}</td>
                <td class="border-b border-border px-4 py-1.5 text-[0.9rem] text-foreground">{{ item.version }}</td>
                <td class="border-b border-border px-4 py-1.5 text-right" :class="{ 'min-[900px]:pr-6': index === 0 }">
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
        </div>

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
    </section>
  </UiPanel>
</template>
