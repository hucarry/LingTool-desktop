<script setup lang="ts">
defineProps<{
  glyph: string
  title: string
  themeLabel: string
  defaultPythonLabel: string
  defaultPythonTitle: string
  pythonTitle: string
}>()
</script>

<template>
  <header class="flex h-[35px] shrink-0 items-stretch justify-between border-b border-border bg-tabs">
    <div class="flex min-w-0 h-full items-center gap-1.5 border-r border-border bg-editor px-3.5 text-xs text-muted">
      <span class="inline-flex min-w-[18px] items-center justify-center text-[10px] font-bold tracking-[0.08em]">
        {{ glyph }}
      </span>
      <span class="truncate text-foreground">{{ title }}</span>
    </div>

    <div class="hidden items-center gap-2 px-3 md:flex">
      <span class="ui-badge">{{ themeLabel }}</span>
      <span class="ui-badge" :title="defaultPythonTitle">
        {{ pythonTitle }}: {{ defaultPythonLabel }}
      </span>
    </div>
  </header>
</template>
