import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { useToolForm } from '../useToolForm'
import { useSettingsStore } from '../../stores/settings'

describe('useToolForm', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useSettingsStore()
  })

  it('derives id, name, and cwd from a selected path', () => {
    const form = useToolForm('python')

    form.applyPathSuggestion('C:/Tools/demo-script.py', true)

    expect(form.form.id).toBe('demo-script')
    expect(form.form.name).toBe('demo-script')
    expect(form.form.cwd).toBe('C:/Tools')
  })

  it('deduplicates tags and builds a payload', () => {
    const form = useToolForm('python')

    form.form.id = 'demo'
    form.form.name = 'Demo'
    form.form.path = 'C:/Tools/demo.py'
    form.form.tagsText = 'alpha, beta, alpha'

    expect(form.createPayload().tags).toEqual(['alpha', 'beta'])
  })
})
