import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { describe, expect, it } from 'vitest'

import TerminalPanel from '../TerminalPanel.vue'
import { useSettingsStore } from '../../stores/settings'
import type { TerminalInfo } from '../../types'

const terminals: TerminalInfo[] = [
  {
    terminalId: 'term-main',
    title: 'Main',
    shell: 'powershell.exe',
    cwd: 'C:/workspace/main',
    status: 'running',
    startTime: '2026-03-20T00:00:00Z',
  },
  {
    terminalId: 'term-side',
    title: 'Side',
    shell: 'node.exe',
    cwd: 'C:/workspace/side',
    status: 'running',
    startTime: '2026-03-20T00:00:00Z',
  },
]

function mountTerminalPanel() {
  const pinia = createPinia()
  setActivePinia(pinia)
  useSettingsStore().setLocale('en-US')

  return mount(TerminalPanel, {
    props: {
      visible: true,
      terminals,
      activeTerminalId: 'term-main',
      outputsByTerminal: {
        'term-main': ['main'],
        'term-side': ['side'],
      },
    },
    global: {
      plugins: [pinia],
      stubs: {
        teleport: true,
        TerminalViewport: {
          props: ['terminalId', 'outputs'],
          template: '<div class="terminal-viewport-stub" />',
        },
      },
    },
  })
}

describe('TerminalPanel', () => {
  it('enables split layout from the toolbar', async () => {
    const wrapper = mountTerminalPanel()

    const splitButton = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Split'))

    expect(splitButton).toBeDefined()

    await splitButton!.trigger('click')

    expect(wrapper.find('.terminal-layout').classes()).toContain('is-split')
    expect(wrapper.findAll('.terminal-pane')).toHaveLength(2)
  })

  it('updates the input target label when the focused pane changes', async () => {
    const wrapper = mountTerminalPanel()

    const splitButton = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Split'))

    await splitButton!.trigger('click')

    expect(wrapper.find('.toolbar-target').text()).toContain('Side')

    const primaryChip = wrapper
      .findAll('.split-target-chip')
      .find((chip) => chip.text().includes('Main'))

    expect(primaryChip).toBeDefined()

    await primaryChip!.trigger('click')

    expect(wrapper.find('.toolbar-target').text()).toContain('Main')
    expect(wrapper.emitted('selectTerminal')?.at(-1)).toEqual(['term-main'])
  })
})
