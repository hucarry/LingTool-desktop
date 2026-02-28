<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Box, Cpu, SetUp, Setting } from '@element-plus/icons-vue'
import ToolRunner from './components/ToolRunner.vue'
import TerminalPanel from './components/TerminalPanel.vue'
import { useToolHub } from './composables/useToolHub'

interface MenuItem {
  path: '/python' | '/tools' | '/siliconflow'
  icon: typeof Box
  title: string
}

const hub = useToolHub()
const route = useRoute()
const router = useRouter()

const runnerVisible = hub.runnerVisible
const pythonOverride = hub.pythonOverride
const activeTool = hub.activeTool

const MIN_TERMINAL_HEIGHT = 140
const DEFAULT_TERMINAL_HEIGHT = 260
const COLLAPSED_TERMINAL_HEIGHT = 34

const terminalHeight = ref(DEFAULT_TERMINAL_HEIGHT)
const terminalExpanded = ref(true)
const isDragging = ref(false)
const editorStackRef = ref<HTMLElement>()

const menuItems: MenuItem[] = [
  { path: '/python', icon: Box, title: 'Python Packages' },
  { path: '/tools', icon: Cpu, title: 'Tools' },
  { path: '/siliconflow', icon: SetUp, title: 'Model Scanner' },
]
const fallbackMenuItem: MenuItem = menuItems[0]!

const activeMenu = computed<MenuItem['path']>(() => {
  if (route.path.startsWith('/tools')) {
    return '/tools'
  }

  if (route.path.startsWith('/siliconflow')) {
    return '/siliconflow'
  }

  return '/python'
})

const activeMenuItem = computed(() => {
  return menuItems.find((item) => item.path === activeMenu.value) ?? fallbackMenuItem
})

const panelHeight = computed(() => {
  return terminalExpanded.value ? `${terminalHeight.value}px` : `${COLLAPSED_TERMINAL_HEIGHT}px`
})

const activeTerminalStatus = computed(() => {
  const terminal = hub.terminals.value.find((item) => item.terminalId === hub.activeTerminalId.value)
  if (!terminal) {
    return 'No Active Terminal'
  }

  const shellName = terminal.shell.split(/[\\/]/).pop() || terminal.shell
  return `${shellName} (${terminal.status})`
})

function navigate(path: MenuItem['path']): void {
  if (activeMenu.value === path) {
    return
  }

  router.push(path)
}

function onDragStart(event: MouseEvent): void {
  event.preventDefault()
  isDragging.value = true

  document.addEventListener('mousemove', onDragMove)
  document.addEventListener('mouseup', onDragEnd)
  document.body.style.cursor = 'ns-resize'
  document.body.style.userSelect = 'none'
}

function onDragMove(event: MouseEvent): void {
  if (!isDragging.value || !editorStackRef.value) {
    return
  }

  const rect = editorStackRef.value.getBoundingClientRect()
  const maxHeight = Math.max(MIN_TERMINAL_HEIGHT, rect.height - 160)
  const nextHeight = Math.round(rect.bottom - event.clientY)

  terminalHeight.value = Math.max(MIN_TERMINAL_HEIGHT, Math.min(maxHeight, nextHeight))

  if (!terminalExpanded.value) {
    terminalExpanded.value = true
  }
}

function onDragEnd(): void {
  if (!isDragging.value) {
    return
  }

  isDragging.value = false
  document.removeEventListener('mousemove', onDragMove)
  document.removeEventListener('mouseup', onDragEnd)
  document.body.style.cursor = ''
  document.body.style.userSelect = ''
}

function toggleTerminal(): void {
  terminalExpanded.value = !terminalExpanded.value
}

function expandTerminal(): void {
  if (!terminalExpanded.value) {
    terminalExpanded.value = true
  }
}

onMounted(() => {
  hub.initToolHub()
})

onBeforeUnmount(() => {
  onDragEnd()
  hub.disposeToolHub()
})
</script>

