<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { RouterView, useRoute, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'

import ActivityBarNav from './ActivityBarNav.vue'
import EditorHeader from './EditorHeader.vue'
import ExplorerSidebar from './ExplorerSidebar.vue'
import StatusBar from './StatusBar.vue'
import TerminalDock from './TerminalDock.vue'
import { useI18n } from '../../composables/useI18n'
import {
  ACTIVITY_BAR_WIDTH,
  MAX_SIDEBAR_WIDTH,
  MIN_EDITOR_WIDTH,
  MIN_SIDEBAR_WIDTH,
  SIDEBAR_SASH_WIDTH,
  MIN_TERMINAL_HEIGHT,
  DEFAULT_TERMINAL_HEIGHT,
  useWorkbenchStore,
} from '../../stores/workbench'
import { usePythonStore } from '../../stores/python'
import { useSettingsStore } from '../../stores/settings'
import { useTerminalsStore } from '../../stores/terminals'
import { useToolsStore } from '../../stores/tools'

type DragKind = 'none' | 'terminal' | 'sidebar'
type MenuPath = '/python' | '/tools' | '/tools/new' | '/settings'

const route = useRoute()
const router = useRouter()
const { t, formatSessionCount } = useI18n()
const settingsStore = useSettingsStore()
const toolsStore = useToolsStore()
const pythonStore = usePythonStore()
const terminalsStore = useTerminalsStore()
const workbenchStore = useWorkbenchStore()

const { defaultPythonPath, theme } = storeToRefs(settingsStore)
const { tools } = storeToRefs(toolsStore)
const { pythonPackages } = storeToRefs(pythonStore)
const { terminals, activeTerminalId, terminalOutputsById } = storeToRefs(terminalsStore)
const { sidebarWidth, sidebarVisible, terminalHeight, terminalExpanded } = storeToRefs(workbenchStore)

const workbenchMainRef = ref<HTMLElement | null>(null)
const editorStackRef = ref<HTMLElement | null>(null)
const dragKind = ref<DragKind>('none')
const isNarrowScreen = ref(false)
const isCompactScreen = ref(false)
const didAutoCollapseTerminalForCompact = ref(false)
const NARROW_SCREEN_BREAKPOINT = 1100
const COMPACT_SCREEN_BREAKPOINT = 860

const menuItems = computed(() => [
  { path: '/python' as const, glyph: 'PY', title: t('app.menu.python') },
  { path: '/tools' as const, glyph: 'TL', title: t('app.menu.tools') },
  { path: '/tools/new' as const, glyph: '+', title: t('app.menu.addTool') },
  { path: '/settings' as const, glyph: 'ST', title: t('app.settings') },
])

const activeMenu = computed<MenuPath>(() => {
  if (route.path.startsWith('/settings')) {
    return '/settings'
  }

  if (route.path.startsWith('/tools/new')) {
    return '/tools/new'
  }

  if (route.path.startsWith('/tools')) {
    return '/tools'
  }

  return '/python'
})

const activeMenuItem = computed(() => {
  return menuItems.value.find((item) => item.path === activeMenu.value) ?? menuItems.value[0]
})

const sessionCaption = computed(() => formatSessionCount(terminals.value.length))
const themeLabel = computed(() => t(theme.value === 'light' ? 'settings.themeLight' : 'settings.themeDark'))
const defaultPythonLabel = computed(() => {
  const raw = defaultPythonPath.value.trim()
  if (!raw) {
    return t('app.python.systemDefault')
  }

  return raw.split(/[\\/]/).pop() || raw
})
const currentViewSummary = computed(() => {
  if (activeMenu.value === '/python') {
    return t('app.status.packages', { count: pythonPackages.value.length })
  }

  if (activeMenu.value === '/settings') {
    return t('app.status.settings')
  }

  return t('app.status.tools', {
    total: tools.value.length,
    invalid: tools.value.filter((tool) => !tool.valid).length,
  })
})
const activeTerminalStatus = computed(() => {
  const terminal = terminals.value.find((item) => item.terminalId === activeTerminalId.value)
  if (!terminal) {
    return t('app.noActiveTerminal')
  }

  const shellName = terminal.shell.split(/[\\/]/).pop() || terminal.shell
  return `${shellName} (${t(`terminal.status.${terminal.status}`)})`
})
const effectiveSidebarWidth = computed(() => {
  if (!isNarrowScreen.value) {
    return sidebarWidth.value
  }

  if (typeof window === 'undefined') {
    return 300
  }

  return Math.max(240, Math.min(320, window.innerWidth - ACTIVITY_BAR_WIDTH - 24))
})
const showSidebarSash = computed(() => sidebarVisible.value && !isNarrowScreen.value)
const statusLeftItems = computed(() => {
  if (isNarrowScreen.value) {
    return [t('app.brand'), activeMenuItem.value?.title ?? '']
  }

  return [t('app.brand'), activeMenuItem.value?.title ?? '', currentViewSummary.value]
})
const statusRightItems = computed(() => {
  if (isNarrowScreen.value) {
    return [{ label: activeTerminalStatus.value }]
  }

  return [
    { label: `${t('settings.themeTitle')}: ${themeLabel.value}` },
    { label: `${t('settings.pythonTitle')}: ${defaultPythonLabel.value}`, title: defaultPythonPath.value || t('app.python.systemDefault') },
    { label: activeTerminalStatus.value },
  ]
})

function navigate(path: MenuPath): void {
  if (activeMenu.value === path) {
    workbenchStore.toggleSidebarVisible()
    return
  }

  router.push(path)

  if (isNarrowScreen.value) {
    workbenchStore.setSidebarVisible(false)
    return
  }

  workbenchStore.setSidebarVisible(true)
}

function getSidebarMaxWidth(): number {
  if (!workbenchMainRef.value) {
    return MAX_SIDEBAR_WIDTH
  }

  const rect = workbenchMainRef.value.getBoundingClientRect()
  const dynamicMax = Math.max(
    MIN_SIDEBAR_WIDTH,
    Math.round(rect.width - ACTIVITY_BAR_WIDTH - SIDEBAR_SASH_WIDTH - MIN_EDITOR_WIDTH),
  )

  return Math.min(MAX_SIDEBAR_WIDTH, dynamicMax)
}

function clampSidebarWidthIfNeeded(): void {
  if (isNarrowScreen.value) {
    return
  }

  workbenchStore.setSidebarWidth(sidebarWidth.value, getSidebarMaxWidth())
}

function updateViewportState(): void {
  if (typeof window === 'undefined') {
    return
  }

  const nextIsNarrow = window.innerWidth < NARROW_SCREEN_BREAKPOINT
  const nextIsCompact = window.innerWidth < COMPACT_SCREEN_BREAKPOINT
  const wasNarrow = isNarrowScreen.value
  const wasCompact = isCompactScreen.value
  isNarrowScreen.value = nextIsNarrow
  isCompactScreen.value = nextIsCompact

  if (nextIsCompact) {
    workbenchStore.setTerminalHeight(Math.min(terminalHeight.value, 180))

    if (!wasCompact && terminalExpanded.value && !didAutoCollapseTerminalForCompact.value) {
      workbenchStore.setTerminalExpanded(false)
      didAutoCollapseTerminalForCompact.value = true
    }
  } else if (!nextIsCompact && terminalHeight.value < DEFAULT_TERMINAL_HEIGHT) {
    workbenchStore.setTerminalHeight(DEFAULT_TERMINAL_HEIGHT)
  }

  if (nextIsNarrow) {
    onGlobalDragEnd()
    if (!wasNarrow) {
      workbenchStore.setSidebarVisible(false)
    }
    return
  }

  clampSidebarWidthIfNeeded()
}

function beginDrag(kind: DragKind, cursor: 'ns-resize' | 'ew-resize'): void {
  if (dragKind.value !== 'none') {
    return
  }

  dragKind.value = kind
  document.addEventListener('mousemove', onGlobalDragMove)
  document.addEventListener('mouseup', onGlobalDragEnd)
  document.body.style.cursor = cursor
  document.body.style.userSelect = 'none'
}

function onGlobalDragMove(event: MouseEvent): void {
  if (dragKind.value === 'terminal' && editorStackRef.value) {
    const rect = editorStackRef.value.getBoundingClientRect()
    const maxHeight = Math.max(MIN_TERMINAL_HEIGHT, rect.height - 160)
    const nextHeight = Math.round(rect.bottom - event.clientY)
    workbenchStore.setTerminalHeight(Math.max(MIN_TERMINAL_HEIGHT, Math.min(maxHeight, nextHeight)))

    if (!terminalExpanded.value) {
      workbenchStore.setTerminalExpanded(true)
    }
    return
  }

  if (dragKind.value === 'sidebar' && workbenchMainRef.value) {
    const rect = workbenchMainRef.value.getBoundingClientRect()
    const nextWidth = event.clientX - rect.left - ACTIVITY_BAR_WIDTH
    workbenchStore.setSidebarWidth(nextWidth, getSidebarMaxWidth())
  }
}

function onGlobalDragEnd(): void {
  if (dragKind.value === 'none') {
    return
  }

  dragKind.value = 'none'
  document.removeEventListener('mousemove', onGlobalDragMove)
  document.removeEventListener('mouseup', onGlobalDragEnd)
  document.body.style.cursor = ''
  document.body.style.userSelect = ''
}

function onWindowResize(): void {
  updateViewportState()
  clampSidebarWidthIfNeeded()
}

onMounted(() => {
  updateViewportState()
  clampSidebarWidthIfNeeded()
  window.addEventListener('resize', onWindowResize)
})

onBeforeUnmount(() => {
  onGlobalDragEnd()
  window.removeEventListener('resize', onWindowResize)
})
</script>

<template>
  <div class="flex h-screen flex-col overflow-hidden bg-workbench text-foreground" :class="{ 'is-dragging': dragKind !== 'none' }">
    <div ref="workbenchMainRef" class="flex min-h-0 flex-1 overflow-hidden">
      <ActivityBarNav :items="menuItems" :active-path="activeMenu" :compact="isCompactScreen" @navigate="navigate" />

      <div class="relative flex min-w-0 flex-1 overflow-hidden">
        <ExplorerSidebar
          v-if="sidebarVisible"
          :class="isNarrowScreen ? 'absolute inset-y-0 left-0 z-30' : ''"
          :items="menuItems"
          :active-path="activeMenu"
          :width="effectiveSidebarWidth"
          :overlay="isNarrowScreen"
          @navigate="navigate"
          @close="workbenchStore.setSidebarVisible(false)"
        />

        <button
          v-if="sidebarVisible && isNarrowScreen"
          type="button"
          class="absolute inset-0 z-20 bg-black/20 backdrop-blur-[1px]"
          aria-label="Close explorer"
          @click="workbenchStore.setSidebarVisible(false)"
        />

        <div
          v-if="showSidebarSash"
          :class="[
            'relative w-1 shrink-0 cursor-ew-resize bg-transparent',
            dragKind === 'sidebar' ? 'before:bg-accent' : 'before:bg-transparent hover:before:bg-accent',
          ]"
          @mousedown="beginDrag('sidebar', 'ew-resize')"
        >
          <span class="pointer-events-none absolute inset-y-0 left-0.5 w-px transition-colors before:bg-transparent" />
        </div>

        <section ref="editorStackRef" class="flex min-w-0 flex-1 flex-col bg-editor">
          <EditorHeader
            :glyph="activeMenuItem?.glyph ?? 'PY'"
            :title="activeMenuItem?.title ?? ''"
            :theme-label="themeLabel"
            :default-python-label="defaultPythonLabel"
            :default-python-title="defaultPythonPath || t('app.python.systemDefault')"
            :python-title="t('settings.pythonTitle')"
          />

          <main class="min-h-0 flex-1">
            <RouterView />
          </main>

          <TerminalDock
            :height="terminalHeight"
            :expanded="terminalExpanded"
            :terminals="terminals"
            :active-terminal-id="activeTerminalId"
            :outputs-by-terminal="terminalOutputsById"
            :session-caption="sessionCaption"
            :title="t('terminal.panel')"
            :toggle-label="terminalExpanded ? t('app.hide') : t('app.show')"
            @toggle="workbenchStore.toggleTerminalExpanded"
            @expand="workbenchStore.setTerminalExpanded(true)"
            @drag-start="beginDrag('terminal', 'ns-resize')"
            @select-terminal="terminalsStore.selectTerminal"
            @create-terminal="terminalsStore.createTerminal"
            @stop-terminal="terminalsStore.stopTerminal"
            @stop-all-terminals="terminalsStore.stopAllTerminals"
            @input="terminalsStore.sendTerminalInput"
            @resize-terminal="terminalsStore.resizeTerminal"
            @clear-output="terminalsStore.clearTerminalOutput"
          />
        </section>
      </div>
    </div>

    <StatusBar
      :left-items="statusLeftItems"
      :right-items="statusRightItems"
    />
  </div>
</template>

<style scoped>
.is-dragging :deep(.xterm),
.is-dragging :deep(iframe) {
  pointer-events: none;
}
</style>
