<script setup lang="ts">
import UiBadge from './ui/UiBadge.vue'
import UiButton from './ui/UiButton.vue'
import { useNotifyCenter } from '../composables/useNotify'

const { notifications, removeNotification } = useNotifyCenter()

function toneClass(type: 'success' | 'warning' | 'error' | 'info'): string {
  return {
    success: 'border-l-success',
    warning: 'border-l-warning',
    error: 'border-l-danger',
    info: 'border-l-accent',
  }[type]
}
</script>

<template>
  <teleport to="body">
    <transition-group
      name="notify"
      tag="div"
      class="pointer-events-none fixed top-4 right-4 z-[4600] flex w-[min(360px,calc(100vw-1.5rem))] flex-col gap-2.5 max-sm:left-3 max-sm:right-3 max-sm:w-auto"
    >
      <article
        v-for="item in notifications"
        :key="item.id"
        :class="['pointer-events-auto grid grid-cols-[auto_1fr_auto] items-start gap-3 rounded-panel border border-border border-l-[3px] bg-elevated p-3 shadow-flyout', toneClass(item.type)]"
      >
        <UiBadge class="h-[22px] w-[22px] justify-center rounded-full bg-editor px-0 text-foreground">
          {{ item.type.slice(0, 1).toUpperCase() }}
        </UiBadge>
        <div class="min-w-0">
          <p class="break-words text-sm leading-6 text-foreground">{{ item.content }}</p>
          <UiBadge v-if="item.count > 1" class="mt-2">x{{ item.count }}</UiBadge>
        </div>
        <UiButton variant="ghost" size="sm" @click="removeNotification(item.id)">x</UiButton>
      </article>
    </transition-group>
  </teleport>
</template>

<style scoped>
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
</style>
