<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { computed } from 'vue'

import UiButton from '../components/ui/UiButton.vue'
import UiInput from '../components/ui/UiInput.vue'
import UiPanel from '../components/ui/UiPanel.vue'
import { useI18n } from '../composables/useI18n'
import { useSettingsStore } from '../stores/settings'

const { t } = useI18n()
const settingsStore = useSettingsStore()
const { theme, defaultPythonPath, defaultNodePath, diagnosticsExporting, lastDiagnosticBundlePath } = storeToRefs(settingsStore)

const diagnosticsButtonLabel = computed(() =>
  diagnosticsExporting.value
    ? t('settings.diagnosticsExporting')
    : t('settings.diagnosticsExport'),
)
</script>

<template>
  <section class="h-full overflow-auto p-3">
    <div class="flex flex-col gap-3">
      <UiPanel class="space-y-1.5">
        <h2 class="text-lg font-semibold text-foreground">{{ t('settings.title') }}</h2>
        <p class="max-w-3xl text-sm leading-6 text-muted">{{ t('settings.description') }}</p>
      </UiPanel>

      <UiPanel class="flex flex-col gap-3">
        <div class="space-y-1">
          <h3 class="text-sm font-semibold text-foreground">{{ t('settings.themeTitle') }}</h3>
          <p class="text-xs leading-5 text-muted">{{ t('settings.themeDesc') }}</p>
        </div>

        <div class="inline-flex w-fit gap-2 rounded-field border border-border bg-editor p-1">
          <UiButton :variant="theme === 'dark' ? 'primary' : 'ghost'" size="sm" @click="settingsStore.setTheme('dark')">
            {{ t('settings.themeDark') }}
          </UiButton>
          <UiButton :variant="theme === 'light' ? 'primary' : 'ghost'" size="sm" @click="settingsStore.setTheme('light')">
            {{ t('settings.themeLight') }}
          </UiButton>
        </div>
      </UiPanel>

      <UiPanel class="flex flex-col gap-3">
        <div class="space-y-1">
          <h3 class="text-sm font-semibold text-foreground">{{ t('settings.pythonTitle') }}</h3>
          <p class="text-xs leading-5 text-muted">{{ t('settings.pythonDesc') }}</p>
        </div>

        <div class="grid gap-2 md:grid-cols-[minmax(0,1fr)_auto_auto]">
          <UiInput :model-value="defaultPythonPath" readonly :placeholder="t('settings.placeholder')" />
          <UiButton @click="settingsStore.pickDefaultPython">{{ t('python.browse') }}</UiButton>
          <UiButton @click="settingsStore.clearDefaultPythonPath">{{ t('settings.clear') }}</UiButton>
        </div>
      </UiPanel>

      <UiPanel class="flex flex-col gap-3">
        <div class="space-y-1">
          <h3 class="text-sm font-semibold text-foreground">{{ t('settings.nodeTitle') }}</h3>
          <p class="text-xs leading-5 text-muted">{{ t('settings.nodeDesc') }}</p>
        </div>

        <div class="grid gap-2 md:grid-cols-[minmax(0,1fr)_auto_auto]">
          <UiInput :model-value="defaultNodePath" readonly :placeholder="t('settings.nodePlaceholder')" />
          <UiButton @click="settingsStore.pickDefaultNode">{{ t('python.browse') }}</UiButton>
          <UiButton @click="settingsStore.clearDefaultNodePath">{{ t('settings.clear') }}</UiButton>
        </div>
      </UiPanel>

      <UiPanel class="flex flex-col gap-3">
        <div class="space-y-1">
          <h3 class="text-sm font-semibold text-foreground">{{ t('settings.diagnosticsTitle') }}</h3>
          <p class="text-xs leading-5 text-muted">{{ t('settings.diagnosticsDesc') }}</p>
        </div>

        <div class="grid gap-2 md:grid-cols-[minmax(0,1fr)_auto]">
          <UiInput
            :model-value="lastDiagnosticBundlePath"
            readonly
            :placeholder="t('settings.diagnosticsPlaceholder')"
          />
          <UiButton :disabled="diagnosticsExporting" @click="settingsStore.exportDiagnosticBundle">
            {{ diagnosticsButtonLabel }}
          </UiButton>
        </div>
      </UiPanel>
    </div>
  </section>
</template>
