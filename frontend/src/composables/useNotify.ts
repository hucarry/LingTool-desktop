type NotifyType = 'success' | 'warning' | 'error' | 'info'

type NotificationModule = typeof import('vue-devui/notification')

let notificationModulePromise: Promise<NotificationModule> | null = null

function loadNotificationModule(): Promise<NotificationModule> {
  if (!notificationModulePromise) {
    notificationModulePromise = Promise.all([
      import('vue-devui/notification'),
      import('vue-devui/notification/style.css'),
    ]).then(([module]) => module)
  }

  return notificationModulePromise
}

function openNotification(type: NotifyType, content: string, duration: number): void {
  void loadNotificationModule()
    .then(({ NotificationService }) => {
      NotificationService.open({
        type,
        title: '',
        content,
        duration,
      })
    })
    .catch((error) => {
      console.error('[Notify] Failed to load notification service.', error)
    })
}

/**
 * Wrap DevUI NotificationService with simple helpers.
 */
export function useNotify() {
  function success(msg: string) {
    openNotification('success', msg, 3000)
  }

  function warning(msg: string) {
    openNotification('warning', msg, 4000)
  }

  function error(msg: string) {
    openNotification('error', msg, 5000)
  }

  function info(msg: string) {
    openNotification('info', msg, 3000)
  }

  return { success, warning, error, info }
}
