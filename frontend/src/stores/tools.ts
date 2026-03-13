import { defineStore } from 'pinia'
import { storeToRefs } from 'pinia'
import { ref, watch } from 'vue'

import { bridge } from '../services/bridge'
import { useI18n } from '../composables/useI18n'
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
  const addToolPythonSelection = ref('')
  const editToolPathSelection = ref('')
  const editToolPythonSelection = ref('')
  const lastAddedToolId = ref('')
  const lastUpdatedToolId = ref('')

  const activeTool = ref<ToolItem | null>(null)
  const runnerVisible = ref(false)
  const pythonOverride = ref('')

  const { t } = useI18n()
  const notifications = useNotificationsStore()
  const settingsStore = useSettingsStore()
  const terminalsStore = useTerminalsStore()
  const { defaultPythonPath } = storeToRefs(settingsStore)

  watch(defaultPythonPath, (nextPath, previousPath) => {
    if (
      activeTool.value?.type === 'python'
      && !activeTool.value.python
      && pythonOverride.value === (previousPath ?? '')
    ) {
      pythonOverride.value = nextPath
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
    activeTool.value = tool
    pythonOverride.value = tool.type === 'python'
      ? (tool.python ?? appDefaultPythonPath)
      : ''
    runnerVisible.value = true
  }

  function setPythonOverride(path: string): void {
    pythonOverride.value = path.trim()
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
    bridge.send({
      type: 'browseFile',
      defaultPath,
      filter: toolType === 'python' ? '*.py' : '*.exe',
      purpose: 'addToolPath',
    })
  }

  function pickAddToolPython(defaultPath?: string): void {
    bridge.send({
      type: 'browseFile',
      defaultPath,
      filter: '*.exe',
      purpose: 'addToolPython',
    })
  }

  function pickEditToolPath(defaultPath?: string, toolType?: string): void {
    bridge.send({
      type: 'browseFile',
      defaultPath,
      filter: toolType === 'python' ? '*.py' : '*.exe',
      purpose: 'editToolPath',
    })
  }

  function pickEditToolPython(defaultPath?: string): void {
    bridge.send({
      type: 'browseFile',
      defaultPath,
      filter: '*.exe',
      purpose: 'editToolPython',
    })
  }

  function pickPythonInterpreter(): void {
    const tool = activeTool.value
    if (tool?.type !== 'python') {
      return
    }

    bridge.send({
      type: 'browsePython',
      defaultPath: pythonOverride.value
        || tool.python
        || defaultPythonPath.value
        || tool.cwd
        || tool.path,
      purpose: 'toolRunner',
    })
  }

  function runToolInTerminal(payload: { toolId: string; args: Record<string, string>; python?: string }): void {
    const tool = tools.value.find((item) => item.id === payload.toolId)
    const effectivePython = payload.python?.trim()
      || (tool?.type === 'python'
        ? (tool.python?.trim() || defaultPythonPath.value.trim() || undefined)
        : undefined)

    bridge.send({
      type: 'runToolInTerminal',
      toolId: payload.toolId,
      args: payload.args,
      python: effectivePython,
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

    if (message.purpose === 'addToolPython') {
      addToolPythonSelection.value = message.path
      return
    }

    if (message.purpose === 'editToolPath') {
      editToolPathSelection.value = message.path
      return
    }

    if (message.purpose === 'editToolPython') {
      editToolPythonSelection.value = message.path
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

  function handlePythonSelected(path?: string): void {
    if (!path?.trim()) {
      return
    }

    pythonOverride.value = path
  }

  return {
    tools,
    loadingTools,
    addingTool,
    updatingTool,
    deletingTools,
    addToolPathSelection,
    addToolPythonSelection,
    editToolPathSelection,
    editToolPythonSelection,
    lastAddedToolId,
    lastUpdatedToolId,
    activeTool,
    runnerVisible,
    pythonOverride,
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
    pickAddToolPython,
    pickEditToolPath,
    pickEditToolPython,
    openTool,
    setPythonOverride,
    pickPythonInterpreter,
    runToolInTerminal,
    handleToolsMessage,
    handleFileSelectedMessage,
    handleToolAddedMessage,
    handleToolUpdatedMessage,
    handleToolsDeletedMessage,
    handlePythonSelected,
  }
})
