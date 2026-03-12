<script setup lang="ts">
import { Button as DButton } from 'vue-devui/button'
import { Input as DInput } from 'vue-devui/input'
import 'vue-devui/button/style.css'
import 'vue-devui/input/style.css'
import { useI18n } from '../composables/useI18n'
import { useSettings } from '../composables/useSettings'

const { t } = useI18n()
const { theme, defaultPythonPath, pickDefaultPython, clearDefaultPythonPath } = useSettings()
</script>

<template>
  <section class="settings-view">
    <div class="settings-shell">
      <header class="settings-header">
        <h2>{{ t('settings.title') }}</h2>
        <p>{{ t('settings.description') }}</p>
      </header>

      <section class="settings-card">
        <div class="settings-copy">
          <h3>{{ t('settings.themeTitle') }}</h3>
          <p>{{ t('settings.themeDesc') }}</p>
        </div>

        <div class="theme-segmented">
          <button
            class="seg-btn"
            :class="{ active: theme === 'dark' }"
            @click="theme = 'dark'"
          >
            {{ t('settings.themeDark') }}
          </button>
          <button
            class="seg-btn"
            :class="{ active: theme === 'light' }"
            @click="theme = 'light'"
          >
            {{ t('settings.themeLight') }}
          </button>
        </div>
      </section>

      <section class="settings-card">
        <div class="settings-copy">
          <h3>{{ t('settings.pythonTitle') }}</h3>
          <p>{{ t('settings.pythonDesc') }}</p>
        </div>

        <div class="python-row">
          <d-input
            :model-value="defaultPythonPath"
            readonly
            :placeholder="t('settings.placeholder')"
          />
          <d-button @click="pickDefaultPython">{{ t('python.browse') }}</d-button>
          <d-button @click="clearDefaultPythonPath">{{ t('settings.clear') }}</d-button>
        </div>
      </section>
    </div>
  </section>
</template>

<style scoped>
.settings-view {
  height: 100%;
  min-height: 0;
  overflow: auto;
}

.settings-shell {
  display: flex;
  flex-direction: column;
  gap: 18px;
  padding: 18px;
  border: 1px solid var(--vscode-border-color);
  background: var(--vscode-editor-bg);
}

.settings-header h2 {
  font-size: 18px;
  font-weight: 700;
  color: var(--vscode-text-primary);
}

.settings-header p {
  margin-top: 8px;
  font-size: 13px;
  color: var(--vscode-text-muted);
  line-height: 1.6;
}

.settings-card {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
  border: 1px solid var(--vscode-border-color);
  background: var(--vscode-sidebar-bg);
}

.settings-copy h3 {
  font-size: 15px;
  font-weight: 600;
  color: var(--vscode-text-primary);
}

.settings-copy p {
  margin-top: 6px;
  font-size: 12px;
  color: var(--vscode-text-muted);
  line-height: 1.5;
}

.theme-segmented {
  display: inline-flex;
  background: var(--vscode-editor-bg);
  padding: 4px;
  border-radius: 4px;
  border: 1px solid var(--vscode-border-color);
  gap: 4px;
}

.seg-btn {
  border: none;
  background: transparent;
  padding: 6px 16px;
  border-radius: 4px;
  color: var(--vscode-text-primary);
  cursor: pointer;
  font-size: 13px;
  font-family: inherit;
  transition: all 0.2s ease;
}

.seg-btn.active {
  background: var(--vscode-accent-color);
  color: #fff;
}

.seg-btn:hover:not(.active) {
  background: var(--vscode-hover-bg);
}

.python-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto auto;
  gap: 10px;
  align-items: center;
}

@media (max-width: 760px) {
  .settings-shell {
    padding: 10px;
  }

  .python-row {
    grid-template-columns: 1fr;
  }
}
</style>
