<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { Button as DButton } from 'vue-devui/button'
import { Switch as DSwitch } from 'vue-devui/switch'
import 'vue-devui/button/style.css'
import 'vue-devui/switch/style.css'
import type { LogEntry, RunInfo } from '../types'
import { useI18n } from '../composables/useI18n'
import { useNotify } from '../composables/useNotify'

const props = defineProps<{
  run: RunInfo | null
  logs: LogEntry[]
}>()

const emit = defineEmits<{
  (e: 'clear', runId: string): void
}>()

const autoScroll = ref(true)
const containerRef = ref<HTMLElement>()
const { t } = useI18n()

const mergedText = computed(() =>
  props.logs
    .map((log) => `[${new Date(log.ts).toLocaleTimeString()}][${log.channel}] ${log.line}`)
    .join('\n'),
)

watch(
  () => props.logs.length,
  async () => {
    if (!autoScroll.value) {
      return
    }

    await nextTick()
    const container = containerRef.value
    if (container) {
      container.scrollTop = container.scrollHeight
    }
  },
)

const notify = useNotify()

async function copyLogs(): Promise<void> {
  if (!mergedText.value) {
    notify.info(t('log.noLogsCopy'))
    return
  }

  try {
    await navigator.clipboard.writeText(mergedText.value)
    notify.success(t('log.copied'))
  } catch {
    notify.error(t('log.copyFailed'))
  }
}

function clearLogs(): void {
  if (!props.run) {
    return
  }

  emit('clear', props.run.runId)
}
</script>

<template>
  <section class="log-panel">
    <header class="log-head">
      <div class="run-meta">
        <h3>{{ t('log.title') }}</h3>
        <p v-if="run">
          {{ run.toolName }} | {{ run.status }}
          <span v-if="run.pid">(PID: {{ run.pid }})</span>
        </p>
        <p v-else>{{ t('log.noRunSelected') }}</p>
      </div>

      <div class="log-actions">
        <d-switch v-model="autoScroll">
          <template #checked>{{ t('log.autoScrollOn') }}</template>
          <template #unchecked>{{ t('log.autoScrollOff') }}</template>
        </d-switch>
        <d-button size="sm" @click="copyLogs">{{ t('log.copy') }}</d-button>
        <d-button size="sm" @click="clearLogs" :disabled="!run">{{ t('log.clear') }}</d-button>
      </div>
    </header>

    <div ref="containerRef" class="log-body">
      <template v-if="logs.length > 0">
        <p
          v-for="(log, index) in logs"
          :key="`${log.runId}-${index}`"
          :class="['log-line', log.channel === 'stderr' ? 'stderr' : 'stdout']"
        >
          <span class="time">{{ new Date(log.ts).toLocaleTimeString() }}</span>
          <span class="channel">{{ log.channel }}</span>
          <span class="line">{{ log.line }}</span>
        </p>
      </template>
      <div v-else class="empty-state">
        <i class="icon-refresh" style="font-size: 48px; color: var(--vscode-text-muted); opacity: 0.5;"></i>
        <p>{{ t('log.empty') }}</p>
      </div>
    </div>
  </section>
</template>

<style scoped>
.log-panel {
  border-radius: var(--radius-lg);
  display: flex;
  flex-direction: column;
  min-height: 0;
  height: 100%;
  overflow: hidden;
  background: var(--vscode-editor-bg);
}

.log-head {
  padding: 16px 20px;
  border-bottom: 1px solid var(--vscode-border-color);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  background: var(--vscode-sidebar-bg);
}

.run-meta h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: var(--vscode-text-primary);
}

.run-meta p {
  margin: 4px 0 0;
  font-size: 13px;
  color: var(--vscode-text-muted);
  font-family: var(--vscode-font-mono);
}

.log-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.log-body {
  flex: 1;
  overflow: auto;
  padding: 16px 20px;
  background: var(--vscode-editor-bg);
  border-radius: 0 0 var(--radius-lg) var(--radius-lg);
  font-family: var(--vscode-font-mono);
}

.log-line {
  margin: 0;
  line-height: 1.6;
  word-break: break-all;
  white-space: pre-wrap;
  font-size: 13px;
}

.log-line + .log-line {
  margin-top: 4px;
}

.time {
  color: var(--vscode-text-muted);
  margin-right: 8px;
  user-select: none;
}

.channel {
  color: var(--vscode-text-muted);
  margin-right: 8px;
  user-select: none;
  opacity: 0.8;
}

.stdout .line {
  color: var(--vscode-text-primary);
}

.stderr .line {
  color: var(--el-color-danger);
}
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: var(--vscode-text-muted);
  gap: 12px;
}
</style>
