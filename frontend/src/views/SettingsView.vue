<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from '../composables/useI18n'
import { useSettings } from '../composables/useSettings'

const { locale } = useI18n()
const { theme, defaultPythonPath, pickDefaultPython, clearDefaultPythonPath } = useSettings()

const text = computed(() => {
  if (locale.value === 'zh-CN') {
    return {
      title: '设置',
      description: '配置主题和全局默认 Python 解释器。默认解释器会作为工具运行和 Python 包管理的默认值。',
      themeTitle: '主题',
      themeDesc: '切换工作台整体外观。',
      themeDark: '深色',
      themeLight: '浅色',
      pythonTitle: '默认 Python',
      pythonDesc: '未设置时会回退到工具配置或系统 Python。',
      browse: '浏览...',
      clear: '清除',
      placeholder: '未设置时回退到工具配置或系统 Python',
    }
  }

  return {
    title: 'Settings',
    description: 'Configure the theme and the global default Python interpreter used by tools and the package manager.',
    themeTitle: 'Theme',
    themeDesc: 'Switch the overall workbench appearance.',
    themeDark: 'Dark',
    themeLight: 'Light',
    pythonTitle: 'Default Python',
    pythonDesc: 'Falls back to tool config or system Python when empty.',
    browse: 'Browse...',
    clear: 'Clear',
    placeholder: 'Falls back to tool config or system Python when empty',
  }
})
</script>

<template>
  <section class="settings-view">
    <div class="settings-shell">
      <header class="settings-header">
        <h2>{{ text.title }}</h2>
        <p>{{ text.description }}</p>
      </header>

      <section class="settings-card">
        <div class="settings-copy">
          <h3>{{ text.themeTitle }}</h3>
          <p>{{ text.themeDesc }}</p>
        </div>

        <el-segmented
          v-model="theme"
          class="theme-switcher"
          :options="[
            { label: text.themeDark, value: 'dark' },
            { label: text.themeLight, value: 'light' },
          ]"
        />
      </section>

      <section class="settings-card">
        <div class="settings-copy">
          <h3>{{ text.pythonTitle }}</h3>
          <p>{{ text.pythonDesc }}</p>
        </div>

        <div class="python-row">
          <el-input
            :model-value="defaultPythonPath"
            readonly
            :placeholder="text.placeholder"
          />
          <el-button @click="pickDefaultPython">{{ text.browse }}</el-button>
          <el-button @click="clearDefaultPythonPath">{{ text.clear }}</el-button>
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

.theme-switcher {
  width: fit-content;
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
