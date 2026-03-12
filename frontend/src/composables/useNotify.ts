import { readonly, ref } from 'vue'

export type NotifyType = 'success' | 'warning' | 'error' | 'info'
type NotifyMergeMode = 'count' | 'replace'

export interface NotifyItem {
  id: number
  type: NotifyType
  content: string
  count: number
  groupKey: string
  updatedAt: number
}

export interface NotifyOptions {
  duration?: number
  groupKey?: string
  mergeMode?: NotifyMergeMode
  mergeWindowMs?: number
}

const DEFAULT_DURATION: Record<NotifyType, number> = {
  success: 3000,
  warning: 4000,
  error: 5000,
  info: 3000,
}

const MAX_NOTIFICATIONS = 4
const DEFAULT_MERGE_WINDOW_MS = 2600

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

function scheduleRemoval(id: number, duration: number): void {
  clearTimer(id)

  if (duration <= 0 || typeof window === 'undefined') {
    return
  }

  const timer = window.setTimeout(() => {
    removeNotification(id)
  }, duration)

  timers.set(id, timer)
}

function createDefaultGroupKey(type: NotifyType, content: string): string {
  return `${type}:${content}`
}

function findGroupedNotification(
  type: NotifyType,
  groupKey: string,
  mergeWindowMs: number,
  now: number,
): NotifyItem | undefined {
  return notifications.value.find((item) => {
    return item.type === type
      && item.groupKey === groupKey
      && now - item.updatedAt <= mergeWindowMs
  })
}

function openNotification(type: NotifyType, content: string, options: NotifyOptions = {}): void {
  const normalized = content.trim()
  if (!normalized) {
    return
  }

  const duration = options.duration ?? DEFAULT_DURATION[type]
  const mergeWindowMs = options.mergeWindowMs ?? DEFAULT_MERGE_WINDOW_MS
  const mergeMode = options.mergeMode ?? 'count'
  const groupKey = options.groupKey?.trim() || createDefaultGroupKey(type, normalized)
  const now = Date.now()
  const groupedItem = findGroupedNotification(type, groupKey, mergeWindowMs, now)

  if (groupedItem) {
    groupedItem.content = normalized
    groupedItem.updatedAt = now
    groupedItem.count = mergeMode === 'count'
      ? groupedItem.count + 1
      : 1

    notifications.value = [...notifications.value]
    scheduleRemoval(groupedItem.id, duration)
    return
  }

  const id = ++notificationSeed
  notifications.value = [
    ...notifications.value,
    {
      id,
      type,
      content: normalized,
      count: 1,
      groupKey,
      updatedAt: now,
    },
  ]

  trimNotifications()
  scheduleRemoval(id, duration)
}

export function useNotify() {
  function success(msg: string, options?: NotifyOptions) {
    openNotification('success', msg, options)
  }

  function warning(msg: string, options?: NotifyOptions) {
    openNotification('warning', msg, options)
  }

  function error(msg: string, options?: NotifyOptions) {
    openNotification('error', msg, options)
  }

  function info(msg: string, options?: NotifyOptions) {
    openNotification('info', msg, options)
  }

  return { success, warning, error, info }
}

export function useNotifyCenter() {
  return {
    notifications: readonly(notifications),
    removeNotification,
  }
}
