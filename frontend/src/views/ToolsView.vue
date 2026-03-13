<script setup lang="ts">
import { onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import { useRouter } from 'vue-router'

import ToolList from '../components/ToolList.vue'
import { useToolsStore } from '../stores/tools'
import type { ToolItem } from '../types'

const router = useRouter()
const toolsStore = useToolsStore()
const {
  tools,
  loadingTools,
  updatingTool,
  deletingTools,
  editToolPathSelection,
  editToolPythonSelection,
} = storeToRefs(toolsStore)

onMounted(() => {
  if (!loadingTools.value && tools.value.length === 0) {
    toolsStore.fetchTools()
  }
})

function runToolDirect(tool: ToolItem): void {
  toolsStore.runToolInTerminal({
    toolId: tool.id,
    args: {},
    python: tool.type === 'python' ? tool.python : undefined,
  })
}
</script>

<template>
  <section class="h-full min-h-0 overflow-hidden p-3">
    <ToolList
      :tools="tools"
      :loading="loadingTools"
      :updating="updatingTool"
      :deleting="deletingTools"
      :edit-tool-path-selection="editToolPathSelection"
      :edit-tool-python-selection="editToolPythonSelection"
      @create-tool="router.push('/tools/new')"
      @refresh="toolsStore.fetchTools"
      @open-tool="(tool) => toolsStore.openTool(tool)"
      @run-tool="runToolDirect"
      @update-tool="toolsStore.updateTool"
      @delete-tools="toolsStore.deleteTools"
      @pick-edit-tool-path="({ defaultPath, toolType }) => toolsStore.pickEditToolPath(defaultPath, toolType)"
      @pick-edit-tool-python="({ defaultPath }) => toolsStore.pickEditToolPython(defaultPath)"
    />
  </section>
</template>
