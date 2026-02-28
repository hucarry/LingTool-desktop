<script setup lang="ts">
import PythonPackagePanel from '../components/PythonPackagePanel.vue'
import { useToolHub } from '../composables/useToolHub'

const hub = useToolHub()
const packagePythonPath = hub.packagePythonPath
const pythonPackages = hub.pythonPackages
const loadingPythonPackages = hub.loadingPythonPackages
const pythonOperationBusy = hub.pythonOperationBusy
const pythonOperationPackage = hub.pythonOperationPackage
const pythonOperationAction = hub.pythonOperationAction
const pythonPackageStatus = hub.pythonPackageStatus
</script>

<template>
  <section class="workspace-view">
    <PythonPackagePanel
      :python-path="packagePythonPath"
      :packages="pythonPackages"
      :loading="loadingPythonPackages"
      :processing="pythonOperationBusy"
      :processing-package="pythonOperationPackage"
      :processing-action="pythonOperationAction"
      :status-text="pythonPackageStatus"
      @browse-python="hub.pickPythonForPackages"
      @use-system-python="hub.useSystemPythonForPackages"
      @refresh-packages="hub.refreshPythonPackages"
      @install-package="hub.installPythonPackage"
      @uninstall-package="hub.uninstallPythonPackage"
    />
  </section>
</template>

<style scoped>
.workspace-view {
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;
  border: 1px solid var(--vscode-border-color);
  background: var(--vscode-editor-bg);
  overflow: hidden;
}

@media (max-width: 1100px) {
  .workspace-view {
    overflow: auto;
  }
}
</style>
