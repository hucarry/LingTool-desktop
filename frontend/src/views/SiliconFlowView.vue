<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useToolHub } from '../composables/useToolHub'
import { useI18n } from '../composables/useI18n'

interface ModelRow {
  id: string
  status?: 'idle' | 'testing' | 'success' | 'fail'
  message?: string
}

const hub = useToolHub()
const { t } = useI18n()

const apiKey = ref('')
const models = ref<ModelRow[]>([])
const loading = ref(false)
const scanning = ref(false)
const scanIndex = ref(-1)

let lastBufferLength = 0

const stopWatch = watch(hub.activeTerminalOutputs, (newVal) => {
  if (newVal.length <= lastBufferLength) {
    return
  }

  const newLines = newVal.slice(lastBufferLength)
  lastBufferLength = newVal.length

  for (const line of newLines) {
    const text = line.trim()
    if (!text.startsWith('{') || !text.endsWith('}')) {
      continue
    }

    try {
      const data = JSON.parse(text)
      if (data.type === 'list' && Array.isArray(data.models)) {
        models.value = data.models.map((item: any) => ({
          id: item.id,
          status: 'idle',
          message: '',
        }))
        loading.value = false
        continue
      }

      if (data.type === 'result' && typeof data.id === 'string') {
        const index = models.value.findIndex((item) => item.id === data.id)
        if (index === -1) {
          continue
        }

        const current = models.value[index]
        if (!current) {
          continue
        }

        current.status = data.success ? 'success' : 'fail'
        current.message = typeof data.message === 'string' ? data.message : ''

        if (scanning.value) {
          requestAnimationFrame(() => {
            continueScan()
          })
        }
      }
    } catch {
      // Ignore malformed JSON chunks.
    }
  }
})

function hasActiveTerminal(): boolean {
  if (hub.activeTerminalId.value) {
    return true
  }

  ElMessage.warning(t('silicon.noActiveTerminal'))
  return false
}

function getModelList(): void {
  if (!apiKey.value.trim()) {
    ElMessage.warning(t('silicon.enterApiKey'))
    return
  }

  if (!hasActiveTerminal()) {
    return
  }

  loading.value = true
  models.value = []
  scanning.value = false
  scanIndex.value = -1

  lastBufferLength = hub.activeTerminalOutputs.value.length

  const cmd = `python Tools/model_scanner_cli.py list ${apiKey.value.trim()}\n`
  hub.sendTerminalInput({
    terminalId: hub.activeTerminalId.value,
    data: cmd,
  })
}

function startScan(): void {
  if (!hasActiveTerminal()) {
    return
  }

  if (models.value.length === 0) {
    ElMessage.warning(t('silicon.fetchBeforeScan'))
    return
  }

  scanning.value = true
  scanIndex.value = -1
  continueScan()
}

function continueScan(): void {
  if (!scanning.value) {
    return
  }

  scanIndex.value += 1
  if (scanIndex.value >= models.value.length) {
    scanning.value = false
    ElMessage.success(t('silicon.scanCompleted'))
    return
  }

  const model = models.value[scanIndex.value]
  if (!model) {
    scanning.value = false
    return
  }

  model.status = 'testing'
  model.message = ''

  const cmd = `python Tools/model_scanner_cli.py test ${apiKey.value.trim()} ${model.id}\n`
  hub.sendTerminalInput({
    terminalId: hub.activeTerminalId.value,
    data: cmd,
  })
}

function stopScan(): void {
  scanning.value = false
}

async function copyId(id: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(id)
    ElMessage.success(t('silicon.copySuccess', { id }))
  } catch {
    ElMessage.error(t('silicon.copyFailed'))
  }
}

onBeforeUnmount(() => {
  stopWatch()
})
</script>

<template>
  <section class="silicon-view">
    <header class="header">
      <div class="title-area">
        <h2>{{ t('silicon.title') }}</h2>
        <p>{{ t('silicon.desc') }}</p>
      </div>

      <div class="actions">
        <el-input v-model="apiKey" :placeholder="t('silicon.apiKey')" style="width: 300px" type="password" show-password />
        <el-button type="warning" :loading="loading" @click="getModelList">{{ t('silicon.fetchList') }}</el-button>
        <el-button type="primary" :disabled="models.length === 0 || scanning" @click="startScan">
          {{ t('silicon.startScan') }}
        </el-button>
        <el-button v-if="scanning" type="danger" @click="stopScan">{{ t('silicon.stop') }}</el-button>
      </div>
    </header>

    <div class="table-container">
      <el-table :data="models" stripe height="100%" @row-dblclick="(row: ModelRow) => copyId(row.id)">
        <el-table-column prop="id" :label="t('silicon.modelId')" min-width="320">
          <template #default="{ row }">
            <span class="model-id" :title="t('silicon.doubleClickCopy')">{{ row.id }}</span>
          </template>
        </el-table-column>

        <el-table-column prop="status" :label="t('silicon.status')" width="130">
          <template #default="{ row }">
            <el-tag v-if="row.status === 'success'" type="success">{{ t('silicon.status.success') }}</el-tag>
            <el-tag v-else-if="row.status === 'fail'" type="danger">{{ t('silicon.status.failed') }}</el-tag>
            <el-tag v-else-if="row.status === 'testing'" type="warning">{{ t('silicon.status.testing') }}</el-tag>
            <el-tag v-else type="info">{{ t('silicon.status.idle') }}</el-tag>
          </template>
        </el-table-column>

        <el-table-column prop="message" :label="t('silicon.responseError')" min-width="320" />
      </el-table>
    </div>
  </section>
</template>

<style scoped>
.silicon-view {
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 12px;
  border: 1px solid var(--vscode-border-color);
  background: var(--vscode-editor-bg);
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 14px;
}

.title-area h2 {
  font-size: 16px;
  font-weight: 600;
  color: var(--vscode-text-primary);
}

.title-area p {
  margin-top: 4px;
  font-size: 12px;
  color: var(--vscode-text-muted);
}

.actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.table-container {
  flex: 1;
  min-height: 0;
  border: 1px solid var(--vscode-border-color);
}

.model-id {
  font-family: var(--vscode-font-mono);
  font-size: 12px;
  cursor: pointer;
}

@media (max-width: 1200px) {
  .header {
    flex-direction: column;
  }

  .actions {
    width: 100%;
    flex-wrap: wrap;
  }
}
</style>
