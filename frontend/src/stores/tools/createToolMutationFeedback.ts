import type { Ref } from 'vue'

import type {
  ToolAddedMessage,
  ToolsDeletedMessage,
  ToolUpdatedMessage,
} from '../../types'

type TranslateFn = (key: string, params?: Record<string, string | number>) => string

interface ToolMutationFeedbackOptions {
  addingTool: Ref<boolean>
  updatingTool: Ref<boolean>
  deletingTools: Ref<boolean>
  lastAddedToolId: Ref<string>
  lastUpdatedToolId: Ref<string>
  notifications: {
    success(message: string, options?: { groupKey?: string }): void
  }
  t: TranslateFn
}

export function createToolMutationFeedback(options: ToolMutationFeedbackOptions) {
  const {
    addingTool,
    updatingTool,
    deletingTools,
    lastAddedToolId,
    lastUpdatedToolId,
    notifications,
    t,
  } = options

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

  return {
    handleToolAddedMessage,
    handleToolUpdatedMessage,
    handleToolsDeletedMessage,
  }
}
