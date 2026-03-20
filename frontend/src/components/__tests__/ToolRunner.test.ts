import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { describe, expect, it } from 'vitest'

import ToolRunner from '../ToolRunner.vue'
import { useSettingsStore } from '../../stores/settings'
import type { ToolItem } from '../../types'

function mountToolRunner(tool: ToolItem) {
  const pinia = createPinia()
  setActivePinia(pinia)
  useSettingsStore().setLocale('en-US')

  return mount(ToolRunner, {
    props: {
      visible: true,
      tool,
      runtimeOverride: 'C:/Python/python.exe',
      defaultPythonPath: 'C:/Python/python.exe',
    },
    global: {
      plugins: [pinia],
      stubs: {
        teleport: true,
      },
    },
  })
}

describe('ToolRunner', () => {
  it('renders structured argument fields and previews commands without argsTemplate', async () => {
    const wrapper = mountToolRunner({
      id: 'structured-python',
      name: 'Structured Python',
      type: 'python',
      path: 'C:/Tools/demo.py',
      cwd: 'C:/Tools',
      runtimePath: 'C:/Python/python.exe',
      argsTemplate: '',
      argsSpec: {
        version: 1,
        fields: [
          { name: 'input', label: 'Input File', kind: 'path', required: true, placeholder: 'C:/input.txt' },
          { name: 'format', label: 'Format', kind: 'select', defaultValue: 'json', options: [{ label: 'JSON', value: 'json' }, { label: 'CSV', value: 'csv' }] },
        ],
        argv: [
          { kind: 'literal', value: '--input' },
          { kind: 'field', field: 'input' },
          { kind: 'literal', value: '--format' },
          { kind: 'field', field: 'format', omitWhenEmpty: false },
        ],
      },
      tags: [],
      description: 'demo',
      pathExists: true,
      valid: true,
    })

    expect(wrapper.text()).toContain('Input File')
    expect(wrapper.text()).toContain('Format')
    expect(wrapper.text()).toContain('--format json')

    const input = wrapper.find('input[placeholder="C:/input.txt"]')
    await input.setValue('C:/data/source.txt')

    expect(wrapper.text()).toContain('--input C:/data/source.txt --format json')
  })
})
