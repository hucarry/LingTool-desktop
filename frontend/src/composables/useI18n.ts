import { computed } from 'vue'

import { messages, type MessageDictionary } from '../locales'
import { useSettingsStore } from '../stores/settings'

export type Locale = keyof typeof messages

type MessageParams = Record<string, string | number>
const dictionaries: Record<Locale, MessageDictionary> = messages

function interpolate(template: string, params?: MessageParams): string {
  if (!params) {
    return template
  }

  return template.replace(/\{([a-zA-Z0-9_]+)\}/g, (full, key: string) => {
    const value = params[key]
    return value === undefined ? full : String(value)
  })
}

function t(key: string, params?: MessageParams): string {
  const settingsStore = useSettingsStore()
  const template = dictionaries[settingsStore.locale][key] ?? dictionaries['en-US'][key] ?? key
  return interpolate(template, params)
}

function formatSessionCount(count: number): string {
  const settingsStore = useSettingsStore()

  if (settingsStore.locale === 'en-US') {
    return t(count === 1 ? 'app.session.one' : 'app.session.other', { count })
  }

  return t('app.sessions', { count })
}

export function useI18n() {
  const settingsStore = useSettingsStore()

  return {
    locale: computed(() => settingsStore.locale),
    setLocale: settingsStore.setLocale,
    t,
    formatSessionCount,
  }
}
