<script setup lang="ts">
import { onMounted } from 'vue'
import { storeToRefs } from 'pinia'

import PythonPackagePanel from '../components/PythonPackagePanel.vue'
import { usePythonStore } from '../stores/python'

const pythonStore = usePythonStore()
const {
  packagePythonPath,
  pythonPackages,
  loadingPythonPackages,
  pythonOperationBusy,
  pythonOperationPackage,
  pythonOperationAction,
  pythonPackageStatus,
} = storeToRefs(pythonStore)

onMounted(() => {
  if (!loadingPythonPackages.value && pythonPackages.value.length === 0) {
    pythonStore.refreshPythonPackages()
  }
})
</script>

<template>
  <section class="flex h-full min-h-0 flex-col overflow-hidden p-3">
    <PythonPackagePanel
      :python-path="packagePythonPath"
      :packages="pythonPackages"
      :loading="loadingPythonPackages"
      :processing="pythonOperationBusy"
      :processing-package="pythonOperationPackage"
      :processing-action="pythonOperationAction"
      :status-text="pythonPackageStatus"
      @browse-python="pythonStore.pickPythonForPackages"
      @use-system-python="
        () => {
          pythonStore.useSystemPythonPath()
          pythonStore.refreshPythonPackages()
        }
      "
      @refresh-packages="pythonStore.refreshPythonPackages"
      @install-package="pythonStore.installPythonPackage"
      @uninstall-package="pythonStore.uninstallPythonPackage"
    />
  </section>
</template>
