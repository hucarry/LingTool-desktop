<script setup lang="ts">
import ToolList from '../components/ToolList.vue'
import { useToolHub } from '../composables/useToolHub'
import type { AddToolPayload, ToolItem } from '../types'

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

function updateTool(payload: AddToolPayload): void {
  hub.updateTool(payload)
}

function deleteTools(toolIds: string[]): void {
  hub.deleteTools(toolIds)
}
</script>

<template>
  <section class="tools-view">
    <ToolList
      :tools="tools"
      :loading="loadingTools"
      :updating="hub.updatingTool.value"
      :deleting="hub.deletingTools.value"
      :edit-tool-path-selection="hub.editToolPathSelection.value"
      :edit-tool-python-selection="hub.editToolPythonSelection.value"
      @refresh="hub.fetchTools"
      @open-tool="hub.openTool"
      @run-tool="runToolDirect"
      @update-tool="updateTool"
      @delete-tools="deleteTools"
      @pick-edit-tool-path="({ defaultPath, toolType }) => hub.pickEditToolPath(defaultPath, toolType)"
      @pick-edit-tool-python="({ defaultPath }) => hub.pickEditToolPython(defaultPath)"
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
