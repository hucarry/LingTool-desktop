<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ToolRunner from './components/ToolRunner.vue'
import TerminalPanel from './components/TerminalPanel.vue'
import { useToolHub } from './composables/useToolHub'
import { useI18n, type Locale } from './composables/useI18n'
import { useSettings } from './composables/useSettings'

interface MenuItem {
  path: '/python' | '/tools' | '/tools/new' | '/settings'
  iconId: string
  titleKey?: string
  title?: Record<Locale, string>
}

const hub = useToolHub()
const route = useRoute()
const router = useRouter()
const { locale, t, formatSessionCount } = useI18n()
const { defaultPythonPath } = useSettings()

const runnerVisible = hub.runnerVisible
const pythonOverride = hub.pythonOverride
const activeTool = hub.activeTool

type DragKind = 'none' | 'terminal' | 'sidebar'

const SIDEBAR_WIDTH_STORAGE_KEY = 'toolhub.sidebarWidth'
const SIDEBAR_VISIBLE_STORAGE_KEY = 'toolhub.sidebarVisible'
const ACTIVITY_BAR_WIDTH = 48
const SIDEBAR_SASH_WIDTH = 4
const MIN_EDITOR_WIDTH = 420
const MIN_SIDEBAR_WIDTH = 180
const DEFAULT_SIDEBAR_WIDTH = 248
const MAX_SIDEBAR_WIDTH = 560

const MIN_TERMINAL_HEIGHT = 140
const DEFAULT_TERMINAL_HEIGHT = 260
const COLLAPSED_TERMINAL_HEIGHT = 34

const terminalHeight = ref(DEFAULT_TERMINAL_HEIGHT)
const terminalExpanded = ref(true)
const dragKind = ref<DragKind>('none')
const isDragging = computed(() => dragKind.value !== 'none')
const isSidebarDragging = computed(() => dragKind.value === 'sidebar')
const sidebarWidth = ref(loadSidebarWidth())
const sidebarVisible = ref(loadSidebarVisible())
const workbenchMainRef = ref<HTMLElement>()
const editorStackRef = ref<HTMLElement>()

const menuItems: MenuItem[] = [
  { path: '/python', iconId: 'icon-code', titleKey: 'app.menu.python' },
  { path: '/tools', iconId: 'icon-folder', titleKey: 'app.menu.tools' },
  {
    path: '/tools/new',
    iconId: 'icon-add',
    title: {
      'zh-CN': '新增工具',
      'en-US': 'Add Tool',
    },
  },
]

const settingsMenuItem: MenuItem = {
  path: '/settings',
  iconId: 'icon-setting',
  title: {
    'zh-CN': '设置',
    'en-US': 'Settings',
  },
}

const fallbackMenuItem: MenuItem = menuItems[0]!
const sideMenuItems: MenuItem[] = [...menuItems, settingsMenuItem]

const activeMenu = computed<MenuItem['path']>(() => {
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
  return sideMenuItems.find((item) => item.path === activeMenu.value) ?? fallbackMenuItem
})

const panelHeight = computed(() => {
  return terminalExpanded.value ? `${terminalHeight.value}px` : `${COLLAPSED_TERMINAL_HEIGHT}px`
})

const activeTerminalStatus = computed(() => {
  const terminal = hub.terminals.value.find((item) => item.terminalId === hub.activeTerminalId.value)
  if (!terminal) {
    return t('app.noActiveTerminal')
  }

  const shellName = terminal.shell.split(/[\\/]/).pop() || terminal.shell
  return `${shellName} (${t(`terminal.status.${terminal.status}`)})`
})

const terminalSessionCaption = computed(() => {
  return formatSessionCount(hub.terminals.value.length)
})

const showSidebar = computed(() => sidebarVisible.value)

function menuTitle(item: MenuItem): string {
  if (item.titleKey) {
    return t(item.titleKey)
  }

  return item.title?.[locale.value] ?? item.path
}

function navigate(path: MenuItem['path']): void {
  if (activeMenu.value === path) {
    return
  }

  router.push(path)
}

function loadSidebarWidth(): number {
  if (typeof window === 'undefined') {
    return DEFAULT_SIDEBAR_WIDTH
  }

  try {
    const raw = window.localStorage.getItem(SIDEBAR_WIDTH_STORAGE_KEY)
    const parsed = raw ? Number.parseInt(raw, 10) : Number.NaN
    if (Number.isFinite(parsed)) {
      return Math.max(MIN_SIDEBAR_WIDTH, Math.min(MAX_SIDEBAR_WIDTH, parsed))
    }
  } catch {
    // ignore
  }

  return DEFAULT_SIDEBAR_WIDTH
}

