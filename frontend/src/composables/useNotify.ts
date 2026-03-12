import { readonly, ref } from 'vue'

export type NotifyType = 'success' | 'warning' | 'error' | 'info'

export interface NotifyItem {
  id: number
  type: NotifyType
  content: string
}

const DEFAULT_DURATION: Record<NotifyType, number> = {
  success: 3000,
  warning: 4000,
  error: 5000,
  info: 3000,
}

const MAX_NOTIFICATIONS = 4

const notifications = ref<NotifyItem[]>([])
const timers = new Map<number, ReturnType<typeof setTimeout>>()

let notificationSeed = 0

function clearTimer(id: number): void {
  const timer = timers.get(id)
  if (!timer) {
    return
  }

  clearTimeout(timer)
  timers.delete(id)
}

function removeNotification(id: number): void {
  clearTimer(id)
  notifications.value = notifications.value.filter((item) => item.id !== id)
}

function trimNotifications(): void {
  while (notifications.value.length > MAX_NOTIFICATIONS) {
    const oldest = notifications.value[0]
    if (!oldest) {
      break
    }

    removeNotification(oldest.id)
  }
}

function openNotification(type: NotifyType, content: string, duration = DEFAULT_DURATION[type]): void {
  const normalized = content.trim()
  if (!normalized) {
    return
  }

  const id = ++notificationSeed
  notifications.value = [
    ...notifications.value,
    {
      id,
      type,
      content: normalized,
    },
  ]

  trimNotifications()

  if (duration > 0 && typeof window !== 'undefined') {
    const timer = window.setTimeout(() => {
      removeNotification(id)
    }, duration)

    timers.set(id, timer)
  }
}

export function useNotify() {
  function success(msg: string) {
    openNotification('success', msg)
  }

  function warning(msg: string) {
    openNotification('warning', msg)
  }

  function error(msg: string) {
    openNotification('error', msg)
  }

  function info(msg: string) {
    openNotification('info', msg)
  }

  return { success, warning, error, info }
}

export function useNotifyCenter() {
  return {
    notifications: readonly(notifications),
    removeNotification,
  }
}
