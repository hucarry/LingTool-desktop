import { storeToRefs } from 'pinia'

import { useSettingsStore } from '../stores/settings'

export function useSettings() {
  const settingsStore = useSettingsStore()
  const refs = storeToRefs(settingsStore)

  return {
    ...refs,
    pickDefaultPython: settingsStore.pickDefaultPython,
    setTheme: settingsStore.setTheme,
    setLocale: settingsStore.setLocale,
    setDefaultPythonPath: settingsStore.setDefaultPythonPath,
    clearDefaultPythonPath: settingsStore.clearDefaultPythonPath,
  }
}