function loadSidebarVisible(): boolean {
  if (typeof window === 'undefined') {
    return true
  }

  try {
    const raw = window.localStorage.getItem(SIDEBAR_VISIBLE_STORAGE_KEY)
    if (raw === 'false') {
      return false
    }
  } catch {
    // ignore
  }

  return true
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

function setSidebarWidth(nextWidth: number): void {
  const maxWidth = getSidebarMaxWidth()
  const normalized = Math.max(MIN_SIDEBAR_WIDTH, Math.min(maxWidth, Math.round(nextWidth)))
  sidebarWidth.value = normalized

  if (typeof window !== 'undefined') {
    try {
      window.localStorage.setItem(SIDEBAR_WIDTH_STORAGE_KEY, String(normalized))
    } catch {
      // ignore
    }
  }
}

function setSidebarVisible(nextVisible: boolean): void {
  sidebarVisible.value = nextVisible

  if (typeof window !== 'undefined') {
    try {
      window.localStorage.setItem(SIDEBAR_VISIBLE_STORAGE_KEY, String(nextVisible))
    } catch {
      // ignore
    }
  }
}

function toggleSidebarVisibility(): void {
  if (dragKind.value === 'sidebar') {
    onGlobalDragEnd()
  }

  setSidebarVisible(!sidebarVisible.value)
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

function onTerminalDragStart(event: MouseEvent): void {
  event.preventDefault()
  beginDrag('terminal', 'ns-resize')
}

function onSidebarDragStart(event: MouseEvent): void {
  event.preventDefault()
  beginDrag('sidebar', 'ew-resize')
}

function onGlobalDragMove(event: MouseEvent): void {
  if (dragKind.value === 'terminal' && editorStackRef.value) {
    const rect = editorStackRef.value.getBoundingClientRect()
    const maxHeight = Math.max(MIN_TERMINAL_HEIGHT, rect.height - 160)
    const nextHeight = Math.round(rect.bottom - event.clientY)

    terminalHeight.value = Math.max(MIN_TERMINAL_HEIGHT, Math.min(maxHeight, nextHeight))

    if (!terminalExpanded.value) {
      terminalExpanded.value = true
    }
    return
  }

  if (dragKind.value === 'sidebar' && workbenchMainRef.value) {
    const rect = workbenchMainRef.value.getBoundingClientRect()
    const nextWidth = event.clientX - rect.left - ACTIVITY_BAR_WIDTH
    setSidebarWidth(nextWidth)
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

function clampSidebarWidthIfNeeded(): void {
  if (typeof window !== 'undefined' && window.innerWidth <= 960) {
    return
  }

  setSidebarWidth(sidebarWidth.value)
}

function onWindowResize(): void {
  clampSidebarWidthIfNeeded()
}

function onDragEnd(): void {
  onGlobalDragEnd()
}

function toggleTerminal(): void {
  terminalExpanded.value = !terminalExpanded.value
}

function expandTerminal(): void {
  if (!terminalExpanded.value) {
    terminalExpanded.value = true
  }
}

function handlePrimaryNavigation(item: MenuItem): void {
  if (activeMenu.value === item.path) {
    toggleSidebarVisibility()
    return
  }

  setSidebarVisible(true)
  navigate(item.path)
}

function openSettings(): void {
  if (activeMenu.value === '/settings') {
    toggleSidebarVisibility()
    return
  }
  setSidebarVisible(true)
  navigate('/settings')
}

onMounted(() => {
  hub.initToolHub()
  clampSidebarWidthIfNeeded()
  window.addEventListener('resize', onWindowResize)
})

onBeforeUnmount(() => {
  onDragEnd()
  window.removeEventListener('resize', onWindowResize)
  hub.disposeToolHub()
})
</script>

<template>
  <div class="workbench-root" :class="{ 'is-dragging': isDragging }">
    <div ref="workbenchMainRef" class="workbench-main">
      <nav class="activity-bar" :aria-label="t('app.activityBar')">
        <div class="activity-bar-head"></div>
        <d-tooltip v-for="item in menuItems" :key="item.path" :content="menuTitle(item)" position="right">
          <button
            class="activity-item"
            :class="{ active: activeMenu === item.path }"
            type="button"
            @click="handlePrimaryNavigation(item)"
          >
            <i :class="item.iconId" style="font-size: 20px;"></i>
          </button>
        </d-tooltip>

        <div class="activity-spacer" />

        <d-tooltip :content="t('app.settings')" position="right">
          <button class="activity-item utility-item" type="button" :class="{ active: activeMenu === '/settings' }" @click="openSettings">
            <i class="icon-setting" style="font-size: 18px;"></i>
          </button>
        </d-tooltip>
      </nav>

      <aside v-if="showSidebar" class="side-bar" :aria-label="t('app.explorer')" :style="{ width: `${sidebarWidth}px` }">
        <header class="side-bar-head">{{ t('app.explorer') }}</header>

        <button
          v-for="item in menuItems"
          :key="`side-${item.path}`"
          class="side-item"
          :class="{ active: activeMenu === item.path }"
          type="button"
          @click="navigate(item.path)"
        >
          <span class="side-item-title">{{ menuTitle(item) }}</span>
          <span class="side-item-path">{{ item.path }}</span>
        </button>
        
        <div class="activity-spacer" />
        
        <button
          class="side-item side-utility-item"
          :class="{ active: activeMenu === '/settings' }"
          type="button"
          @click="navigate('/settings')"
        >
          <span class="side-item-title">{{ menuTitle(settingsMenuItem) }}</span>
          <span class="side-item-path">{{ settingsMenuItem.path }}</span>
        </button>
      </aside>

      <div
        v-if="showSidebar"
        class="side-sash"
        :class="{ active: isSidebarDragging }"
        @mousedown="onSidebarDragStart"
      />

      <section ref="editorStackRef" class="editor-stack">
        <header class="editor-tabs">
          <div class="editor-tab is-active">
            <i :class="activeMenuItem.iconId" style="font-size: 14px;"></i>
            <span>{{ menuTitle(activeMenuItem) }}</span>
          </div>
        </header>

        <main class="editor-content">
          <RouterView />
        </main>

        <section class="panel-dock" :style="{ height: panelHeight }">
          <div class="drag-handle" @mousedown="onTerminalDragStart">
            <div class="drag-handle-line" />
          </div>

          <header class="panel-header">
            <div class="panel-header-left">
              <button class="panel-tab is-active" type="button" @click="expandTerminal">{{ t('terminal.panel') }}</button>
              <span class="panel-caption">{{ terminalSessionCaption }}</span>
            </div>

            <button class="panel-toggle" type="button" @click="toggleTerminal">
              {{ terminalExpanded ? t('app.hide') : t('app.show') }}
            </button>
          </header>

          <div v-show="terminalExpanded" class="panel-body">
            <TerminalPanel
              :terminals="hub.terminals.value"
              :active-terminal-id="hub.activeTerminalId.value"
              :outputs-by-terminal="hub.terminalOutputsById"
              @select-terminal="hub.selectTerminal"
              @create-terminal="hub.createTerminal"
              @stop-terminal="hub.stopTerminal"
              @stop-all-terminals="hub.stopAllTerminals"
              @input="hub.sendTerminalInput"
              @resize-terminal="hub.resizeTerminal"
              @clear-output="hub.clearTerminalOutput"
            />
          </div>
        </section>
      </section>
    </div>

    <footer class="status-bar">
      <div class="status-left">
        <span class="status-item">{{ t('app.brand') }}</span>
        <span class="status-item">{{ menuTitle(activeMenuItem) }}</span>
      </div>
      <div class="status-right">
        <span class="status-item">{{ activeTerminalStatus }}</span>
      </div>
    </footer>

    <ToolRunner
      v-model:visible="runnerVisible"
      v-model:python-override="pythonOverride"
      :tool="activeTool"
      :default-python-path="defaultPythonPath"
      @pick-python="hub.pickPythonInterpreter"
      @run="hub.handleRun"
    />
  </div>
</template>

<style scoped>
.workbench-root {
  height: 100vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: var(--vscode-workbench-bg);
  color: var(--vscode-text-primary);
}

.workbench-root.is-dragging :deep(.xterm),
.workbench-root.is-dragging :deep(iframe) {
  pointer-events: none;
}

.workbench-main {
  flex: 1;
  min-height: 0;
  display: flex;
  overflow: hidden;
}

.activity-bar {
  width: 48px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  background: var(--vscode-activitybar-bg);
  border-right: 1px solid var(--vscode-border-color);
}

.activity-bar-head {
  height: 35px; /* match side-bar-head height */
  width: 100%;
}

.activity-item {
  width: 100%;
  height: 42px;
  border: 0;
  background: transparent;
  color: var(--vscode-text-muted);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  position: relative;
}

.activity-item:hover {
  color: var(--vscode-text-primary);
}

.activity-item.active {
  color: var(--vscode-text-primary);
}

.activity-item.active::before {
  content: '';
  position: absolute;
  left: 0;
  top: 7px;
  bottom: 7px;
  width: 2px;
  background: var(--vscode-accent-color);
}

.activity-spacer {
  flex: 1;
}

.utility-item {
  margin-bottom: 8px;
}

.side-bar {
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  background: var(--vscode-sidebar-bg);
  border-right: 1px solid var(--vscode-border-color);
}

.side-sash {
  width: 4px;
  flex-shrink: 0;
  cursor: ew-resize;
  position: relative;
  background: transparent;
}

.side-sash::before {
  content: '';
  position: absolute;
  top: 0;
  bottom: 0;
  left: 1px;
  width: 1px;
  background: transparent;
  transition: background 0.12s ease;
}

.side-sash:hover::before,
.side-sash.active::before {
  background: var(--vscode-accent-color);
}

.side-bar-head {
  height: 35px;
  display: flex;
  align-items: center;
  padding: 0 12px;
  font-size: 11px;
  letter-spacing: 0.8px;
  color: var(--vscode-text-muted);
  border-bottom: 1px solid var(--vscode-border-color);
}

.side-item {
  border: 0;
  background: transparent;
  color: inherit;
  text-align: left;
  padding: 10px 12px;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 4px;
  border-left: 2px solid transparent;
}

.side-item:hover {
  background: var(--vscode-hover-bg);
}

.side-item.active {
  background: var(--vscode-active-bg);
  border-left-color: var(--vscode-accent-color);
}

.side-item-title {
  font-size: 12px;
  color: var(--vscode-text-primary);
}

.side-item-path {
  font-size: 11px;
  color: var(--vscode-text-muted);
  font-family: var(--vscode-font-mono);
}

.side-utility-item {
  margin-bottom: 8px;
}

.editor-stack {
  flex: 1;
  min-width: 0;
  min-height: 0;
  display: flex;
  flex-direction: column;
  background: var(--vscode-editor-bg);
}

.editor-tabs {
  height: 35px;
  flex-shrink: 0;
  display: flex;
  align-items: stretch;
  border-bottom: 1px solid var(--vscode-border-color);
  background: var(--vscode-tabs-bg);
}

.editor-tab {
  height: 100%;
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 0 14px;
  border-right: 1px solid var(--vscode-border-color);
  font-size: 12px;
  color: var(--vscode-text-muted);
  background: var(--vscode-editor-bg);
  cursor: default;
}

.editor-tab.is-active {
  color: var(--vscode-text-primary);
  border-top: 1px solid var(--vscode-accent-color);
  background: var(--vscode-editor-bg);
}

.editor-content {
  flex: 1;
  min-height: 0;
  position: relative;
}

.panel-dock {
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  border-top: 1px solid var(--vscode-border-color);
  background: var(--vscode-panel-bg);
  position: relative;
}

.drag-handle {
  position: absolute;
  top: -3px;
  left: 0;
  right: 0;
  height: 6px;
  cursor: ns-resize;
  z-index: 10;
}

.drag-handle-line {
  position: absolute;
  top: 2px;
  left: 0;
  right: 0;
  height: 2px;
  background: transparent;
  transition: background 0.1s;
}

.drag-handle:hover .drag-handle-line,
.workbench-root.is-dragging .drag-handle-line {
  background: var(--vscode-accent-color);
}

.panel-header {
  height: 35px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 16px 0 0;
  border-bottom: 1px solid var(--vscode-border-color);
}

.panel-header-left {
  display: flex;
  align-items: center;
  height: 100%;
}

.panel-tab {
  border: 0;
  background: transparent;
  height: 100%;
  padding: 0 12px;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--vscode-text-muted);
  cursor: pointer;
  border-bottom: 1px solid transparent;
}

.panel-tab:hover {
  color: var(--vscode-text-primary);
}

.panel-tab.is-active {
  color: var(--vscode-text-primary);
  border-bottom-color: var(--vscode-accent-color);
}

.panel-caption {
  margin-left: 12px;
  font-size: 11px;
  color: var(--vscode-text-muted);
}

.panel-toggle {
  border: 0;
  background: transparent;
  padding: 4px;
  color: var(--vscode-text-muted);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}

.panel-toggle:hover {
  color: var(--vscode-text-primary);
}

.panel-body {
  flex: 1;
  min-height: 0;
  position: relative;
}

.status-bar {
  height: 22px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 8px;
  background: var(--vscode-statusbar-bg);
  color: var(--vscode-statusbar-color);
  font-size: 11px;
}

.status-left,
.status-right {
  display: flex;
  align-items: center;
  height: 100%;
}

.status-item {
  display: flex;
  align-items: center;
  height: 100%;
  padding: 0 8px;
  cursor: default;
}

.status-item:hover {
  background: rgba(255, 255, 255, 0.1);
}
</style>
