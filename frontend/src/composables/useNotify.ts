import { NotificationService } from 'vue-devui'

/**
 * 封装 DevUI NotificationService，提供简洁的通知方法
 */
export function useNotify() {
  function success(msg: string) {
    NotificationService.open({
      type: 'success',
      title: '',
      content: msg,
      duration: 3000,
    })
  }

  function warning(msg: string) {
    NotificationService.open({
      type: 'warning',
      title: '',
      content: msg,
      duration: 4000,
    })
  }

  function error(msg: string) {
    NotificationService.open({
      type: 'error',
      title: '',
      content: msg,
      duration: 5000,
    })
  }

  function info(msg: string) {
    NotificationService.open({
      type: 'info',
      title: '',
      content: msg,
      duration: 3000,
    })
  }

  return { success, warning, error, info }
}
