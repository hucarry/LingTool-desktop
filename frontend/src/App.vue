<script setup lang="ts">
import { onBeforeUnmount, onMounted } from 'vue'
import { storeToRefs } from 'pinia'

import NotificationViewport from './components/NotificationViewport.vue'
import WorkbenchLayout from './components/layout/WorkbenchLayout.vue'
import { defineAsyncComponent } from 'vue'

const ToolRunner = defineAsyncComponent(() => import('./components/ToolRunner.vue'))
import { useBridgeBootstrap } from './composables/useBridgeBootstrap'
import { useSettingsStore } from './stores/settings'
import { useToolsStore } from './stores/tools'

const bootstrap = useBridgeBootstrap()
const toolsStore = useToolsStore()
const settingsStore = useSettingsStore()
const { runnerVisible, runtimeOverride, activeTool } = storeToRefs(toolsStore)
const { defaultPythonPath, defaultNodePath } = storeToRefs(settingsStore)

onMounted(() => {
  bootstrap.init()
})

onBeforeUnmount(() => {
  bootstrap.dispose()
})
</script>

<template>
  <WorkbenchLayout />

  <ToolRunner
    v-model:visible="runnerVisible"
    v-model:runtime-override="runtimeOverride"
    :tool="activeTool"
    :default-python-path="defaultPythonPath"
    :default-node-path="defaultNodePath"
    @pick-runtime="toolsStore.pickRuntimePath"
    @run="toolsStore.runToolInTerminal"
    @open-url="toolsStore.openUrlTool"
  />

  <NotificationViewport />
</template>
