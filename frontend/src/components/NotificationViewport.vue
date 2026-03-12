<script setup lang="ts">
import { useNotifyCenter } from '../composables/useNotify'

const { notifications, removeNotification } = useNotifyCenter()

function closeNotification(id: number): void {
  removeNotification(id)
}
</script>

<template>
  <teleport to="body">
    <transition-group name="notify" tag="div" class="notify-stack">
      <article
        v-for="item in notifications"
        :key="item.id"
        class="notify-card"
        :class="`is-${item.type}`"
      >
        <span class="notify-mark">{{ item.type.slice(0, 1).toUpperCase() }}</span>
        <div class="notify-body">
          <p class="notify-text">{{ item.content }}</p>
        </div>
        <button class="notify-close" type="button" @click="closeNotification(item.id)">x</button>
      </article>
    </transition-group>
  </teleport>
</template>

<style scoped>
.notify-stack {
  position: fixed;
  top: 16px;
  right: 16px;
  z-index: 4600;
  width: min(360px, calc(100vw - 24px));
  display: flex;
  flex-direction: column;
  gap: 10px;
  pointer-events: none;
}

.notify-card {
  pointer-events: auto;
  display: grid;
  grid-template-columns: auto 1fr auto;
  gap: 10px;
  align-items: start;
  padding: 12px;
  border: 1px solid var(--vscode-border-color);
  border-left-width: 3px;
  border-radius: 8px;
  background: color-mix(in srgb, var(--vscode-sidebar-bg) 92%, var(--vscode-editor-bg));
  box-shadow: 0 12px 28px rgba(0, 0, 0, 0.24);
}

.notify-card.is-success {
  border-left-color: var(--el-color-success);
}

.notify-card.is-warning {
  border-left-color: var(--el-color-warning);
}

.notify-card.is-error {
  border-left-color: var(--el-color-danger);
}

.notify-card.is-info {
  border-left-color: var(--vscode-accent-color);
}

.notify-mark {
  width: 22px;
  height: 22px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 999px;
  background: var(--vscode-editor-bg);
  color: var(--vscode-text-primary);
  font-size: 11px;
  font-weight: 700;
}

.notify-body {
  min-width: 0;
}

.notify-text {
  color: var(--vscode-text-primary);
  font-size: 13px;
  line-height: 1.45;
  word-break: break-word;
}

.notify-close {
  width: 24px;
  height: 24px;
  border: 0;
  border-radius: 4px;
  background: transparent;
  color: var(--vscode-text-muted);
  cursor: pointer;
}

.notify-close:hover {
  background: var(--vscode-hover-bg);
  color: var(--vscode-text-primary);
}

.notify-enter-active,
.notify-leave-active {
  transition: opacity 0.18s ease, transform 0.18s ease;
}

.notify-enter-from,
.notify-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}

.notify-move {
  transition: transform 0.18s ease;
}

@media (max-width: 720px) {
  .notify-stack {
    top: 12px;
    right: 12px;
    left: 12px;
    width: auto;
  }
}
</style>
