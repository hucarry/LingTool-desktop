import type { ToolItem, ToolType } from '../types'

export type ScriptToolType = 'python' | 'node'

const scriptTypes = new Set<ToolType>(['python', 'node'])

export function normalizeToolType(type: string): ToolType {
  const normalized = type.trim().toLowerCase()
  if (normalized === 'exe') {
    return 'executable'
  }

  if (normalized === 'python' || normalized === 'node' || normalized === 'command' || normalized === 'executable' || normalized === 'url') {
    return normalized
  }

  return 'command'
}

export function isScriptToolType(type: string): type is ScriptToolType {
  return scriptTypes.has(normalizeToolType(type))
}

export function isCommandToolType(type: string): boolean {
  return normalizeToolType(type) === 'command'
}

export function isUrlToolType(type: string): boolean {
  return normalizeToolType(type) === 'url'
}

export function isExecutableToolType(type: string): boolean {
  return normalizeToolType(type) === 'executable'
}

export function needsRuntimePath(type: string): boolean {
  return isScriptToolType(type)
}

export function supportsPathBrowse(type: string): boolean {
  const normalized = normalizeToolType(type)
  return normalized === 'python' || normalized === 'node' || normalized === 'executable'
}

export function getToolPathFilter(type: string): string | undefined {
  const normalized = normalizeToolType(type)
  if (normalized === 'python') {
    return 'py'
  }

  if (normalized === 'node') {
    return 'js,mjs,cjs'
  }

  if (normalized === 'executable') {
    return 'exe,cmd,bat'
  }

  return undefined
}

export function getRuntimeBrowseFilter(type: string): string | undefined {
  const normalized = normalizeToolType(type)
  if (normalized === 'node') {
    return 'exe,cmd,bat'
  }

  return undefined
}

export function getDefaultRuntimeCommand(type: string): string {
  return normalizeToolType(type) === 'node' ? 'node' : 'python'
}

export function getDefaultRuntimeForTool(
  tool: Pick<ToolItem, 'type' | 'runtimePath'>,
  defaults: { defaultPythonPath?: string; defaultNodePath?: string },
): string {
  if (!isScriptToolType(tool.type)) {
    return ''
  }

  if (tool.runtimePath?.trim()) {
    return tool.runtimePath.trim()
  }

  if (tool.type === 'python') {
    return defaults.defaultPythonPath?.trim() ?? ''
  }

  return defaults.defaultNodePath?.trim() ?? ''
}

export function isValidHttpUrl(value: string): boolean {
  try {
    const url = new URL(value)
    return url.protocol === 'http:' || url.protocol === 'https:'
  } catch {
    return false
  }
}
