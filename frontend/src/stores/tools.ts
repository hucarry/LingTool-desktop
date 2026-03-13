import { defineStore } from 'pinia'
import { storeToRefs } from 'pinia'
import { ref, watch } from 'vue'

import { bridge } from '../services/bridge'
import { useI18n } from '../composables/useI18n'
import {
  getDefaultRuntimeForTool,
  getRuntimeBrowseFilter,
  getToolPathFilter,
  isScriptToolType,
  normalizeToolType,
} from '../utils/toolTypes'
import { useNotificationsStore } from './notifications'
import { useSettingsStore } from './settings'
import { useTerminalsStore } from './terminals'
import type {
  AddToolPayload,
  FileSelectedMessage,
  ToolAddedMessage,
  ToolsDeletedMessage,
  ToolItem,
  ToolsMessage,
  ToolUpdatedMessage,
} from '../types'

export const useToolsStore = defineStore('tools', () => {
  const tools = ref<ToolItem[]>([])
  const loadingTools = ref(false)
  const addingTool = ref(false)
  const updatingTool = ref(false)
  const deletingTools = ref(false)
  const addToolPathSelection = ref('')
  const addToolRuntimeSelection = ref('')
  const editToolPathSelection = ref('')
  const editToolRuntimeSelection = ref('')
  const lastAddedToolId = ref('')
  const lastUpdatedToolId = ref('')

  const activeTool = ref<ToolItem | null>(null)
  const runnerVisible = ref(false)
  const runtimeOverride = ref('')

  const { t } = useI18n()
  const notifications = useNotificationsStore()
  const settingsStore = useSettingsStore()
  const terminalsStore = useTerminalsStore()
  const { defaultPythonPath, defaultNodePath } = storeToRefs(settingsStore)

  watch(defaultPythonPath, (nextPath, previousPath) => {
    if (
      activeTool.value?.type === 'python'
      && !activeTool.value.runtimePath
      && runtimeOverride.value === (previousPath ?? '')
    ) {
      runtimeOverride.value = nextPath
    }
  })

  watch(defaultNodePath, (nextPath, previousPath) => {
    if (
      activeTool.value?.type === 'node'
      && !activeTool.value.runtimePath
      && runtimeOverride.value === (previousPath ?? '')
    ) {
      runtimeOverride.value = nextPath
    }
  })

  function beginLoadingTools(): void {
    loadingTools.value = true
  }

  function beginAddTool(): void {
    addingTool.value = true
    lastAddedToolId.value = ''
  }

  function beginUpdateTool(): void {
    updatingTool.value = true
    lastUpdatedToolId.value = ''
  }

  function beginDeleteTools(): void {
    deletingTools.value = true
  }

  function resetBusyStates(): void {
    loadingTools.value = false
    addingTool.value = false
    updatingTool.value = false
    deletingTools.value = false
  }

  function openTool(tool: ToolItem, appDefaultPythonPath = defaultPythonPath.value): void {
    const normalizedTool = {
      ...tool,
      type: normalizeToolType(tool.type),
    }
    activeTool.value = tool
    runtimeOverride.value = getDefaultRuntimeForTool(normalizedTool, {
      defaultPythonPath: appDefaultPythonPath,
      defaultNodePath: defaultNodePath.value,
    })
    runnerVisible.value = true
  }

  function setRuntimeOverride(path: string): void {
    runtimeOverride.value = path.trim()
  }

  function closeRunner(): void {
    runnerVisible.value = false
  }

  function fetchTools(): void {
    beginLoadingTools()
    bridge.send({ type: 'getTools' })
  }

  function addTool(tool: AddToolPayload): void {
    beginAddTool()
    bridge.send({
      type: 'addTool',
      tool,
    })
  }

  function updateTool(tool: AddToolPayload): void {
    beginUpdateTool()
    bridge.send({
      type: 'updateTool',
      tool,
    })
  }

  function deleteTools(toolIds: string[]): void {
    const normalized = toolIds
      .map((item) => item.trim())
      .filter((item, index, array) => item.length > 0 && array.indexOf(item) === index)

    if (normalized.length === 0) {
      return
    }

    beginDeleteTools()
    bridge.send({
      type: 'deleteTools',
      toolIds: normalized,
    })
  }

  function pickAddToolPath(defaultPath?: string, toolType?: string): void {
    const filter = getToolPathFilter(toolType ?? '')
    if (!filter) {
      return
    }

    bridge.send({
      type: 'browseFile',
      defaultPath,
      filter,
      purpose: 'addToolPath',
    })
  }

  function pickAddToolRuntime(defaultPath?: string, toolType?: string): void {
    const normalizedType = normalizeToolType(toolType ?? '')
    if (normalizedType === 'python') {
      bridge.send({
        type: 'browsePython',
        defaultPath,
        purpose: 'addToolRuntime',
      })
      return
    }

    const filter = getRuntimeBrowseFilter(normalizedType)
    if (!filter) {
      return
    }

    bridge.send({
      type: 'browseFile',
      defaultPath,
      filter,
      purpose: 'addToolRuntime',
    })
  }

  function pickEditToolPath(defaultPath?: string, toolType?: string): void {
    const filter = getToolPathFilter(toolType ?? '')
    if (!filter) {
      return
    }

    bridge.send({
      type: 'browseFile',
      defaultPath,
      filter,
      purpose: 'editToolPath',
    })
  }

  function pickEditToolRuntime(defaultPath?: string, toolType?: string): void {
    const normalizedType = normalizeToolType(toolType ?? '')
    if (normalizedType === 'python') {
      bridge.send({
        type: 'browsePython',
        defaultPath,
        purpose: 'editToolRuntime',
      })
      return
    }

    const filter = getRuntimeBrowseFilter(normalizedType)
    if (!filter) {
      return
    }

    bridge.send({
      type: 'browseFile',
      defaultPath,
      filter,
      purpose: 'editToolRuntime',
    })
  }

  function pickRuntimePath(): void {
    const tool = activeTool.value
    if (!tool || !isScriptToolType(tool.type)) {
      return
    }

    const defaultPath = runtimeOverride.value
      || tool.runtimePath
      || getDefaultRuntimeForTool(tool, {
        defaultPythonPath: defaultPythonPath.value,
        defaultNodePath: defaultNodePath.value,
      })
      || tool.cwd
      || tool.path

    if (tool.type === 'python') {
      bridge.send({
        type: 'browsePython',
        defaultPath,
        purpose: 'toolRunnerRuntime',
      })
      return
    }

    bridge.send({
      type: 'browseFile',
      defaultPath,
      filter: getRuntimeBrowseFilter(tool.type),
      purpose: 'toolRunnerRuntime',
    })
  }

  function openUrlTool(toolId: string): void {
    bridge.send({
      type: 'openUrlTool',
      toolId,
    })
  }

  function runToolInTerminal(payload: { toolId: string; args: Record<string, string>; runtimePath?: string }): void {
    const tool = tools.value.find((item) => item.id === payload.toolId)
    const effectiveRuntime = tool && isScriptToolType(tool.type)
      ? (payload.runtimePath?.trim()
        || getDefaultRuntimeForTool(tool, {
          defaultPythonPath: defaultPythonPath.value,
          defaultNodePath: defaultNodePath.value,
        })
        || undefined)
      : undefined

    bridge.send({
      type: 'runToolInTerminal',
      toolId: payload.toolId,
      args: payload.args,
      runtimePath: effectiveRuntime,
      terminalId: terminalsStore.activeTerminalId || undefined,
    })
  }

  function handleToolsMessage(message: ToolsMessage): void {
    tools.value = message.tools
    resetBusyStates()
  }

  function handleFileSelectedMessage(message: FileSelectedMessage): void {
    if (!message.path?.trim()) {
      return
    }

    if (message.purpose === 'addToolPath') {
      addToolPathSelection.value = message.path
      return
    }

    if (message.purpose === 'addToolRuntime') {
      addToolRuntimeSelection.value = message.path
      return
    }

    if (message.purpose === 'editToolPath') {
      editToolPathSelection.value = message.path
      return
    }

    if (message.purpose === 'editToolRuntime') {
      editToolRuntimeSelection.value = message.path
      return
    }

    if (message.purpose === 'toolRunnerRuntime') {
      runtimeOverride.value = message.path
    }
  }

  function handleToolAddedMessage(message: ToolAddedMessage): void {
    addingTool.value = false
    lastAddedToolId.value = message.toolId

    notifications.success(t('tools.added', { toolId: message.toolId }), {
      groupKey: 'tools.added',
    })
  }

  function handleToolUpdatedMessage(message: ToolUpdatedMessage): void {
    updatingTool.value = false
    lastUpdatedToolId.value = message.toolId

    notifications.success(t('tools.updated', { toolId: message.toolId }), {
      groupKey: 'tools.updated',
    })
  }

  function handleToolsDeletedMessage(message: ToolsDeletedMessage): void {
    deletingTools.value = false

    notifications.success(t('tools.deleted', { count: message.deletedCount }), {
      groupKey: 'tools.deleted',
    })
  }

  function handleRuntimeSelected(path?: string): void {
    if (!path?.trim()) {
      return
    }

    runtimeOverride.value = path
  }

  return {
    tools,
    loadingTools,
    addingTool,
    updatingTool,
    deletingTools,
    addToolPathSelection,
    addToolRuntimeSelection,
    editToolPathSelection,
    editToolRuntimeSelection,
    lastAddedToolId,
    lastUpdatedToolId,
    activeTool,
    runnerVisible,
    runtimeOverride,
    closeRunner,
    beginLoadingTools,
    beginAddTool,
    beginUpdateTool,
    beginDeleteTools,
    resetBusyStates,
    fetchTools,
    addTool,
    updateTool,
    deleteTools,
    pickAddToolPath,
    pickAddToolRuntime,
    pickEditToolPath,
    pickEditToolRuntime,
    openTool,
    setRuntimeOverride,
    pickRuntimePath,
    openUrlTool,
    runToolInTerminal,
    handleToolsMessage,
    handleFileSelectedMessage,
    handleToolAddedMessage,
    handleToolUpdatedMessage,
    handleToolsDeletedMessage,
    handleRuntimeSelected,
  }
})
