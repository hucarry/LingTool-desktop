<script setup lang="ts">
import ToolList from '../components/ToolList.vue'
import { useToolHub } from '../composables/useToolHub'

const hub = useToolHub()
const tools = hub.tools
const loadingTools = hub.loadingTools
</script>

<template>
  <section class="tools-view">
    <ToolList
      :tools="tools"
      :loading="loadingTools"
      @refresh="hub.fetchTools"
      @open-tool="hub.openTool"
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
