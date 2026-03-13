<script setup lang="ts">
import { onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import { useRouter } from 'vue-router'

import ToolList from '../components/ToolList.vue'
import { useToolsStore } from '../stores/tools'
import type { ToolItem } from '../types'
import { isUrlToolType } from '../utils/toolTypes'

const router = useRouter()
const toolsStore = useToolsStore()
const {
  tools,
  loadingTools,
  updatingTool,
  deletingTools,
  editToolPathSelection,
  editToolRuntimeSelection,
} = storeToRefs(toolsStore)

onMounted(() => {
  if (!loadingTools.value && tools.value.length === 0) {
    toolsStore.fetchTools()
  }
})

function runToolDirect(tool: ToolItem): void {
  if (isUrlToolType(tool.type)) {
    toolsStore.openUrlTool(tool.id)
    return
  }

  toolsStore.runToolInTerminal({
    toolId: tool.id,
    args: {},
    runtimePath: tool.runtimePath,
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
      :edit-tool-runtime-selection="editToolRuntimeSelection"
      @create-tool="router.push('/tools/new')"
      @refresh="toolsStore.fetchTools"
      @open-tool="(tool) => toolsStore.openTool(tool)"
      @run-tool="runToolDirect"
      @update-tool="toolsStore.updateTool"
      @delete-tools="toolsStore.deleteTools"
      @pick-edit-tool-path="({ defaultPath, toolType }) => toolsStore.pickEditToolPath(defaultPath, toolType)"
      @pick-edit-tool-runtime="({ defaultPath, toolType }) => toolsStore.pickEditToolRuntime(defaultPath, toolType)"
    />
  </section>
</template>
