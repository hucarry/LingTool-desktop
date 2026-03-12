import { computed, ref } from 'vue'

import { messages, type MessageDictionary } from '../locales'

export type Locale = keyof typeof messages

type MessageParams = Record<string, string | number>

const LOCALE_STORAGE_KEY = 'toolhub.locale'
const dictionaries: Record<Locale, MessageDictionary> = messages

function loadLocale(): Locale {
  if (typeof window === 'undefined') {
    return 'zh-CN'
  }

  try {
    const saved = window.localStorage.getItem(LOCALE_STORAGE_KEY)
    if (saved === 'zh-CN' || saved === 'en-US') {
      return saved
    }
  } catch {
    // ignore
  }

  return 'zh-CN'
}

const locale = ref<Locale>(loadLocale())

function persistLocale(next: Locale): void {
  if (typeof window === 'undefined') {
    return
  }

  try {
    window.localStorage.setItem(LOCALE_STORAGE_KEY, next)
  } catch {
    // ignore
  }
}

function setLocale(next: Locale): void {
  if (locale.value === next) {
    return
  }

  locale.value = next
  persistLocale(next)
}

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
  const template = dictionaries[locale.value][key] ?? dictionaries['en-US'][key] ?? key
  return interpolate(template, params)
}

function formatSessionCount(count: number): string {
  if (locale.value === 'en-US') {
    return t(count === 1 ? 'app.session.one' : 'app.session.other', { count })
  }

  return t('app.sessions', { count })
}

export function useI18n() {
  return {
    locale: computed(() => locale.value),
    setLocale,
    t,
    formatSessionCount,
  }
}
