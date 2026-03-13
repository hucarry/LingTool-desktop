import { storeToRefs } from 'pinia'

import { usePythonStore } from '../stores/python'
import { useSettingsStore } from '../stores/settings'
import { useTerminalsStore } from '../stores/terminals'
import { useToolsStore } from '../stores/tools'
import type { AddToolPayload, ToolItem } from '../types'

export function useToolHub() {
  const toolsStore = useToolsStore()
  const pythonStore = usePythonStore()
  const terminalsStore = useTerminalsStore()
  const settingsStore = useSettingsStore()

  return {
    ...storeToRefs(toolsStore),
    ...storeToRefs(pythonStore),
    ...storeToRefs(terminalsStore),
    fetchTools: toolsStore.fetchTools,
    addTool: (tool: AddToolPayload) => toolsStore.addTool(tool),
    updateTool: (tool: AddToolPayload) => toolsStore.updateTool(tool),
    deleteTools: (toolIds: string[]) => toolsStore.deleteTools(toolIds),
    pickAddToolPath: (defaultPath?: string, toolType?: string) => toolsStore.pickAddToolPath(defaultPath, toolType),
    pickAddToolPython: (defaultPath?: string) => toolsStore.pickAddToolPython(defaultPath),
    pickEditToolPath: (defaultPath?: string, toolType?: string) => toolsStore.pickEditToolPath(defaultPath, toolType),
    pickEditToolPython: (defaultPath?: string) => toolsStore.pickEditToolPython(defaultPath),
    openTool: (tool: ToolItem) => toolsStore.openTool(tool, settingsStore.defaultPythonPath),
    handleRun: (payload: { toolId: string; args: Record<string, string>; python?: string }) => toolsStore.runToolInTerminal(payload),
    pickPythonInterpreter: toolsStore.pickPythonInterpreter,
    pickPythonForPackages: pythonStore.pickPythonForPackages,
    useSystemPythonForPackages: () => {
      pythonStore.useSystemPythonPath()
      pythonStore.refreshPythonPackages()
    },
    refreshPythonPackages: pythonStore.refreshPythonPackages,
    installPythonPackage: pythonStore.installPythonPackage,
    uninstallPythonPackage: pythonStore.uninstallPythonPackage,
    createTerminal: terminalsStore.createTerminal,
    stopTerminal: terminalsStore.stopTerminal,
    stopAllTerminals: terminalsStore.stopAllTerminals,
    sendTerminalInput: terminalsStore.sendTerminalInput,
    resizeTerminal: terminalsStore.resizeTerminal,
    clearTerminalOutput: terminalsStore.clearTerminalOutput,
    selectTerminal: terminalsStore.selectTerminal,
    initToolHub: terminalsStore.fetchTerminals,
    disposeToolHub: terminalsStore.resetCreateState,
  }
}
