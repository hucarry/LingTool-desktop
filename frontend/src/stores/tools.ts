import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

import { useI18n } from '../composables/useI18n'
import { useNotify } from '../composables/useNotify'
import { useSettings } from '../composables/useSettings'
import type {
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
  const notify = useNotify()
  const { defaultPythonPath } = useSettings()

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

    notify.success(t('tools.added', { toolId: message.toolId }), {
      groupKey: 'tools.added',
    })
  }

  function handleToolUpdatedMessage(message: ToolUpdatedMessage): void {
    updatingTool.value = false
    lastUpdatedToolId.value = message.toolId

    notify.success(t('tools.updated', { toolId: message.toolId }), {
      groupKey: 'tools.updated',
    })
  }

  function handleToolsDeletedMessage(message: ToolsDeletedMessage): void {
    deletingTools.value = false

    notify.success(t('tools.deleted', { count: message.deletedCount }), {
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
    beginLoadingTools,
    beginAddTool,
    beginUpdateTool,
    beginDeleteTools,
    resetBusyStates,
    openTool,
    setPythonOverride,
    handleToolsMessage,
    handleFileSelectedMessage,
    handleToolAddedMessage,
    handleToolUpdatedMessage,
    handleToolsDeletedMessage,
    handlePythonSelected,
  }
})
