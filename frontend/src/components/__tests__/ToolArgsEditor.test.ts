import { mount } from '@vue/test-utils'
import { defineComponent, ref } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import { describe, expect, it } from 'vitest'

import ToolArgsEditor from '../tools/ToolArgsEditor.vue'
import type { ArgEditorMode, ArgsSpecV1 } from '../../types'
import { useSettingsStore } from '../../stores/settings'

function mountHarness() {
  const pinia = createPinia()
  setActivePinia(pinia)
  useSettingsStore().setLocale('en-US')

  const Harness = defineComponent({
    components: { ToolArgsEditor },
    setup() {
      const mode = ref<ArgEditorMode>('legacy')
      const argsTemplate = ref('--input {input}')
      const argsSpec = ref<ArgsSpecV1 | null>(null)

      return {
        mode,
        argsTemplate,
        argsSpec,
      }
    },
    template: `
      <ToolArgsEditor
        v-model:mode="mode"
        v-model:args-template="argsTemplate"
        v-model:args-spec="argsSpec"
        label="Args Template"
      />
    `,
  })

  return mount(Harness, {
    global: {
      plugins: [pinia],
    },
  })
}

describe('ToolArgsEditor', () => {
  it('switches to structured mode and keeps models in sync', async () => {
    const wrapper = mountHarness()

    await wrapper.get('[data-testid="args-mode-structured"]').trigger('click')

    expect((wrapper.vm as { mode: ArgEditorMode }).mode).toBe('structured')
    expect((wrapper.vm as { argsSpec: ArgsSpecV1 | null }).argsSpec?.fields).toEqual([
      expect.objectContaining({
        name: 'input',
        kind: 'text',
      }),
    ])

    await wrapper.get('[data-testid="args-add-field"]').trigger('click')

    expect((wrapper.vm as { argsSpec: ArgsSpecV1 | null }).argsSpec?.fields).toHaveLength(2)
    expect((wrapper.vm as { argsTemplate: string }).argsTemplate).toContain('--arg2 {arg2}')
  })
})
