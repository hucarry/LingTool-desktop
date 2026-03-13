<script setup lang="ts">
import { onBeforeUnmount, onMounted } from 'vue'
import { storeToRefs } from 'pinia'

import NotificationViewport from './components/NotificationViewport.vue'
import ToolRunner from './components/ToolRunner.vue'
import WorkbenchLayout from './components/layout/WorkbenchLayout.vue'
import { useBridgeBootstrap } from './composables/useBridgeBootstrap'
import { useSettingsStore } from './stores/settings'
import { useToolsStore } from './stores/tools'

const bootstrap = useBridgeBootstrap()
const toolsStore = useToolsStore()
const settingsStore = useSettingsStore()
const { runnerVisible, pythonOverride, activeTool } = storeToRefs(toolsStore)
const { defaultPythonPath } = storeToRefs(settingsStore)

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
    v-model:python-override="pythonOverride"
    :tool="activeTool"
    :default-python-path="defaultPythonPath"
    @pick-python="toolsStore.pickPythonInterpreter"
    @run="toolsStore.runToolInTerminal"
  />

  <NotificationViewport />
</template>
