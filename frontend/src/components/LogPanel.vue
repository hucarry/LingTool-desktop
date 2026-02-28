<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import type { LogEntry, RunInfo } from '../types'

const props = defineProps<{
  run: RunInfo | null
  logs: LogEntry[]
}>()

const emit = defineEmits<{
  (e: 'clear', runId: string): void
}>()

const autoScroll = ref(true)
const containerRef = ref<HTMLElement>()

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
    ElMessage.warning('暂无日志可复制')
    return
  }

  try {
    await navigator.clipboard.writeText(mergedText.value)
    ElMessage.success('日志已复制')
  } catch {
    ElMessage.error('复制失败，请检查剪贴板权限')
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
        <h3>日志输出</h3>
        <p v-if="run">
          {{ run.toolName }} | {{ run.status }}
          <span v-if="run.pid">(PID: {{ run.pid }})</span>
        </p>
        <p v-else>请选择一条运行记录</p>
      </div>

      <div class="log-actions">
        <el-switch v-model="autoScroll" inline-prompt active-text="自动滚动" inactive-text="手动" />
        <el-button size="small" @click="copyLogs">复制</el-button>
        <el-button size="small" @click="clearLogs" :disabled="!run">清空</el-button>
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
      <el-empty v-else description="当前没有日志" :image-size="72" />
    </div>
  </section>
</template>

<style scoped>
.log-panel {
  border-radius: var(--radius-lg);
  border: 1px solid var(--glass-border);
  background: rgba(255, 255, 255, 0.6);
  backdrop-filter: blur(12px);
  display: flex;
  flex-direction: column;
  min-height: 0;
  height: 100%;
  overflow: hidden;
  box-shadow: var(--shadow-sm);
}

.log-head {
  padding: 16px 20px;
  border-bottom: 1px solid var(--glass-border);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  background: rgba(255, 255, 255, 0.3);
}

.run-meta h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
}

.run-meta p {
  margin: 4px 0 0;
  font-size: 13px;
  color: var(--text-secondary);
  font-family: 'Fira Code', monospace;
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
  background: #0f172a; /* Slate 900 for modern terminal look */
  border-radius: 0 0 var(--radius-lg) var(--radius-lg);
  font-family: 'Fira Code', Consolas, Monaco, monospace;
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
  color: #64748b; /* Slate 500 */
  margin-right: 8px;
  user-select: none;
}

.channel {
  color: #94a3b8; /* Slate 400 */
  margin-right: 8px;
  user-select: none;
}

.stdout .line {
  color: #f8fafc; /* Slate 50 */
}

.stderr .line {
  color: #fca5a5; /* Red 300 */
}
</style>
