import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import ToolPathField from '../tools/ToolPathField.vue'

describe('ToolPathField', () => {
  it('emits the selected preset path', async () => {
    const wrapper = mount(ToolPathField, {
      props: {
        modelValue: '',
        label: 'Tool Path',
        hint: 'Pick a path',
        browseLabel: 'Browse...',
        desktopPath: 'C:/Users/demo/Desktop',
        appRootPath: 'D:/Apps/ToolHub',
        desktopLabel: 'Desktop',
        appRootLabel: 'App Root',
        customLabel: 'Custom Path',
      },
    })

    await wrapper.find('select').setValue('D:/Apps/ToolHub')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['D:/Apps/ToolHub'])
  })

  it('keeps the select on custom when the current path is not a preset', () => {
    const wrapper = mount(ToolPathField, {
      props: {
        modelValue: 'E:/Custom/tool.py',
        label: 'Tool Path',
        browseLabel: 'Browse...',
        desktopPath: 'C:/Users/demo/Desktop',
        appRootPath: 'D:/Apps/ToolHub',
        desktopLabel: 'Desktop',
        appRootLabel: 'App Root',
        customLabel: 'Custom Path',
      },
    })

    expect((wrapper.find('select').element as HTMLSelectElement).value).toBe('')
  })
})
