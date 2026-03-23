<script setup lang="ts">
import { ref as vueRef, computed } from 'vue'
import { storeToRefs } from 'pinia'
import UiButton from '../components/ui/UiButton.vue'
import UiInput from '../components/ui/UiInput.vue'
import UiPanel from '../components/ui/UiPanel.vue'
import { useI18n } from '../composables/useI18n'
import { useAiSettingsStore } from '../stores/aiSettings'

const { t } = useI18n()
const aiSettingsStore = useAiSettingsStore()
const { isCloudModelEnabled, apiBase, apiKey, model, savedApiBases } = storeToRefs(aiSettingsStore)

function saveCurrentApiBase() {
  const url = apiBase.value.trim()
  if (url && !savedApiBases.value.includes(url)) {
    savedApiBases.value.push(url)
  }
}

function removeApiBase(urlToRemove: string) {
  savedApiBases.value = savedApiBases.value.filter(u => u !== urlToRemove)
}

const isTesting = vueRef(false)
const testResult = vueRef<{ success: boolean; message: string; models: string[] } | null>(null)
const activeCategoryTab = vueRef('全部')
const searchQuery = vueRef('')

const isTestingModel = vueRef(false)
const testModelMessage = vueRef<{ success: boolean; text: string } | null>(null)

async function copyToClipboard(text: string) {
  try {
    await navigator.clipboard.writeText(text)
    if (testResult.value) {
      testResult.value.message = `📋 已成功复制模型名至剪贴板：${text}`
    }
  } catch (err) { }
}

async function testSpecificModel() {
  if (!apiBase.value || !model.value) return
  isTestingModel.value = true
  testModelMessage.value = null
  try {
    const baseUrl = apiBase.value.trim().replace(/\/+$/, '')
    const payload = {
      model: model.value.trim(),
      messages: [{ role: 'user', content: '这是一条连接连通性测试消息，如果收到请只回复：OK' }],
      max_tokens: 20
    }
    
    const res = await fetch(`${baseUrl}/chat/completions`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(apiKey.value.trim() ? { 'Authorization': `Bearer ${apiKey.value.trim()}` } : {})
      },
      body: JSON.stringify(payload)
    })
    
    if (!res.ok) {
      let errStr = res.statusText
      try { 
        const j = await res.json()
        if (j.error) errStr = j.error.message || j.error 
      } catch(e) {}
      throw new Error(`HTTP ${res.status}: ${errStr}`)
    }
    
    const data = await res.json()
    if (data.choices && data.choices[0] && data.choices[0].message) {
      const txt = data.choices[0].message.content || '[无正文]'
      testModelMessage.value = { success: true, text: `✅ 测试通过！成功调用此模型。它的回答片段: "${txt.slice(0, 30)}..."` }
    } else {
      throw new Error('未收到标准的大语言模型格式回复。')
    }
  } catch (e: any) {
    testModelMessage.value = { success: false, text: `❌ 调用该模型失败: ${e.message}` }
  } finally {
    isTestingModel.value = false
  }
}

const DOMESTIC_VENDORS = ['deepseek', 'moonshot', 'zhipu', 'glm', 'qwen', 'alibaba', 'tencent', 'hunyuan', 'baichuan', 'yi', '01-ai', 'stepfun', 'sensevoice', 'sensenova', 'fnlp', 'inclusionai', 'netease', 'zai-org', 'internlm']
const OVERSEAS_VENDORS = ['openai', 'gpt', 'claude', 'anthropic', 'gemini', 'google', 'meta', 'llama', 'mistral', 'cohere']

function classifyModel(modelId: string) {
  let vendor = '其他 / 未归类'
  const lower = modelId.toLowerCase()

  if (modelId.includes('/')) {
    vendor = modelId.split('/')[0] || '默认厂商'
  } else {
    if (lower.startsWith('gpt-') || lower.startsWith('o1') || lower.startsWith('text-')) vendor = 'openai'
    else if (lower.startsWith('claude-')) vendor = 'anthropic'
    else if (lower.startsWith('gemini-')) vendor = 'google'
    else if (lower.startsWith('qwen')) vendor = 'alibaba'
    else if (lower.startsWith('glm') || lower.startsWith('chatglm')) vendor = 'ZhipuAI'
    else if (lower.startsWith('deepseek')) vendor = 'deepseek'
    else if (lower.startsWith('llama')) vendor = 'meta'
  }

  const vLower = vendor.toLowerCase()
  let category = '其他开源/未分类'
  
  if (DOMESTIC_VENDORS.some(v => vLower.includes(v) || lower.includes(v))) {
    category = '国产大模型'
  } else if (OVERSEAS_VENDORS.some(v => vLower.includes(v) || lower.includes(v))) {
    category = '海外大厂'
  }

  return { category, vendor }
}

