import { computed, nextTick, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import type { LogEntry, RunInfo } from '../types'
import { useI18n } from '../composables/useI18n'

const props = defineProps<{
  run: RunInfo | null
  logs: LogEntry[]
}>()

const emit = defineEmits<{
  (e: 'clear', runId: string): void
}>()

const autoScroll = ref(true)
const containerRef = ref<HTMLElement>()
const { locale } = useI18n()

const text = computed(() => {
  if (locale.value === 'zh-CN') {
    return {
      title: '日志输出',
      noRunSelected: '请选择一条运行记录',
      autoScrollOn: '自动滚动',
      autoScrollOff: '手动',
      copy: '复制',
      clear: '清空',
      noLogsCopy: '暂无日志可复制',
      copied: '日志已复制',
      copyFailed: '复制失败，请检查剪贴板权限',
      empty: '当前没有日志',
    }
  }

  return {
    title: 'Log Output',
    noRunSelected: 'Please select a run',
    autoScrollOn: 'Auto Scroll',
    autoScrollOff: 'Manual',
    copy: 'Copy',
    clear: 'Clear',
    noLogsCopy: 'No logs to copy',
    copied: 'Logs copied to clipboard',
    copyFailed: 'Copy failed, check clipboard permissions',
    empty: 'No logs available',
  }
})

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

async function copyLogs(): Promise<void> {
  if (!mergedText.value) {
    ElMessage.warning(text.value.noLogsCopy)
    return
  }

  try {
    await navigator.clipboard.writeText(mergedText.value)
    ElMessage.success(text.value.copied)
  } catch {
    ElMessage.error(text.value.copyFailed)
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
        <h3>{{ text.title }}</h3>
        <p v-if="run">
          {{ run.toolName }} | {{ run.status }}
          <span v-if="run.pid">(PID: {{ run.pid }})</span>
        </p>
        <p v-else>{{ text.noRunSelected }}</p>
      </div>

      <div class="log-actions">
        <el-switch v-model="autoScroll" inline-prompt :active-text="text.autoScrollOn" :inactive-text="text.autoScrollOff" />
        <el-button size="small" @click="copyLogs">{{ text.copy }}</el-button>
        <el-button size="small" @click="clearLogs" :disabled="!run">{{ text.clear }}</el-button>
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
      <el-empty v-else :description="text.empty" :image-size="72" />
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
</style>
