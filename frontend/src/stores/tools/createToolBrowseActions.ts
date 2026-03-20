import type { Ref } from 'vue'

import { bridge } from '../../services/bridge'
import {
  getRuntimeBrowseFilter,
  getToolPathFilter,
  normalizeToolType,
} from '../../utils/toolTypes'
import type { FileSelectedMessage } from '../../types'

interface ToolBrowseState {
  addToolPathSelection: Ref<string>
  addToolRuntimeSelection: Ref<string>
  editToolPathSelection: Ref<string>
  editToolRuntimeSelection: Ref<string>
  runtimeOverride: Ref<string>
}

function pickToolPath(purpose: 'addToolPath' | 'editToolPath', defaultPath?: string, toolType?: string): void {
  const filter = getToolPathFilter(toolType ?? '')
  if (!filter) {
    return
  }

  bridge.send({
    type: 'browseFile',
    defaultPath,
    filter,
    purpose,
  })
}

function pickToolRuntime(purpose: 'addToolRuntime' | 'editToolRuntime', defaultPath?: string, toolType?: string): void {
  const normalizedType = normalizeToolType(toolType ?? '')
  if (normalizedType === 'python') {
    bridge.send({
      type: 'browsePython',
      defaultPath,
      purpose,
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
    purpose,
  })
}

export function createToolBrowseActions(state: ToolBrowseState) {
  function pickAddToolPath(defaultPath?: string, toolType?: string): void {
    pickToolPath('addToolPath', defaultPath, toolType)
  }

  function pickAddToolRuntime(defaultPath?: string, toolType?: string): void {
    pickToolRuntime('addToolRuntime', defaultPath, toolType)
  }

  function pickEditToolPath(defaultPath?: string, toolType?: string): void {
    pickToolPath('editToolPath', defaultPath, toolType)
  }

  function pickEditToolRuntime(defaultPath?: string, toolType?: string): void {
    pickToolRuntime('editToolRuntime', defaultPath, toolType)
  }

  function handleFileSelectedMessage(message: FileSelectedMessage): void {
    if (!message.path?.trim()) {
      return
    }

    if (message.purpose === 'addToolPath') {
      state.addToolPathSelection.value = message.path
      return
    }

    if (message.purpose === 'addToolRuntime') {
      state.addToolRuntimeSelection.value = message.path
      return
    }

    if (message.purpose === 'editToolPath') {
      state.editToolPathSelection.value = message.path
      return
    }

    if (message.purpose === 'editToolRuntime') {
      state.editToolRuntimeSelection.value = message.path
      return
    }

    if (message.purpose === 'toolRunnerRuntime') {
      state.runtimeOverride.value = message.path
    }
  }

  return {
    pickAddToolPath,
    pickAddToolRuntime,
    pickEditToolPath,
    pickEditToolRuntime,
    handleFileSelectedMessage,
  }
}
