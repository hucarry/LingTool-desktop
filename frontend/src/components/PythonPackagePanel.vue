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
      <el-button :loading="loading" @click="emit('refreshPackages')">{{ t('python.refresh') }}</el-button>
    </header>

    <div class="toolbar-row interpreter-row">
      <el-input :model-value="pythonPath || 'python'" readonly :placeholder="t('python.currentInterpreter')" />
      <el-button @click="emit('browsePython')">{{ t('python.browse') }}</el-button>
      <el-button @click="emit('useSystemPython')">{{ t('python.systemPython') }}</el-button>
    </div>

    <div class="toolbar-row install-row">
      <el-input
        v-model="packageToInstall"
        :placeholder="t('python.installInput')"
        @keyup.enter="installPackage"
      />
      <el-button
        type="primary"
        :loading="processing && processingAction === 'install'"
        @click="installPackage"
      >
        {{ t('python.install') }}
      </el-button>
    </div>

    <p class="status-text" :class="{ danger: isDangerStatus }">
      {{ statusText || t('python.ready') }}
    </p>

    <div class="toolbar-row search-row">
      <el-input v-model="packageKeyword" clearable :placeholder="t('python.searchInstalled')" />
      <span class="count">{{ filteredPackages.length }} / {{ packages.length }}</span>
    </div>

    <div class="table-wrap">
      <el-table
        v-loading="loading"
        :data="filteredPackages"
        stripe
        border
        height="100%"
        :empty-text="t('python.noPackageData')"
      >
        <el-table-column prop="name" :label="t('python.name')" min-width="220" />
        <el-table-column prop="version" :label="t('python.version')" width="160" />
        <el-table-column :label="t('python.action')" width="130" fixed="right">
          <template #default="{ row }">
            <el-popconfirm
              :title="t('python.uninstallConfirm', { name: row.name })"
              :confirm-button-text="t('python.uninstall')"
              :cancel-button-text="t('python.cancel')"
              @confirm="uninstallPackage(row.name)"
            >
              <template #reference>
                <el-button
                  size="small"
                  type="danger"
                  plain
                  :loading="isRowBusy(row.name)"
                  :disabled="processing && !isRowBusy(row.name)"
                >
                  {{ t('python.uninstall') }}
                </el-button>
              </template>
            </el-popconfirm>
          </template>
        </el-table-column>
      </el-table>
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
