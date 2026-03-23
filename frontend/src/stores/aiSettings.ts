import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

const AI_API_BASE_KEY = 'toolhub.ai.apiBase'
const AI_API_KEY_KEY = 'toolhub.ai.apiKey'
const AI_MODEL_KEY = 'toolhub.ai.model'
const AI_SAVED_BASES_KEY = 'toolhub.ai.savedApiBases'
const AI_ENABLED_KEY = 'toolhub.ai.enabled'

function safeGet(key: string, defaultVal: string): string {
  if (typeof window === 'undefined') return defaultVal
  try {
    return window.localStorage.getItem(key) ?? defaultVal
  } catch {
    return defaultVal
  }
}

function safeSet(key: string, val: string) {
  if (typeof window === 'undefined') return
  try {
    window.localStorage.setItem(key, val)
  } catch {}
}

const defaultPresets = [
  'https://api.openai.com/v1',
  'https://api.deepseek.com/v1',
  'https://dashscope.aliyuncs.com/compatible-mode/v1'
]

export const useAiSettingsStore = defineStore('aiSettings', () => {
  const isCloudModelEnabled = ref(safeGet(AI_ENABLED_KEY, 'false') === 'true')
  const apiBase = ref(safeGet(AI_API_BASE_KEY, 'https://api.openai.com/v1'))
  const apiKey = ref(safeGet(AI_API_KEY_KEY, ''))
  const model = ref(safeGet(AI_MODEL_KEY, 'gpt-3.5-turbo'))

  // 解析历史保存的 URL 列表
  let initSaved: string[]
  try {
    const rawSaved = safeGet(AI_SAVED_BASES_KEY, '')
    initSaved = rawSaved ? JSON.parse(rawSaved) : defaultPresets
  } catch {
    initSaved = defaultPresets
  }
  const savedApiBases = ref<string[]>(initSaved)

  watch(isCloudModelEnabled, (val) => safeSet(AI_ENABLED_KEY, val.toString()))
  watch(apiBase, (val) => safeSet(AI_API_BASE_KEY, val))
  watch(apiKey, (val) => safeSet(AI_API_KEY_KEY, val))
  watch(model, (val) => safeSet(AI_MODEL_KEY, val))
  watch(savedApiBases, (val) => safeSet(AI_SAVED_BASES_KEY, JSON.stringify(val)), { deep: true })

  return { isCloudModelEnabled, apiBase, apiKey, model, savedApiBases }
})