const modelGroups = computed(() => {
  const result = testResult.value
  if (!result || !Array.isArray(result.models)) return {}
  
  const groups: Record<string, Record<string, string[]>> = {
    '国产大模型': {},
    '海外大厂': {},
    '其他开源/未分类': {}
  }
  
  const query = searchQuery.value.trim().toLowerCase()
  const filteredModels = query ? result.models.filter(m => m.toLowerCase().includes(query)) : result.models
  
  for (const m of filteredModels) {
    const { category, vendor } = classifyModel(m)
    const catGroup = groups[category] ?? (groups[category] = {})
    if (!catGroup[vendor]) catGroup[vendor] = []
    catGroup[vendor].push(m)
  }
  
  return groups
})

async function testConnection() {
  if (!apiBase.value) return
  isTesting.value = true
  testResult.value = null
  try {
    const baseUrl = apiBase.value.trim().replace(/\/+$/, '')
    const res = await fetch(`${baseUrl}/models`, {
      headers: apiKey.value.trim() ? {
        'Authorization': `Bearer ${apiKey.value.trim()}`
      } : undefined
    })
    
    if (!res.ok) {
      if (res.status === 401) throw new Error('401 认证失败 (API 密钥错误)')
      if (res.status === 404) throw new Error('404 找不到接口路径 (检查 API 基础地址)')
      const errText = await res.text().catch(() => '')
      throw new Error(`HTTP ${res.status}: ${res.statusText} ${errText}`.trim())
    }
    
    const data = await res.json()
    if (data.data && Array.isArray(data.data)) {
       const models = data.data.map((m: any) => m.id).sort()
       testResult.value = { success: true, message: `✅ 连接成功！共找到 ${models.length} 个可用模型。`, models }
    } else {
       throw new Error('响应格式不正确，缺少 OpenAI 标准的 data 模型字段数组。')
    }
  } catch (e: any) {
    testResult.value = { success: false, message: `❌ 连接测试失败: ${e.message}`, models: [] }
  } finally {
    isTesting.value = false
  }
}
</script>

