<script setup lang="ts">
import ToolList from '../components/ToolList.vue'
import { useToolHub } from '../composables/useToolHub'
import type { ToolItem } from '../types'

const hub = useToolHub()
const tools = hub.tools
const loadingTools = hub.loadingTools

function runToolDirect(tool: ToolItem): void {
  hub.handleRun({
    toolId: tool.id,
    args: {},
    python: tool.type === 'python' ? tool.python : undefined,
  })
}
</script>

<template>
  <section class="tools-view">
    <ToolList
      :tools="tools"
      :loading="loadingTools"
      @refresh="hub.fetchTools"
      @open-tool="hub.openTool"
      @run-tool="runToolDirect"
    />
  </section>
</template>

<style scoped>
.tools-view {
  height: 100%;
  min-height: 0;
  border: 1px solid var(--vscode-border-color);
  background: var(--vscode-editor-bg);
  overflow: hidden;
}
</style>
