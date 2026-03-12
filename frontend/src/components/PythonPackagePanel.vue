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
  return text.includes('fail') || text.includes('failed') || text.includes('失败')
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
      <button class="panel-button" type="button" :disabled="loading" @click="emit('refreshPackages')">
        {{ t('python.refresh') }}
      </button>
    </header>

    <div class="toolbar-row interpreter-row">
      <input class="field-input" :value="pythonPath || 'python'" readonly :placeholder="t('python.currentInterpreter')" />
      <button class="panel-button" type="button" @click="emit('browsePython')">{{ t('python.browse') }}</button>
      <button class="panel-button" type="button" @click="emit('useSystemPython')">{{ t('python.systemPython') }}</button>
    </div>

    <div class="toolbar-row install-row">
      <input
        v-model="packageToInstall"
        class="field-input"
        type="text"
        :placeholder="t('python.installInput')"
        @keyup.enter="installPackage"
      />
      <button
        class="panel-button primary"
        type="button"
        :disabled="processing && processingAction === 'install'"
        @click="installPackage"
      >
        {{ t('python.install') }}
      </button>
    </div>

    <p class="status-text" :class="{ danger: isDangerStatus }">
      {{ statusText || t('python.ready') }}
    </p>

    <div class="toolbar-row search-row">
      <label class="search-box">
        <span class="search-icon" aria-hidden="true">/</span>
        <input v-model="packageKeyword" type="search" :placeholder="t('python.searchInstalled')" />
      </label>
      <span class="count">{{ filteredPackages.length }} / {{ packages.length }}</span>
    </div>

    <div class="table-wrap">
      <div v-if="loading" class="loading-overlay">
        <span class="loading-spinner" />
      </div>

      <table v-else-if="filteredPackages.length > 0" class="package-table">
        <thead>
          <tr>
            <th>{{ t('python.name') }}</th>
            <th>{{ t('python.version') }}</th>
            <th>{{ t('python.action') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in filteredPackages" :key="item.name">
            <td class="package-name">{{ item.name }}</td>
            <td>{{ item.version }}</td>
            <td class="action-cell">
              <button
                class="panel-button danger"
                type="button"
                :disabled="processing && !isRowBusy(item.name)"
                @click="confirmUninstall(item.name)"
              >
                {{ t('python.uninstall') }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>

      <div v-else class="empty-state">
        <p class="empty-state-title">{{ t('python.noPackageData') }}</p>
      </div>
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

.panel-button {
  height: 32px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 4px;
  background: var(--vscode-sidebar-bg);
  color: var(--vscode-text-primary);
  padding: 0 12px;
  cursor: pointer;
}

.panel-button:hover:not(:disabled) {
  border-color: var(--vscode-accent-color);
  background: var(--vscode-hover-bg);
}

.panel-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.panel-button.primary {
  border-color: var(--vscode-accent-color);
  background: var(--vscode-accent-color);
  color: #ffffff;
}

.panel-button.danger:hover:not(:disabled) {
  border-color: var(--el-color-danger);
  color: var(--el-color-danger);
}

.field-input,
.search-box input {
  width: 100%;
  height: 34px;
  border: 1px solid var(--vscode-border-color);
  border-radius: 6px;
  background: var(--vscode-sidebar-bg);
  color: var(--vscode-text-primary);
  padding: 0 12px;
}

.search-box {
  position: relative;
  display: flex;
  align-items: center;
}

.search-box input {
  padding-left: 34px;
}

.search-icon {
  position: absolute;
  left: 12px;
  color: var(--vscode-text-muted);
  font-size: 12px;
  pointer-events: none;
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
  position: relative;
  flex: 1;
  min-height: 0;
  border: 1px solid var(--vscode-border-color);
  border-radius: 8px;
  overflow: auto;
  background: var(--vscode-sidebar-bg);
}

.package-table {
  width: 100%;
  border-collapse: collapse;
}

.package-table th,
.package-table td {
  padding: 10px 12px;
  border-bottom: 1px solid var(--vscode-border-color);
  text-align: left;
  font-size: 12px;
}

.package-table th {
  position: sticky;
  top: 0;
  background: var(--vscode-editor-bg);
  color: var(--vscode-text-muted);
  z-index: 1;
}

.package-name {
  font-family: var(--vscode-font-mono);
  color: var(--vscode-text-primary);
}

.action-cell {
  width: 140px;
}

.empty-state {
  min-height: 220px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.empty-state-title {
  color: var(--vscode-text-muted);
  font-size: 13px;
}

.loading-overlay {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: color-mix(in srgb, var(--vscode-editor-bg) 62%, transparent);
  backdrop-filter: blur(2px);
}

.loading-spinner {
  width: 28px;
  height: 28px;
  border: 2px solid color-mix(in srgb, var(--vscode-border-color) 70%, transparent);
  border-top-color: var(--vscode-accent-color);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 980px) {
  .interpreter-row,
  .install-row,
  .search-row {
    grid-template-columns: 1fr;
  }
}
</style>