<template>
  <section class="h-full overflow-auto p-3">
    <div class="flex flex-col gap-3">
      <UiPanel class="flex items-center justify-between gap-4">
        <div class="space-y-1.5">
          <h2 class="text-lg font-semibold text-foreground">{{ t('aiSettings.title') }}</h2>
          <p class="max-w-3xl text-sm leading-6 text-muted">{{ t('aiSettings.description') }}（当前启用状态将决定界面是否加载云端引擎）</p>
        </div>
        <button 
          @click="isCloudModelEnabled = !isCloudModelEnabled"
          class="relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none"
          :class="isCloudModelEnabled ? 'bg-accent' : 'bg-border/60'"
          role="switch"
          :aria-checked="isCloudModelEnabled"
        >
          <span 
            class="pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out"
            :class="isCloudModelEnabled ? 'translate-x-5' : 'translate-x-0'"
          ></span>
        </button>
      </UiPanel>

      <UiPanel class="flex flex-col gap-3">
        <div class="space-y-1">
          <h3 class="text-sm font-semibold text-foreground">{{ t('aiSettings.apiBase') }}</h3>
        </div>
        <div class="max-w-xl flex flex-col gap-2">
          <div class="flex gap-2">
            <UiInput v-model="apiBase" list="saved-api-bases" :placeholder="t('aiSettings.apiBasePlaceholder')" class="w-full flex-1" />
            <UiButton @click="saveCurrentApiBase" :disabled="!apiBase.trim() || savedApiBases.includes(apiBase.trim())" variant="primary" class="shrink-0 whitespace-nowrap px-4 bg-accent/90 hover:bg-accent text-white shadow-sm border border-accent/20">
              添加为常用
            </UiButton>
          </div>
          <datalist id="saved-api-bases">
            <option v-for="url in savedApiBases" :key="url" :value="url" />
          </datalist>
          
          <div class="flex flex-wrap gap-2 mt-1">
            <span 
              v-for="url in savedApiBases" 
              :key="url"
              @click="apiBase = url"
              class="inline-flex items-center gap-1.5 px-3 py-1 text-[0.8rem] rounded-md border cursor-pointer transition-colors shadow-xs"
              :class="apiBase === url ? 'bg-accent/15 text-accent border-accent/30' : 'bg-editor border-border/60 text-muted hover:text-foreground hover:bg-hovered'"
            >
              {{ url.replace('https://', '') }}
              <button 
                @click.stop="removeApiBase(url)" 
                class="ml-0.5 mt-[1px] opacity-40 hover:opacity-100 hover:text-danger hover:bg-danger/20 rounded-full w-4 h-4 flex items-center justify-center transition-all"
                title="删除此常用地址"
              >
                <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </span>
          </div>
        </div>
      </UiPanel>

      <UiPanel class="flex flex-col gap-3">
        <div class="space-y-1">
          <h3 class="text-sm font-semibold text-foreground">{{ t('aiSettings.apiKey') }}</h3>
        </div>
        <div class="max-w-xl">
          <UiInput v-model="apiKey" type="password" :placeholder="t('aiSettings.apiKeyPlaceholder')" class="w-full" />
        </div>
      </UiPanel>

      <UiPanel class="flex flex-col gap-3">
        <div class="space-y-1">
          <h3 class="text-sm font-semibold text-foreground">{{ t('aiSettings.model') }} & 专属测试</h3>
          <p class="text-[0.8rem] text-muted">输入你想使用的明确模型名，并可点击专有按钮发送模拟对话验证其可用性。</p>
        </div>
        <div class="max-w-2xl">
          <div class="flex gap-2 items-center">
            <UiInput v-model="model" :placeholder="t('aiSettings.modelPlaceholder')" class="flex-1" />
            <UiButton @click="testSpecificModel" :disabled="isTestingModel || !model" variant="primary">
              {{ isTestingModel ? '正在向大模型发消息...' : '测试当前模型 (Test Prompt)' }}
            </UiButton>
          </div>
          <div v-if="testModelMessage" class="mt-2 text-[0.8rem] font-bold p-2 rounded-md bg-editor border-l-4" :class="testModelMessage.success ? 'border-l-green-500 text-green-600' : 'border-l-danger text-danger'">
            {{ testModelMessage.text }}
          </div>
        </div>
      </UiPanel>

      <!-- 测试版面 -->
      <UiPanel class="flex flex-col gap-4 mt-2">
        <div class="space-y-1 border-b border-border/50 pb-3">
          <h3 class="text-sm font-semibold text-foreground">API 连接状态与模型库嗅探 (Global Connectivity)</h3>
          <p class="text-[0.8rem] text-muted">发送网络探测请求，获取该 API 基础地址下所有由厂商提供的公开模型。</p>
        </div>
        
        <div class="flex items-center gap-3">
          <UiButton @click="testConnection" :disabled="isTesting" :variant="isTesting ? 'default' : 'primary'">
             {{ isTesting ? '正在嗅探云端信息...' : '开始探测可用大模型库' }}
          </UiButton>
        </div>
        
        <div v-if="testResult" class="p-4 rounded-lg border text-sm transition-all shadow-sm" :class="testResult.success ? 'bg-green-500/10 border-green-500/30 text-green-600' : 'bg-danger/10 border-danger/30 text-danger'">
          <div class="font-bold mb-1">{{ testResult.message }}</div>
          
          <div v-if="testResult.success && testResult.models.length > 0" class="mt-4 flex flex-col gap-3">
            <div class="flex flex-wrap items-center justify-between gap-3">
              <div class="flex items-center gap-3">
                <span class="text-[0.8rem] text-foreground font-medium opacity-80">智能分类筛选：</span>
                <div class="flex gap-1.5 p-1 bg-editor rounded-lg border border-border/80 shadow-inner">
                  <button 
                    v-for="cat in ['全部', '国产大模型', '海外大厂', '其他开源/未分类']" :key="cat"
                    class="px-3 py-1.5 text-[0.8rem] rounded-md transition-all font-medium"
                    :class="activeCategoryTab === cat ? 'bg-accent text-white shadow-sm' : 'text-muted hover:text-foreground hover:bg-hovered'"
                    @click="activeCategoryTab = cat"
                    type="button"
                  >
                    {{ cat }}
                  </button>
                </div>
              </div>
              
              <div class="flex-1 max-w-xs min-w-[200px] ml-auto">
                <UiInput v-model="searchQuery" placeholder="搜索可用模型..." class="w-full h-8 min-h-[32px]! text-xs! px-2" />
              </div>
            </div>

            <div class="w-full h-px bg-border/40 my-1"></div>

            <div class="flex flex-col gap-6 max-h-[380px] overflow-y-auto pr-3 scrollbar-thin scrollbar-thumb-border/40">
              <template v-for="(categoryObj, categoryName) in modelGroups" :key="categoryName">
                <div v-show="activeCategoryTab === '全部' || activeCategoryTab === categoryName" class="flex flex-col gap-4">
                  <h4 class="text-[0.85rem] font-bold text-accent px-1" v-if="activeCategoryTab === '全部' && Object.keys(categoryObj).length > 0">
                    {{ categoryName }}
                  </h4>
                  
                  <div v-for="(modelsList, vendor) in categoryObj" :key="vendor" class="flex flex-col gap-2 pl-2">
                    <span class="text-[0.75rem] font-bold text-foreground/70 uppercase tracking-widest">{{ vendor }}</span>
                    <div class="flex flex-wrap gap-2 text-[0.8rem]">
                      <button 
                        v-for="m in modelsList" :key="m"
                        class="group relative px-2.5 py-1.5 bg-editor border border-border/80 text-foreground rounded-md shadow-sm hover:border-accent hover:text-accent hover:shadow-accent/20 transition-all active:scale-95 text-left flex items-center gap-1.5"
                        @click="model = m"
                        @dblclick="copyToClipboard(m)"
                        type="button"
                        :title="'名字: ' + m + '\n单击：应用为当前模型\n双击：复制模型名称'"
                      >
                        <span v-if="model === m" class="w-1.5 h-1.5 rounded-full bg-green-500 shrink-0"></span>
                        <span class="truncate max-w-[240px]">{{ m.includes('/') ? m.split('/')[1] : m }}</span>
                      </button>
                    </div>
                  </div>
                </div>
              </template>
            </div>
          </div>
        </div>
      </UiPanel>
    </div>
  </section>
</template>
