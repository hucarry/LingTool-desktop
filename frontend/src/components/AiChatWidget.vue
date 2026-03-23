<script setup lang="ts">
import { ref, reactive, nextTick, onMounted } from 'vue'

const isOpen = ref(false)
const position = reactive({ x: 100, y: 100 })
const isDragging = ref(false)
const snapState = ref<'none' | 'left' | 'right'>('none')
const isEdgeCollapsed = ref(false)

const dragStart = { x: 0, y: 0, origX: 0, origY: 0 }

onMounted(() => {
  // 初次加载时靠右吸附
  position.x = window.innerWidth - 360
  position.y = window.innerHeight - 560
  snapState.value = 'right'
})

function toggleOpen() {
  isOpen.value = !isOpen.value
  if (isOpen.value) {
    isEdgeCollapsed.value = false
    scrollToBottom()
  }
}

function toggleEdgeCollapse() {
  isEdgeCollapsed.value = !isEdgeCollapsed.value
}

function onMouseDown(e: MouseEvent) {
  isDragging.value = false
  dragStart.x = e.clientX
  dragStart.y = e.clientY
  dragStart.origX = position.x
  dragStart.origY = position.y
  
  window.addEventListener('mousemove', onMouseMove)
  window.addEventListener('mouseup', onMouseUp)
}

function onMouseMove(e: MouseEvent) {
  isDragging.value = true
  let newX = dragStart.origX + (e.clientX - dragStart.x)
  let newY = dragStart.origY + (e.clientY - dragStart.y)
  
  const width = 360
  const height = 520
  const snapDist = 40 // 吸附阈值 40px
  
  const maxX = window.innerWidth - width
  const maxY = window.innerHeight - height

  // 左右磁吸与边界
  if (newX < snapDist) {
    newX = 0
    snapState.value = 'left'
  } else if (newX > maxX - snapDist) {
    newX = maxX
    snapState.value = 'right'
  } else {
    snapState.value = 'none'
  }

  // 被拖动时解除边缘折叠状态
  isEdgeCollapsed.value = false

  // 上下磁吸与边界
  if (newY < snapDist) newY = 0
  else if (newY > maxY - snapDist) newY = maxY

  position.x = newX
  position.y = newY
}

function onMouseUp() {
  window.removeEventListener('mousemove', onMouseMove)
  window.removeEventListener('mouseup', onMouseUp)
}

// ------------------------
// Chat 通信模块 (Mock)
// ------------------------
const inputMessage = ref('')
const messageListRef = ref<HTMLElement | null>(null)
const isTyping = ref(false)

const messages = ref<{role: 'user'|'ai', content: string}[]>([
  { role: 'ai', content: '您好！我是您的 AI 小助手～由于这是初代预览版，后端的大模型连接（C# 与 OpenAI 接口桥接）将在下一步替您打通！目前您可随意点开并尝试随意拖拽。' }
])

function scrollToBottom() {
  nextTick(() => {
    if (messageListRef.value) {
      messageListRef.value.scrollTop = messageListRef.value.scrollHeight + 100
    }
  })
}

function sendMessage() {
  if (!inputMessage.value.trim() || isTyping.value) return
  
  messages.value.push({ role: 'user', content: inputMessage.value })
  const question = inputMessage.value
  inputMessage.value = ''
  scrollToBottom()
  
  // TODO: 后续在这里通过桥接发送指令给 C# 后端
  // window.external.sendMessage({ type: "AiChat", text: question })
  
  isTyping.value = true
  setTimeout(() => {
    messages.value.push({ role: 'ai', content: `（正在等待接入 OpenAI API... 你刚才说了："${question}"）` })
    isTyping.value = false
    scrollToBottom()
  }, 1000)
}
</script>

