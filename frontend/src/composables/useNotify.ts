import { storeToRefs } from 'pinia'

import { useNotificationsStore, type NotifyOptions } from '../stores/notifications'

export function useNotify() {
  const notificationsStore = useNotificationsStore()

  function success(msg: string, options?: NotifyOptions) {
    notificationsStore.success(msg, options)
  }

  function warning(msg: string, options?: NotifyOptions) {
    notificationsStore.warning(msg, options)
  }

  function error(msg: string, options?: NotifyOptions) {
    notificationsStore.error(msg, options)
  }

  function info(msg: string, options?: NotifyOptions) {
    notificationsStore.info(msg, options)
  }

  return { success, warning, error, info }
}

export function useNotifyCenter() {
  const notificationsStore = useNotificationsStore()
  const { notifications } = storeToRefs(notificationsStore)

  return {
    notifications,
    removeNotification: notificationsStore.removeNotification,
  }
}