<template>
  <div class="workbench-root" :class="{ 'is-dragging': isDragging }">
    <header class="title-bar">
      <div class="title-left">
        <span class="app-badge">TH</span>
        <span class="app-name">ToolHub</span>
      </div>
      <div class="title-center">{{ activeMenuItem.title }} - ToolHub Workspace</div>
      <div class="title-right">VS Code Style Layout</div>
    </header>

    <div class="workbench-main">
      <nav class="activity-bar" aria-label="Activity Bar">
        <button
          v-for="item in menuItems"
          :key="item.path"
          class="activity-item"
          :class="{ active: activeMenu === item.path }"
          type="button"
          :title="item.title"
          @click="navigate(item.path)"
        >
          <el-icon :size="20"><component :is="item.icon" /></el-icon>
        </button>

        <div class="activity-spacer" />

        <button class="activity-item utility-item" type="button" title="Settings">
          <el-icon :size="18"><Setting /></el-icon>
        </button>
      </nav>

      <aside class="side-bar" aria-label="Explorer">
        <header class="side-bar-head">EXPLORER</header>

        <button
          v-for="item in menuItems"
          :key="`side-${item.path}`"
          class="side-item"
          :class="{ active: activeMenu === item.path }"
          type="button"
          @click="navigate(item.path)"
        >
          <span class="side-item-title">{{ item.title }}</span>
          <span class="side-item-path">{{ item.path }}</span>
        </button>
      </aside>

      <section ref="editorStackRef" class="editor-stack">
        <header class="editor-tabs">
          <div class="editor-tab is-active">
            <el-icon :size="14"><component :is="activeMenuItem.icon" /></el-icon>
            <span>{{ activeMenuItem.title }}</span>
          </div>
        </header>

        <main class="editor-content">
          <RouterView />
        </main>

        <section class="panel-dock" :style="{ height: panelHeight }">
          <div class="drag-handle" @mousedown="onDragStart">
            <div class="drag-handle-line" />
          </div>

          <header class="panel-header">
            <div class="panel-header-left">
              <button class="panel-tab is-active" type="button" @click="expandTerminal">TERMINAL</button>
              <span class="panel-caption">
                {{ hub.terminals.value.length }} session{{ hub.terminals.value.length === 1 ? '' : 's' }}
              </span>
            </div>

            <button class="panel-toggle" type="button" @click="toggleTerminal">
              {{ terminalExpanded ? 'Hide' : 'Show' }}
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
        <span class="status-item">ToolHub</span>
        <span class="status-item">{{ activeMenuItem.title }}</span>
      </div>
      <div class="status-right">
        <span class="status-item">{{ activeTerminalStatus }}</span>
      </div>
    </footer>

    <ToolRunner
      v-model:visible="runnerVisible"
      v-model:python-override="pythonOverride"
      :tool="activeTool"
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

.title-bar {
  height: 30px;
  flex-shrink: 0;
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  align-items: center;
  padding: 0 12px;
  border-bottom: 1px solid var(--vscode-border-color);
  background: var(--vscode-titlebar-bg);
  color: var(--vscode-text-muted);
  font-size: 12px;
}

.title-left,
.title-right {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.title-right {
  justify-content: flex-end;
}

.title-center {
  color: var(--vscode-text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.app-badge {
  width: 16px;
  height: 16px;
  border-radius: 3px;
  background: var(--vscode-accent-color);
  color: #ffffff;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  font-weight: 700;
}

.app-name {
  color: var(--vscode-text-primary);
  font-weight: 600;
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
  padding-top: 10px;
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
  width: 248px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  background: var(--vscode-sidebar-bg);
  border-right: 1px solid var(--vscode-border-color);
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
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 0 16px;
  font-size: 12px;
  color: var(--vscode-text-muted);
  border-right: 1px solid var(--vscode-border-color);
}

.editor-tab.is-active {
  color: var(--vscode-text-primary);
  background: var(--vscode-editor-bg);
}

.editor-content {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 10px 14px;
}

.panel-dock {
  flex-shrink: 0;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border-top: 1px solid var(--vscode-border-color);
  background: var(--vscode-panel-bg);
  transition: height 0.18s ease;
}

.drag-handle {
  height: 4px;
  cursor: ns-resize;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: transparent;
}

.drag-handle-line {
  width: 44px;
  height: 2px;
  border-radius: 999px;
  background: transparent;
}

.drag-handle:hover .drag-handle-line {
  background: var(--vscode-accent-color);
}

.panel-header {
  height: 32px;
  flex-shrink: 0;
  border-bottom: 1px solid var(--vscode-border-color);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 10px;
}

.panel-header-left {
  display: inline-flex;
  align-items: center;
  gap: 10px;
}

.panel-tab {
  border: 0;
  background: transparent;
  color: var(--vscode-text-muted);
  font-size: 11px;
  letter-spacing: 0.8px;
  padding: 0;
  height: 32px;
  border-bottom: 1px solid transparent;
  cursor: pointer;
}

.panel-tab.is-active {
  color: var(--vscode-text-primary);
  border-bottom-color: var(--vscode-accent-color);
}

.panel-caption {
  font-size: 11px;
  color: var(--vscode-text-muted);
}

.panel-toggle {
  border: 0;
  background: transparent;
  color: var(--vscode-text-muted);
  font-size: 11px;
  cursor: pointer;
}

.panel-toggle:hover {
  color: var(--vscode-text-primary);
}

.panel-body {
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.status-bar {
  height: 22px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 10px;
  background: var(--vscode-statusbar-bg);
  color: #ffffff;
  font-size: 11px;
}

.status-left,
.status-right {
  display: inline-flex;
  align-items: center;
  gap: 10px;
}

.status-item {
  opacity: 0.95;
  white-space: nowrap;
}

@media (max-width: 960px) {
  .side-bar {
    display: none;
  }

  .title-right {
    display: none;
  }

  .title-bar {
    grid-template-columns: auto 1fr;
    gap: 12px;
  }
}

@media (max-width: 640px) {
  .title-center {
    display: none;
  }

  .editor-content {
    padding: 6px;
  }
}
</style>
