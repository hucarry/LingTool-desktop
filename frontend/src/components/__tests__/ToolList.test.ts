import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import ToolList from '../ToolList.vue'
import { useSettingsStore } from '../../stores/settings'
import type { ToolItem } from '../../types'

const tools: ToolItem[] = [
  {
    id: 'alpha',
    name: 'Alpha Tool',
    type: 'python',
    path: 'C:/alpha.py',
    argsTemplate: '',
    tags: ['alpha'],
    description: 'First',
    pathExists: true,
    valid: true,
  },
  {
    id: 'broken',
    name: 'Broken Tool',
    type: 'exe',
    path: 'C:/missing.exe',
    argsTemplate: '',
    tags: ['broken'],
    description: 'Second',
    pathExists: false,
    valid: false,
  },
]

describe('ToolList', () => {
  it('filters tools by search keyword', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    useSettingsStore().setLocale('en-US')

    const wrapper = mount(ToolList, {
      props: {
        tools,
      },
      global: {
        plugins: [pinia],
        stubs: {
          teleport: true,
        },
      },
    })

    const search = wrapper.find('input[type="search"]')
    await search.setValue('broken')

    expect(wrapper.text()).toContain('Broken Tool')
    expect(wrapper.text()).not.toContain('Alpha Tool')
  })

  it('emits delete for selected items', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    useSettingsStore().setLocale('en-US')
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true)
    const wrapper = mount(ToolList, {
      props: {
        tools,
      },
      global: {
        plugins: [pinia],
        stubs: {
          teleport: true,
        },
      },
    })

    const checkboxes = wrapper.findAll('input[type="checkbox"]')
    await checkboxes[1]?.setValue(true)
    await wrapper.findAll('button').find((button) => button.text().includes('Delete Selected'))?.trigger('click')

    expect(wrapper.emitted('deleteTools')?.[0]).toEqual([['alpha']])
    confirmSpy.mockRestore()
  })
})
