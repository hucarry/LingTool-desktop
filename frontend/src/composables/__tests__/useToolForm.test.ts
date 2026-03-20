import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { useToolForm } from '../useToolForm'
import { useSettingsStore } from '../../stores/settings'

describe('useToolForm', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useSettingsStore()
  })

  it('derives id, name, and cwd from a selected script path', () => {
    const form = useToolForm('python')

    form.applyPathSuggestion('C:/Tools/demo-script.py', true)

    expect(form.form.id).toBe('demo-script')
    expect(form.form.name).toBe('demo-script')
    expect(form.form.cwd).toBe('C:/Tools')
  })

  it('derives id and name from a command input', () => {
    const form = useToolForm('command')

    form.applyPathSuggestion('npm', true)

    expect(form.form.id).toBe('npm')
    expect(form.form.name).toBe('npm')
    expect(form.form.cwd).toBe('')
  })

  it('builds a script payload with runtimePath and deduplicated tags', () => {
    const form = useToolForm('node')

    form.form.id = 'demo-node'
    form.form.name = 'Demo Node'
    form.form.path = 'C:/Tools/demo.mjs'
    form.form.runtimePath = 'C:/Node/node.exe'
    form.form.tagsText = 'alpha, beta, alpha'

    expect(form.createPayload()).toMatchObject({
      id: 'demo-node',
      name: 'Demo Node',
      type: 'node',
      path: 'C:/Tools/demo.mjs',
      runtimePath: 'C:/Node/node.exe',
      tags: ['alpha', 'beta'],
    })
  })

  it('validates url tools and omits cwd/args from the payload', () => {
    const form = useToolForm('url')

    form.form.id = 'docs'
    form.form.name = 'Docs'
    form.form.path = 'not-a-url'

    expect(form.validationErrors.value.path).toBeTruthy()

    form.form.path = 'https://example.com/docs'
    form.form.cwd = 'C:/ignored'
    form.form.argsTemplate = '--ignored'

    expect(form.createPayload()).toMatchObject({
      type: 'url',
      path: 'https://example.com/docs',
      cwd: undefined,
      argsTemplate: '',
    })
  })

  it('preserves structured argsSpec when the legacy template stays aligned', () => {
    const form = useToolForm('python')

    form.setFromTool({
      id: 'structured-demo',
      name: 'Structured Demo',
      type: 'python',
      path: 'C:/Tools/demo.py',
      argsTemplate: '--input {input}',
      argsSpec: {
        version: 1,
        fields: [
          { name: 'input', label: 'Input', kind: 'path', required: true },
        ],
        argv: [
          { kind: 'literal', value: '--input' },
          { kind: 'field', field: 'input' },
        ],
      },
      tags: [],
      pathExists: true,
      valid: true,
    })

    expect(form.createPayload().argsSpec).toEqual({
      version: 1,
      fields: [
        { name: 'input', label: 'Input', kind: 'path', required: true, options: [] },
      ],
      argv: [
        { kind: 'literal', value: '--input' },
        { kind: 'field', field: 'input', omitWhenEmpty: true },
      ],
    })
    expect(form.form.argsMode).toBe('structured')
  })

  it('re-infers a minimal argsSpec when the raw template changes', () => {
    const form = useToolForm('python')

    form.setFromTool({
      id: 'structured-demo',
      name: 'Structured Demo',
      type: 'python',
      path: 'C:/Tools/demo.py',
      argsTemplate: '--input {input}',
      argsSpec: {
        version: 1,
        fields: [
          { name: 'input', label: 'Input', kind: 'path', required: true },
        ],
        argv: [
          { kind: 'literal', value: '--input' },
          { kind: 'field', field: 'input' },
        ],
      },
      tags: [],
      pathExists: true,
      valid: true,
    })

    form.form.argsTemplate = '--output {output}'

    expect(form.createPayload().argsSpec).toEqual({
      version: 1,
      fields: [
        { name: 'output', kind: 'text' },
      ],
      argv: [
        { kind: 'literal', value: '--output' },
        { kind: 'field', field: 'output', omitWhenEmpty: false },
      ],
    })
  })
})
