import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'

import { useSettingsStore } from '../settings'
import { useToolsStore } from '../tools'
import type { ToolItem } from '../../types'

function setExternalSender(spy: ReturnType<typeof vi.fn>) {
  Object.defineProperty(window, 'external', {
    value: { sendMessage: spy },
    configurable: true,
    writable: true,
  })
}

describe('tools store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.localStorage.clear()
  })

  it('uses the default Node runtime when running a node tool in terminal', () => {
    const sendSpy = vi.fn()
    setExternalSender(sendSpy)

    const settings = useSettingsStore()
    settings.setDefaultNodePath('C:/Node/node.exe')

    const store = useToolsStore()
    store.handleToolsMessage({
      type: 'tools',
      tools: [
        {
          id: 'node-demo',
          name: 'Node Demo',
          type: 'node',
          path: 'C:/Tools/demo.mjs',
          argsTemplate: '',
          tags: [],
          pathExists: true,
          valid: true,
        } as ToolItem,
      ],
    })

    store.runToolInTerminal({
      toolId: 'node-demo',
      args: {},
    })

    expect(sendSpy).toHaveBeenCalledTimes(1)
    expect(JSON.parse(sendSpy.mock.calls[0]![0]!)).toMatchObject({
      type: 'runToolInTerminal',
      toolId: 'node-demo',
      runtimePath: 'C:/Node/node.exe',
    })
  })

  it('dispatches url tools through the dedicated open message', () => {
    const sendSpy = vi.fn()
    setExternalSender(sendSpy)

    const store = useToolsStore()
    store.openUrlTool('docs')

    expect(sendSpy).toHaveBeenCalledTimes(1)
    expect(JSON.parse(sendSpy.mock.calls[0]![0]!)).toEqual({
      type: 'openUrlTool',
      toolId: 'docs',
    })
  })

  it('routes python runtime browsing through the python selector', () => {
    const sendSpy = vi.fn()
    setExternalSender(sendSpy)

    const store = useToolsStore()
    store.pickAddToolRuntime('C:/Python', 'python')

    expect(sendSpy).toHaveBeenCalledTimes(1)
    expect(JSON.parse(sendSpy.mock.calls[0]![0]!)).toEqual({
      type: 'browsePython',
      defaultPath: 'C:/Python',
      purpose: 'addToolRuntime',
    })
  })

  it('updates file selection targets based on the incoming purpose', () => {
    const store = useToolsStore()

    store.handleFileSelectedMessage({
      type: 'fileSelected',
      path: 'C:/Tools/demo.py',
      purpose: 'addToolPath',
    })
    store.handleFileSelectedMessage({
      type: 'fileSelected',
      path: 'C:/Python/python.exe',
      purpose: 'toolRunnerRuntime',
    })

    expect(store.addToolPathSelection).toBe('C:/Tools/demo.py')
    expect(store.runtimeOverride).toBe('C:/Python/python.exe')
  })

  it('keeps runner override aligned with updated app defaults for python tools', async () => {
    const settings = useSettingsStore()
    settings.setDefaultPythonPath('C:/Python/python.exe')

    const store = useToolsStore()
    store.openTool({
      id: 'python-demo',
      name: 'Python Demo',
      type: 'python',
      path: 'C:/Tools/demo.py',
      argsTemplate: '',
      tags: [],
      pathExists: true,
      valid: true,
    } as ToolItem)

    settings.setDefaultPythonPath('D:/Python/python.exe')
    await nextTick()

    expect(store.runtimeOverride).toBe('D:/Python/python.exe')
  })
})
