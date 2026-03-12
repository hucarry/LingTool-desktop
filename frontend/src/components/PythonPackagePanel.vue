<script setup lang="ts">
import { computed, ref } from 'vue'
import type { PythonPackageItem } from '../types'
import { useI18n } from '../composables/useI18n'

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

const isDangerStatus = computed(() => {
  const text = props.statusText.toLowerCase()
  return text.includes('fail') || text.includes('失败')
})

function installPackage(): void {
  const name = packageToInstall.value.trim()
  if (!name) {
    return
  }

  emit('installPackage', name)
  packageToInstall.value = ''
}

function uninstallPackage(name: string): void {
  if (!name.trim()) {
    return
  }

  emit('uninstallPackage', name.trim())
}

function confirmUninstall(name: string): void {
  const confirmText = t('python.uninstallConfirm', { name })
  if (window.confirm(confirmText)) {
    uninstallPackage(name)
  }
}

function isRowBusy(name: string): boolean {
  if (!props.processing) {
    return false
  }

  return props.processingAction === 'uninstall' && props.processingPackage === name
}
</script>

<template>
  <section class="package-panel">
    <header class="panel-header">
      <div>
        <h2>{{ t('python.managerTitle') }}</h2>
        <p>{{ t('python.managerDesc') }}</p>
      </div>
      <d-button :loading="loading" @click="emit('refreshPackages')">{{ t('python.refresh') }}</d-button>
    </header>

    <div class="toolbar-row interpreter-row">
      <d-input :model-value="pythonPath || 'python'" readonly :placeholder="t('python.currentInterpreter')" />
      <d-button @click="emit('browsePython')">{{ t('python.browse') }}</d-button>
      <d-button @click="emit('useSystemPython')">{{ t('python.systemPython') }}</d-button>
    </div>

    <div class="toolbar-row install-row">
      <d-input
        v-model="packageToInstall"
        :placeholder="t('python.installInput')"
        @keyup.enter="installPackage"
      />
      <d-button
        color="primary"
        variant="solid"
        :loading="processing && processingAction === 'install'"
        @click="installPackage"
      >
        {{ t('python.install') }}
      </d-button>
    </div>

    <p class="status-text" :class="{ danger: isDangerStatus }">
      {{ statusText || t('python.ready') }}
    </p>

    <div class="toolbar-row search-row">
      <d-input v-model="packageKeyword" clearable :placeholder="t('python.searchInstalled')" />
      <span class="count">{{ filteredPackages.length }} / {{ packages.length }}</span>
    </div>

    <div class="table-wrap" v-loading="loading">
      <d-data-table :dataSource="filteredPackages" :scrollable="true" style="height: 100%;">
        <d-column field="name" :header="t('python.name')" :minWidth="220" />
        <d-column field="version" :header="t('python.version')" width="160" />
        <d-column :header="t('python.action')" width="130">
          <template #cell="scope">
            <d-button
              size="sm"
              color="danger"
              variant="outline"
              :disabled="processing && !isRowBusy(scope.rowItem.name)"
              @click="confirmUninstall(scope.rowItem.name)"
            >
              {{ t('python.uninstall') }}
            </d-button>
          </template>
        </d-column>
      </d-data-table>
    </div>
  </section>
</template>

<style scoped>
.package-panel {
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 12px;
  background: var(--vscode-editor-bg);
}

.panel-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 10px;
}

.panel-header h2 {
  font-size: 16px;
  font-weight: 600;
  color: var(--vscode-text-primary);
}

.panel-header p {
  margin-top: 4px;
  font-size: 12px;
  color: var(--vscode-text-muted);
}

.toolbar-row {
  display: grid;
  align-items: center;
  gap: 8px;
}

.interpreter-row {
  grid-template-columns: 1fr auto auto;
}

.install-row {
  grid-template-columns: 1fr auto;
}

.search-row {
  grid-template-columns: 1fr auto;
}

.count {
  font-size: 12px;
  color: var(--vscode-text-muted);
}

.status-text {
  margin: 0;
  font-size: 12px;
  color: #73c991;
}

.status-text.danger {
  color: #f48771;
}

.table-wrap {
  flex: 1;
  min-height: 0;
  border: 1px solid var(--vscode-border-color);
}

@media (max-width: 980px) {
  .interpreter-row {
    grid-template-columns: 1fr;
  }
}
</style>
