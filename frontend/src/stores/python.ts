import { defineStore } from 'pinia'
import { storeToRefs } from 'pinia'
import { ref, watch } from 'vue'

import { bridge } from '../services/bridge'
import { useI18n } from '../composables/useI18n'
import { useNotificationsStore } from './notifications'
import { useSettingsStore } from './settings'
import type {
  PythonPackageInstallStatusMessage,
  PythonPackagesMessage,
} from '../types'

export const usePythonStore = defineStore('python', () => {
  const settingsStore = useSettingsStore()
  const { t, locale } = useI18n()
  const notifications = useNotificationsStore()
  const { defaultPythonPath } = storeToRefs(settingsStore)

  const packagePythonPath = ref(defaultPythonPath.value)
  const pythonPackages = ref<{ name: string; version: string }[]>([])
  const loadingPythonPackages = ref(false)
  const pythonOperationBusy = ref(false)
  const pythonOperationAction = ref<'install' | 'uninstall' | ''>('')
  const pythonOperationPackage = ref('')
  const pythonPackageStatus = ref(t('python.ready'))

  watch(locale, () => {
    if (!pythonOperationBusy.value) {
      pythonPackageStatus.value = t('python.ready')
    }
  })

  watch(defaultPythonPath, (nextPath, previousPath) => {
    if (packagePythonPath.value === (previousPath ?? '')) {
      packagePythonPath.value = nextPath
    }
  })

  function setPackagePythonPath(path: string): void {
    packagePythonPath.value = path.trim()
  }

  function useSystemPythonPath(): void {
    packagePythonPath.value = ''
  }

  function pickPythonForPackages(): void {
    bridge.send({
      type: 'browsePython',
      defaultPath: packagePythonPath.value || defaultPythonPath.value || undefined,
      purpose: 'packageManager',
    })
  }

  function beginLoadingPackages(): void {
    loadingPythonPackages.value = true
  }

  function beginOperation(action: 'install' | 'uninstall', packageName: string): void {
    pythonOperationBusy.value = true
    pythonOperationAction.value = action
    pythonOperationPackage.value = packageName
    pythonPackageStatus.value = action === 'install'
      ? t('python.installing', { packageName })
      : t('python.uninstalling', { packageName })
  }

  function resetBusyState(): void {
    loadingPythonPackages.value = false
    pythonOperationBusy.value = false
    pythonOperationAction.value = ''
    pythonOperationPackage.value = ''
  }

  function packageActionText(action: 'install' | 'uninstall'): string {
    return action === 'uninstall'
      ? t('python.action.uninstall')
      : t('python.action.install')
  }

  function refreshPythonPackages(): void {
    beginLoadingPackages()
    bridge.send({
      type: 'getPythonPackages',
      pythonPath: packagePythonPath.value || undefined,
    })
  }

  function installPythonPackage(packageName: string): void {
    const normalized = packageName.trim()
    if (!normalized) {
      return
    }

    beginOperation('install', normalized)
    bridge.send({
      type: 'installPythonPackage',
      pythonPath: packagePythonPath.value || undefined,
      packageName: normalized,
    })
  }

  function uninstallPythonPackage(packageName: string): void {
    const normalized = packageName.trim()
    if (!normalized) {
      return
    }

    beginOperation('uninstall', normalized)
    bridge.send({
      type: 'uninstallPythonPackage',
      pythonPath: packagePythonPath.value || undefined,
      packageName: normalized,
    })
  }

  function handlePythonPackagesMessage(message: PythonPackagesMessage): void {
    pythonPackages.value = message.packages
    packagePythonPath.value = message.pythonPath === 'python' ? '' : message.pythonPath
    loadingPythonPackages.value = false
  }

  function handlePythonPackageInstallStatusMessage(message: PythonPackageInstallStatusMessage): void {
    const actionText = packageActionText(message.action)

    switch (message.status) {
      case 'running':
        pythonOperationBusy.value = true
        pythonOperationAction.value = message.action
        pythonOperationPackage.value = message.packageName
        pythonPackageStatus.value = t('python.status.running', {
          action: actionText,
          packageName: message.packageName,
        })
        break
      case 'succeeded':
        resetBusyState()
        pythonPackageStatus.value = t('python.status.succeeded', {
          action: actionText,
          packageName: message.packageName,
        })
        notifications.success(
          t('python.status.succeeded', {
            action: actionText,
            packageName: message.packageName,
          }),
          {
            groupKey: `python.${message.action}.succeeded`,
          },
        )
        break
      case 'failed':
        resetBusyState()
        pythonPackageStatus.value = t('python.status.failed', {
          action: actionText,
          packageName: message.packageName,
          details: message.message ? ` (${message.message})` : '',
        })
        notifications.error(
          t('python.status.failed', {
            action: actionText,
            packageName: message.packageName,
            details: '',
          }),
          {
            groupKey: `python.${message.action}.failed`,
          },
        )
        break
      default:
        break
    }
  }

  return {
    packagePythonPath,
    pythonPackages,
    loadingPythonPackages,
    pythonOperationBusy,
    pythonOperationAction,
    pythonOperationPackage,
    pythonPackageStatus,
    setPackagePythonPath,
    useSystemPythonPath,
    pickPythonForPackages,
    beginLoadingPackages,
    beginOperation,
    resetBusyState,
    refreshPythonPackages,
    installPythonPackage,
    uninstallPythonPackage,
    handlePythonPackagesMessage,
    handlePythonPackageInstallStatusMessage,
  }
})
