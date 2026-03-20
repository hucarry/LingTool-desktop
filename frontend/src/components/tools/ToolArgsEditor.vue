<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import UiButton from '../ui/UiButton.vue'
import UiField from '../ui/UiField.vue'
import UiInput from '../ui/UiInput.vue'
import UiSelect from '../ui/UiSelect.vue'
import UiTextarea from '../ui/UiTextarea.vue'
import { useI18n } from '../../composables/useI18n'
import type { ArgEditorMode, ArgFieldKind, ArgsSpecV1 } from '../../types'
import {
  buildArgsSpecFromStructuredFields,
  buildLegacyArgsTemplate,
  extractStructuredArgFields,
  type StructuredArgFieldDraft,
} from '../../utils/argsSpec'

interface StructuredArgFieldRow extends StructuredArgFieldDraft {
  key: string
}

const props = defineProps<{
  label: string
  hint?: string
  placeholder?: string
}>()

const mode = defineModel<ArgEditorMode>('mode', {
  default: 'legacy',
})
const argsTemplate = defineModel<string>('argsTemplate', {
  default: '',
})
const argsSpec = defineModel<ArgsSpecV1 | null>('argsSpec', {
  default: null,
})

const { t } = useI18n()

const rows = ref<StructuredArgFieldRow[]>([])
const structuredCompatible = ref(true)
const syncing = ref(false)
const rowSequence = ref(0)

const kindOptions: ArgFieldKind[] = ['text', 'path', 'number', 'flag', 'select', 'secret']

const structuredPreview = computed(() => {
  return buildLegacyArgsTemplate(buildArgsSpecFromStructuredFields(stripRowKeys(rows.value)), '')
})

const activeHint = computed(() => {
  if (mode.value === 'structured') {
    return t('tools.argsStructuredHint')
  }

  return props.hint
})

watch(
  [mode, argsTemplate, argsSpec],
  () => {
    if (syncing.value || mode.value !== 'structured') {
      return
    }

    syncRowsFromSource()
  },
  { immediate: true },
)

watch(
  rows,
  () => {
    if (syncing.value || mode.value !== 'structured') {
      return
    }

    syncStructuredModels()
  },
  { deep: true },
)

function createRow(draft?: Partial<StructuredArgFieldDraft>): StructuredArgFieldRow {
  rowSequence.value += 1
  return {
    key: `arg-row-${rowSequence.value}`,
    name: draft?.name?.trim() ?? '',
    label: draft?.label?.trim() ?? '',
    description: draft?.description?.trim() ?? '',
    kind: draft?.kind ?? 'text',
    token: draft?.token?.trim() ?? '',
    required: draft?.required === true,
    defaultValue: draft?.defaultValue?.trim() ?? '',
    placeholder: draft?.placeholder?.trim() ?? '',
    optionsText: draft?.optionsText?.trim() ?? '',
  }
}

function stripRowKeys(inputRows: StructuredArgFieldRow[]): StructuredArgFieldDraft[] {
  return inputRows.map(({ key: _key, ...draft }) => draft)
}

function syncRowsFromSource(): void {
  const extraction = extractStructuredArgFields({
    argsTemplate: argsTemplate.value,
    argsSpec: argsSpec.value,
  })

  const nextRows = extraction.fields.map((field) => createRow(field))
  if (
    structuredCompatible.value === extraction.compatible
    && JSON.stringify(stripRowKeys(rows.value)) === JSON.stringify(stripRowKeys(nextRows))
  ) {
    return
  }

  structuredCompatible.value = extraction.compatible
  syncing.value = true
  rows.value = nextRows
  syncing.value = false
}

function syncStructuredModels(): void {
  const nextSpec = buildArgsSpecFromStructuredFields(stripRowKeys(rows.value))
  const nextTemplate = buildLegacyArgsTemplate(nextSpec, '')

  if (
    JSON.stringify(argsSpec.value) === JSON.stringify(nextSpec ?? null)
    && argsTemplate.value === nextTemplate
  ) {
    return
  }

  syncing.value = true
  argsSpec.value = nextSpec ?? null
  argsTemplate.value = nextTemplate
  syncing.value = false
}

function generateNextFieldName(): string {
  const usedNames = new Set(rows.value.map((row) => row.name.trim()).filter(Boolean))
  let index = rows.value.length + 1

  while (usedNames.has(`arg${index}`)) {
    index += 1
  }

  return `arg${index}`
}

function normalizeRowKind(row: StructuredArgFieldRow): void {
  if (row.kind === 'flag') {
    row.required = false
    row.optionsText = ''
    if (!row.defaultValue) {
      row.defaultValue = 'false'
    }
    if (!row.token.trim() && row.name.trim()) {
      row.token = `--${row.name.trim().replace(/_/g, '-')}`
    }
    return
  }

  if (row.kind !== 'select') {
    row.optionsText = ''
  }

  if (row.defaultValue === 'false' && row.kind !== 'select') {
    row.defaultValue = ''
  }
}

function addField(): void {
  const name = generateNextFieldName()
  rows.value.push(
    createRow({
      name,
      label: name,
      token: `--${name}`,
    }),
  )
}

function removeField(key: string): void {
  rows.value = rows.value.filter((row) => row.key !== key)
}

