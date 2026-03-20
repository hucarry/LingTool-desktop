import { defineStore } from 'pinia'
import { storeToRefs } from 'pinia'
import { ref } from 'vue'

import { bridge } from '../services/bridge'
import { useI18n } from '../composables/useI18n'
import { useNotificationsStore } from './notifications'
import { useSettingsStore } from './settings'
import { useTerminalsStore } from './terminals'
import { createToolBrowseActions } from './tools/createToolBrowseActions'
import { createToolMutationFeedback } from './tools/createToolMutationFeedback'
import { useToolRunnerState } from './tools/useToolRunnerState'
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

  const { t } = useI18n()
  const notifications = useNotificationsStore()
  const settingsStore = useSettingsStore()
  const terminalsStore = useTerminalsStore()
  const { defaultPythonPath, defaultNodePath } = storeToRefs(settingsStore)
  const { activeTerminalId } = storeToRefs(terminalsStore)

  const runnerState = useToolRunnerState({
    tools,
    activeTerminalId,
    defaultPythonPath,
    defaultNodePath,
  })
  const browseActions = createToolBrowseActions({
    addToolPathSelection,
    addToolRuntimeSelection,
    editToolPathSelection,
    editToolRuntimeSelection,
    runtimeOverride: runnerState.runtimeOverride,
  })
  const mutationFeedback = createToolMutationFeedback({
    addingTool,
    updatingTool,
    deletingTools,
    lastAddedToolId,
    lastUpdatedToolId,
    notifications,
    t,
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
    runnerState.openTool(tool, appDefaultPythonPath)
  }

  function setRuntimeOverride(path: string): void {
    runnerState.setRuntimeOverride(path)
  }

  function closeRunner(): void {
    runnerState.closeRunner()
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
    browseActions.pickAddToolPath(defaultPath, toolType)
  }

  function pickAddToolRuntime(defaultPath?: string, toolType?: string): void {
    browseActions.pickAddToolRuntime(defaultPath, toolType)
  }

  function pickEditToolPath(defaultPath?: string, toolType?: string): void {
    browseActions.pickEditToolPath(defaultPath, toolType)
  }

  function pickEditToolRuntime(defaultPath?: string, toolType?: string): void {
    browseActions.pickEditToolRuntime(defaultPath, toolType)
  }

  function pickRuntimePath(): void {
    runnerState.pickRuntimePath()
  }

  function openUrlTool(toolId: string): void {
    bridge.send({
      type: 'openUrlTool',
      toolId,
    })
  }

  function runToolInTerminal(payload: { toolId: string; args: Record<string, string>; runtimePath?: string }): void {
    runnerState.runToolInTerminal(payload)
  }

  function handleToolsMessage(message: ToolsMessage): void {
    tools.value = message.tools
    resetBusyStates()
  }

  function handleFileSelectedMessage(message: FileSelectedMessage): void {
    browseActions.handleFileSelectedMessage(message)
  }

  function handleToolAddedMessage(message: ToolAddedMessage): void {
    mutationFeedback.handleToolAddedMessage(message)
  }

  function handleToolUpdatedMessage(message: ToolUpdatedMessage): void {
    mutationFeedback.handleToolUpdatedMessage(message)
  }

  function handleToolsDeletedMessage(message: ToolsDeletedMessage): void {
    mutationFeedback.handleToolsDeletedMessage(message)
  }

  function handleRuntimeSelected(path?: string): void {
    runnerState.handleRuntimeSelected(path)
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
    activeTool: runnerState.activeTool,
    runnerVisible: runnerState.runnerVisible,
    runtimeOverride: runnerState.runtimeOverride,
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
