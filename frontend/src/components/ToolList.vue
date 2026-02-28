<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ToolItem } from '../types'
import { useI18n } from '../composables/useI18n'

const props = defineProps<{
  tools: ToolItem[]
  loading?: boolean
}>()

const emit = defineEmits<{
  (e: 'openTool', tool: ToolItem): void
  (e: 'runTool', tool: ToolItem): void
  (e: 'refresh'): void
}>()

const keyword = ref('')
const { t } = useI18n()

const filteredTools = computed(() => {
  const text = keyword.value.trim().toLowerCase()
  if (!text) {
    return props.tools
  }

  return props.tools.filter((tool) => {
    const hitName = tool.name.toLowerCase().includes(text)
    const hitTag = tool.tags.some((tag) => tag.toLowerCase().includes(text))
    return hitName || hitTag
  })
})

function openTool(tool: ToolItem): void {
  emit('openTool', tool)
}

function runTool(tool: ToolItem): void {
  emit('runTool', tool)
}
</script>

<template>
  <section class="tool-list">
    <header class="tool-list-header">
      <div>
        <h2>{{ t('tools.catalog') }}</h2>
        <p>{{ t('tools.items', { filtered: filteredTools.length, total: tools.length }) }}</p>
      </div>

      <el-button size="small" @click="emit('refresh')">{{ t('python.refresh') }}</el-button>
    </header>

    <el-input v-model="keyword" clearable :placeholder="t('tools.search')" />

    <div class="list-scroll">
      <el-scrollbar height="100%">
        <div class="tool-rows" v-loading="loading">
          <article
            v-for="tool in filteredTools"
            :key="tool.id"
            class="tool-row"
            role="button"
            tabindex="0"
            @click="openTool(tool)"
            @keydown.enter.prevent="openTool(tool)"
            @keydown.space.prevent="openTool(tool)"
          >
            <div class="row-main">
              <h3>{{ tool.name }}</h3>
              <p class="tool-id">{{ tool.id }}</p>
              <p class="tool-path" :title="tool.path">{{ tool.path }}</p>
            </div>

            <div class="row-meta">
              <span class="tool-type">{{ tool.type }}</span>
              <span class="tool-status" :class="{ invalid: !tool.valid }">
                {{ tool.valid ? t('tools.ready') : t('tools.invalidPath') }}
              </span>
            </div>

            <div class="row-actions">
              <div class="tool-tags">
                <span v-for="tag in tool.tags" :key="tag" class="tag">{{ tag }}</span>
              </div>
              <el-button
                type="primary"
                size="small"
                :disabled="!tool.valid"
                @click.stop="runTool(tool)"
              >
                {{ t('tools.run') }}
              </el-button>
            </div>
          </article>

          <el-empty v-if="filteredTools.length === 0 && !loading" :description="t('tools.noMatch')" />
        </div>
      </el-scrollbar>
    </div>
  </section>
</template>

<style scoped>
.tool-list {
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 12px;
}

.tool-list-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.tool-list-header h2 {
  font-size: 16px;
  font-weight: 600;
  color: var(--vscode-text-primary);
}

.tool-list-header p {
  margin-top: 3px;
  font-size: 12px;
  color: var(--vscode-text-muted);
}

.list-scroll {
  flex: 1;
  min-height: 0;
}

.tool-rows {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 4px 4px 10px;
}

.tool-row {
  border: 1px solid var(--vscode-border-color);
  background: #252526;
  border-radius: 3px;
  padding: 10px;
  display: grid;
  grid-template-columns: minmax(180px, 1.6fr) minmax(130px, 0.8fr) minmax(160px, 1.2fr);
  gap: 12px;
  align-items: center;
  cursor: pointer;
  transition: border-color 0.16s ease;
}

.tool-row:hover {
  border-color: var(--vscode-accent-color);
}

.row-main h3 {
  font-size: 14px;
  font-weight: 600;
  color: var(--vscode-text-primary);
}

.tool-id {
  margin-top: 2px;
  font-family: var(--vscode-font-mono);
  font-size: 11px;
  color: var(--vscode-text-muted);
}

.tool-path {
  margin-top: 8px;
  padding: 4px 6px;
  border: 1px solid var(--vscode-border-color);
  background: #1e1e1e;
  color: var(--vscode-text-muted);
  border-radius: 2px;
  font-family: var(--vscode-font-mono);
  font-size: 11px;
  white-space: nowrap;
  text-overflow: ellipsis;
  overflow: hidden;
}

.row-meta {
  display: flex;
  flex-direction: column;
  gap: 7px;
}

.tool-type,
.tool-status {
  display: inline-flex;
  align-items: center;
  width: fit-content;
  border: 1px solid var(--vscode-border-color);
  border-radius: 999px;
  padding: 2px 8px;
  font-size: 11px;
  color: var(--vscode-text-muted);
}

.tool-status {
  border-color: #2ea043;
  color: #73c991;
}

.tool-status.invalid {
  border-color: #f14c4c;
  color: #f48771;
}

.row-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.tool-tags {
  min-width: 0;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
}

.tag {
  display: inline-flex;
  align-items: center;
  height: 20px;
  padding: 0 8px;
  border-radius: 999px;
  font-size: 11px;
  color: var(--vscode-text-muted);
  background: #333333;
  border: 1px solid #4a4a4a;
}

@media (max-width: 1180px) {
  .tool-row {
    grid-template-columns: 1fr;
  }

  .row-actions {
    justify-content: flex-start;
  }
}
</style>