function switchMode(nextMode: ArgEditorMode): void {
  if (nextMode === mode.value) {
    return
  }

  if (nextMode === 'structured') {
    mode.value = 'structured'
    syncRowsFromSource()
    syncStructuredModels()
    return
  }

  syncing.value = true
  argsTemplate.value = buildLegacyArgsTemplate(
    buildArgsSpecFromStructuredFields(stripRowKeys(rows.value)),
    argsTemplate.value,
  )
  mode.value = 'legacy'
  syncing.value = false
}
</script>

<template>
  <UiField :label="label" :hint="activeHint">
    <div class="space-y-3 rounded-panel border border-border/70 bg-panel/80 p-3">
      <div class="flex flex-wrap items-center gap-2">
        <UiButton
          type="button"
          :variant="mode === 'legacy' ? 'primary' : 'default'"
          data-testid="args-mode-legacy"
          @click="switchMode('legacy')"
        >
          {{ t('tools.argsModeLegacy') }}
        </UiButton>
        <UiButton
          type="button"
          :variant="mode === 'structured' ? 'primary' : 'default'"
          data-testid="args-mode-structured"
          @click="switchMode('structured')"
        >
          {{ t('tools.argsModeStructured') }}
        </UiButton>
      </div>

      <div v-if="mode === 'legacy'">
        <UiInput v-model="argsTemplate" :placeholder="placeholder" />
      </div>

      <div v-else class="space-y-3">
        <div
          v-if="!structuredCompatible"
          class="rounded-field border border-warning/40 bg-warning-soft px-3 py-2 text-xs leading-5 text-foreground"
        >
          <strong class="block text-warning">{{ t('tools.argsStructuredIncompatible') }}</strong>
          <span>{{ t('tools.argsStructuredIncompatibleHint') }}</span>
        </div>

        <div v-if="rows.length === 0" class="rounded-field border border-dashed border-border/80 bg-soft px-3 py-4 text-sm text-muted">
          {{ t('tools.argsStructuredEmpty') }}
        </div>

        <div v-for="row in rows" :key="row.key" class="space-y-3 rounded-field border border-border/70 bg-soft p-3">
          <div class="flex items-center justify-between gap-2">
            <strong class="text-sm font-semibold text-foreground">
              {{ row.label || row.name || t('tools.argsStructuredUnnamed') }}
            </strong>
            <UiButton type="button" variant="ghost" size="sm" @click="removeField(row.key)">
              {{ t('tools.argsStructuredRemove') }}
            </UiButton>
          </div>

          <div class="grid gap-3 md:grid-cols-2">
            <UiField :label="t('tools.argsStructuredName')">
              <UiInput v-model="row.name" />
            </UiField>

            <UiField :label="t('tools.argsStructuredLabel')">
              <UiInput v-model="row.label" />
            </UiField>

            <UiField :label="t('tools.argsStructuredKind')">
              <UiSelect v-model="row.kind" @change="normalizeRowKind(row)">
                <option v-for="kind in kindOptions" :key="kind" :value="kind">
                  {{ t(`tools.argsStructuredKind.${kind}`) }}
                </option>
              </UiSelect>
            </UiField>

            <UiField :label="t('tools.argsStructuredToken')" :hint="t('tools.argsStructuredTokenHint')">
              <UiInput v-model="row.token" />
            </UiField>

            <UiField v-if="row.kind !== 'flag'" :label="t('tools.argsStructuredDefault')">
              <UiInput v-model="row.defaultValue" />
            </UiField>

            <UiField v-else :label="t('tools.argsStructuredDefault')">
              <UiSelect v-model="row.defaultValue">
                <option value="false">false</option>
                <option value="true">true</option>
              </UiSelect>
            </UiField>

            <UiField v-if="row.kind !== 'flag'" :label="t('tools.argsStructuredPlaceholder')">
              <UiInput v-model="row.placeholder" />
            </UiField>

            <UiField v-if="row.kind !== 'flag'" :label="t('tools.argsStructuredRequired')">
              <UiSelect
                :model-value="row.required ? 'true' : 'false'"
                @update:model-value="row.required = $event === 'true'"
              >
                <option value="false">{{ t('tools.argsStructuredOptional') }}</option>
                <option value="true">{{ t('tools.argsStructuredRequiredValue') }}</option>
              </UiSelect>
            </UiField>
          </div>

          <UiField v-if="row.kind === 'select'" :label="t('tools.argsStructuredOptions')" :hint="t('tools.argsStructuredOptionsHint')">
            <UiTextarea v-model="row.optionsText" rows="3" />
          </UiField>

          <UiField :label="t('tools.argsStructuredDescription')">
            <UiInput v-model="row.description" />
          </UiField>
        </div>

        <div class="flex flex-wrap items-center gap-2">
          <UiButton type="button" data-testid="args-add-field" @click="addField">
            {{ t('tools.argsStructuredAddField') }}
          </UiButton>
        </div>

        <UiField :label="t('tools.argsStructuredPreview')" :hint="t('tools.argsStructuredPreviewHint')">
          <UiInput :model-value="structuredPreview" readonly />
        </UiField>
      </div>
    </div>
  </UiField>
</template>
