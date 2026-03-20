import type { Ref } from 'vue'
import { ref, watch } from 'vue'

import { bridge } from '../../services/bridge'
import {
  getDefaultRuntimeForTool,
  getRuntimeBrowseFilter,
  isScriptToolType,
  normalizeToolType,
} from '../../utils/toolTypes'
import type { ToolItem } from '../../types'

interface UseToolRunnerStateOptions {
  tools: Ref<ToolItem[]>
  activeTerminalId: Ref<string>
  defaultPythonPath: Ref<string>
  defaultNodePath: Ref<string>
}

export function useToolRunnerState(options: UseToolRunnerStateOptions) {
  const {
    tools,
    activeTerminalId,
    defaultPythonPath,
    defaultNodePath,
  } = options

  const activeTool = ref<ToolItem | null>(null)
  const runnerVisible = ref(false)
  const runtimeOverride = ref('')

  function syncRuntimeOverride(nextPath: string, previousPath: string | undefined, toolType: 'python' | 'node'): void {
    if (
      activeTool.value?.type === toolType
      && !activeTool.value.runtimePath
      && runtimeOverride.value === (previousPath ?? '')
    ) {
      runtimeOverride.value = nextPath
    }
  }

  watch(defaultPythonPath, (nextPath, previousPath) => {
    syncRuntimeOverride(nextPath, previousPath, 'python')
  })

  watch(defaultNodePath, (nextPath, previousPath) => {
    syncRuntimeOverride(nextPath, previousPath, 'node')
  })

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

  function runToolInTerminal(payload: { toolId: string; args: Record<string, string>; runtimePath?: string }): void {
    const tool = tools.value.find((item) => item.id === payload.toolId)
    if (tool) {
      activeTool.value = tool
    }

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
      terminalId: activeTerminalId.value || undefined,
    })
  }

  function handleRuntimeSelected(path?: string): void {
    if (!path?.trim()) {
      return
    }

    runtimeOverride.value = path
  }

  return {
    activeTool,
    runnerVisible,
    runtimeOverride,
    openTool,
    setRuntimeOverride,
    closeRunner,
    pickRuntimePath,
    runToolInTerminal,
    handleRuntimeSelected,
  }
}