<template>
  <div class="ai-chat-widget">
    <!-- 悬浮收缩球 -->
    <Transition name="fade-scale">
      <div
        v-show="!isOpen"
        class="fixed bottom-8 right-8 z-90 flex h-14 w-14 cursor-pointer items-center justify-center rounded-full bg-linear-to-tr from-accent to-blue-500 text-white shadow-2xl transition-all duration-300 hover:scale-110 hover:shadow-accent/30 active:scale-95"
        @click="toggleOpen"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="h-7 w-7" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.8">
          <path stroke-linecap="round" stroke-linejoin="round" d="M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-5 5v-5z" />
        </svg>
      </div>
    </Transition>

    <!-- 弹出的可拖动面板 -->
    <Transition name="chat-panel">
      <div
        v-if="isOpen"
        :style="{ left: position.x + 'px', top: position.y + 'px' }"
        class="fixed z-100 flex h-[520px] w-[360px] flex-col overflow-visible rounded-2xl shadow-[0_20px_60px_-15px_rgba(0,0,0,0.3)] duration-300 ease-in-out transition-transform"
        :class="{ '-translate-x-full': isEdgeCollapsed && snapState === 'left', 'translate-x-full': isEdgeCollapsed && snapState === 'right' }"
      >
        <!-- 主内容包裹层 -->
        <div class="flex h-full w-full flex-col overflow-hidden rounded-2xl border border-border/60 bg-editor/80 backdrop-blur-3xl">
          <!-- Header (可拖拽区) -->
          <div
            class="flex cursor-move select-none items-center justify-between border-b border-border/40 bg-white/5 p-3.5"
          @mousedown="onMouseDown"
        >
          <div class="flex items-center gap-2.5">
            <div class="flex h-9 w-9 items-center justify-center rounded-full bg-linear-to-br from-accent to-blue-600 text-white shadow-md">
              <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd" d="M10 2a1 1 0 00-1 1v1a1 1 0 002 0V3a1 1 0 00-1-1zM4 4h3a3 3 0 006 0h3a2 2 0 012 2v9a2 2 0 01-2 2H4a2 2 0 01-2-2V6a2 2 0 012-2zm2.5 7a1.5 1.5 0 100-3 1.5 1.5 0 000 3zm2.45 4a2.5 2.5 0 10-4.9 0h4.9zM12 9.5a1.5 1.5 0 100-3 1.5 1.5 0 000 3zm-1.05 5.5a2.5 2.5 0 104.9 0h-4.9z" clip-rule="evenodd" />
              </svg>
            </div>
            <div class="flex flex-col">
              <span class="text-sm font-semibold tracking-wide text-foreground">AI 引擎助手</span>
              <span class="text-[10px] text-muted">基于大模型服务</span>
            </div>
          </div>
          <button
            class="rounded-lg p-1.5 text-muted transition-colors hover:bg-hovered/80 hover:text-foreground active:scale-95"
            @click.stop="toggleOpen"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <!-- 聊天流 -->
        <div ref="messageListRef" class="scrollbar-thin scrollbar-thumb-border/60 scrollbar-track-transparent flex-1 scroll-smooth overflow-y-auto p-4 space-y-5">
          <template v-for="(msg, idx) in messages" :key="idx">
            <!-- AI -->
            <div v-if="msg.role === 'ai'" class="flex items-start gap-2.5 pr-8">
              <div class="mt-1 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-accent/10 text-[10px] font-bold text-accent">AI</div>
              <div class="rounded-2xl rounded-tl-sm border border-border/30 bg-sidebar/80 px-3.5 py-2.5 text-[0.9rem] leading-snug text-foreground shadow-sm">
                {{ msg.content }}
              </div>
            </div>
            
            <!-- User -->
            <div v-else class="flex items-end justify-end gap-2.5 pl-8">
              <div class="rounded-2xl rounded-tr-sm bg-accent px-3.5 py-2.5 text-[0.9rem] leading-snug text-white shadow-md">
                {{ msg.content }}
              </div>
            </div>
          </template>
          
          <!-- Typing indicator -->
          <div v-if="isTyping" class="flex items-start gap-2.5 pr-8 opacity-70">
            <div class="mt-1 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-accent/10 text-[10px] font-bold text-accent">AI</div>
            <div class="flex items-center gap-1.5 rounded-2xl rounded-tl-sm border border-border/30 bg-sidebar/80 px-3.5 py-3 text-[0.9rem] text-foreground shadow-sm">
              <span class="h-1.5 w-1.5 animate-bounce rounded-full bg-current"></span>
              <span class="h-1.5 w-1.5 animate-bounce rounded-full bg-current" style="animation-delay: 0.15s"></span>
              <span class="h-1.5 w-1.5 animate-bounce rounded-full bg-current" style="animation-delay: 0.3s"></span>
            </div>
          </div>
        </div>

        <!-- 输入控制栏 -->
        <div class="border-t border-border/40 bg-sidebar/50 p-3">
          <div class="flex items-end gap-2 rounded-xl border border-border/80 bg-editor/80 px-2 py-1.5 shadow-inner transition-colors focus-within:border-accent/60">
            <textarea
              v-model="inputMessage"
              rows="1"
              class="w-full resize-none bg-transparent py-1.5 pl-2 pr-1 text-[0.9rem] text-foreground outline-none placeholder:text-muted/60"
              placeholder="请输入任何你想问的问题..."
              @keyup.enter.prevent="sendMessage"
            ></textarea>
            <button
              class="mb-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-accent text-white shadow-sm transition hover:bg-accent/90 active:scale-95 disabled:opacity-40"
              :disabled="!inputMessage.trim() || isTyping"
              @click="sendMessage"
            >
              <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M14 5l7 7m0 0l-7 7m7-7H3" />
              </svg>
            </button>
          </div>
        </div>
        </div>

        <!-- 边缘折叠把手 -->
        <div
          v-if="snapState === 'left' || snapState === 'right'"
          class="absolute top-1/2 z-10 flex h-20 w-5 -translate-y-1/2 cursor-pointer items-center justify-center border border-border/60 bg-editor/90 text-muted shadow-lg backdrop-blur transition-all duration-300 hover:bg-accent hover:text-white"
          :class="snapState === 'left' ? '-right-[19px] rounded-r-lg border-l-0' : '-left-[19px] rounded-l-lg border-r-0'"
          @click.stop="toggleEdgeCollapse"
        >
          <!-- SVG 箭头 -->
          <svg v-if="snapState === 'left'" xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
             <path stroke-linecap="round" stroke-linejoin="round" :d="isEdgeCollapsed ? 'M5 15l7-7 7 7' : 'M15 19l-7-7 7-7'" />
          </svg>
          <svg v-else xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
             <path stroke-linecap="round" stroke-linejoin="round" :d="isEdgeCollapsed ? 'M15 19l-7-7 7-7' : 'M9 5l7 7-7 7'" />
          </svg>
        </div>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.fade-scale-enter-active,
.fade-scale-leave-active {
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}
.fade-scale-enter-from,
.fade-scale-leave-to {
  opacity: 0;
  transform: scale(0.8);
}

.chat-panel-enter-active,
.chat-panel-leave-active {
  transition: all 0.35s cubic-bezier(0.16, 1, 0.3, 1);
}
.chat-panel-enter-from,
.chat-panel-leave-to {
  opacity: 0;
  transform: translateY(20px) scale(0.95);
}
</style>
