import { computed, type Ref } from 'vue'

import type { TerminalInfo } from '../../types'
import type { TerminalPanelProps } from './types'

interface UseTerminalPanelComputedOptions {
  props: TerminalPanelProps
  splitEnabled: Ref<boolean>
  secondaryTerminalId: Ref<string>
  focusedTerminalId: Ref<string>
  pendingSplitCreation: Ref<boolean>
  t: (key: string) => string
  formatSessionCount: (count: number) => string
}

export function useTerminalPanelComputed(options: UseTerminalPanelComputedOptions) {
  const {
    props,
    splitEnabled,
    secondaryTerminalId,
    focusedTerminalId,
    pendingSplitCreation,
    t,
    formatSessionCount,
  } = options

  function getTerminalBaseLabel(terminal: TerminalInfo): string {
    const title = terminal.title?.trim()
    if (title) {
      return title
    }

    return terminal.shell.split(/[\\/]/).pop() || terminal.shell || t('terminal.shellFallback')
  }

  const terminalLabelCounts = computed(() => {
    const counts = new Map<string, number>()
    props.terminals.forEach((terminal) => {
      const baseLabel = getTerminalBaseLabel(terminal)
      counts.set(baseLabel, (counts.get(baseLabel) ?? 0) + 1)
    })
    return counts
  })

  const terminalIdList = computed(() => props.terminals.map((terminal) => terminal.terminalId))

  const primaryTerminalId = computed(() => {
    if (props.activeTerminalId && terminalIdList.value.includes(props.activeTerminalId)) {
      return props.activeTerminalId
    }

    return terminalIdList.value[0] ?? ''
  })

  const commandTerminalId = computed(() => {
    if (focusedTerminalId.value && terminalIdList.value.includes(focusedTerminalId.value)) {
      return focusedTerminalId.value
    }

    return primaryTerminalId.value
  })

  const terminalTabs = computed(() => {
    return props.terminals.map((terminal, index) => {
      const baseLabel = getTerminalBaseLabel(terminal)
      const order = props.terminals.length - index
      return {
        ...terminal,
        label: (terminalLabelCounts.value.get(baseLabel) ?? 0) > 1 ? `${baseLabel} ${order}` : baseLabel,
        isCommandTarget: terminal.terminalId === commandTerminalId.value,
      }
    })
  })

  const terminalLabelMap = computed(() => {
    const map = new Map<string, string>()
    terminalTabs.value.forEach((terminal) => {
      map.set(terminal.terminalId, terminal.label)
    })
    return map
  })

  const terminalMap = computed(() => {
    const map = new Map<string, TerminalInfo>()
    props.terminals.forEach((terminal) => {
      map.set(terminal.terminalId, terminal)
    })
    return map
  })

  const resolvedSecondaryTerminalId = computed(() => {
    if (!splitEnabled.value || !secondaryTerminalId.value) {
      return ''
    }

    if (!terminalIdList.value.includes(secondaryTerminalId.value)) {
      return ''
    }

    if (secondaryTerminalId.value === primaryTerminalId.value) {
      return ''
    }

    return secondaryTerminalId.value
  })

  const splitActive = computed(() => splitEnabled.value && !!resolvedSecondaryTerminalId.value)
  const sessionSummary = computed(() => formatSessionCount(props.terminals.length))
  const hasTerminalSessions = computed(() => terminalIdList.value.length > 0)

  const primaryOutputs = computed(() => {
    if (!primaryTerminalId.value) {
      return []
    }

    return props.outputsByTerminal[primaryTerminalId.value] ?? []
  })

  const secondaryOutputs = computed(() => {
    if (!resolvedSecondaryTerminalId.value) {
      return []
    }

    return props.outputsByTerminal[resolvedSecondaryTerminalId.value] ?? []
  })

  const primaryTerminal = computed(() => {
    if (!primaryTerminalId.value) {
      return null
    }

    return terminalMap.value.get(primaryTerminalId.value) ?? null
  })

  const secondaryTerminal = computed(() => {
    if (!resolvedSecondaryTerminalId.value) {
      return null
    }

    return terminalMap.value.get(resolvedSecondaryTerminalId.value) ?? null
  })

  const splitCandidates = computed(() => {
    return terminalTabs.value.filter((terminal) => terminal.terminalId !== primaryTerminalId.value)
  })

  const splitStatusText = computed(() => {
    if (!splitEnabled.value) {
      return ''
    }

    if (pendingSplitCreation.value) {
      return t('terminal.creatingSplit')
    }

    if (resolvedSecondaryTerminalId.value) {
      return `${t('terminal.split')}: ${getLabel(resolvedSecondaryTerminalId.value)}`
    }

    return t('terminal.waitingSplit')
  })

  const commandTargetLabel = computed(() => {
    if (!commandTerminalId.value) {
      return t('terminal.noSessions')
    }

    return getLabel(commandTerminalId.value)
  })

  const commandTerminal = computed(() => {
    if (!commandTerminalId.value) {
      return null
    }

    return terminalMap.value.get(commandTerminalId.value) ?? null
  })

  function getLabel(terminalId: string): string {
    return terminalLabelMap.value.get(terminalId) ?? terminalId
  }

  function getTerminalStatusLabel(terminal: TerminalInfo | null): string {
    if (!terminal) {
      return ''
    }

    return t(`terminal.status.${terminal.status}`)
  }

  function getCompactPath(path: string): string {
    const normalized = path.replace(/\\/g, '/')
    const segments = normalized.split('/').filter(Boolean)
    if (segments.length <= 2) {
      return normalized
    }

    return segments.slice(-2).join('/')
  }

  function findAlternativeTerminalId(primaryId: string, preferredId = ''): string {
    if (preferredId && preferredId !== primaryId && terminalIdList.value.includes(preferredId)) {
      return preferredId
    }

    const fallback = props.terminals.find((terminal) => terminal.terminalId !== primaryId)
    return fallback?.terminalId ?? ''
  }

  return {
    commandTargetLabel,
    commandTerminal,
    commandTerminalId,
    findAlternativeTerminalId,
    getCompactPath,
    getLabel,
    getTerminalStatusLabel,
    hasTerminalSessions,
    primaryOutputs,
    primaryTerminal,
    primaryTerminalId,
    resolvedSecondaryTerminalId,
    secondaryOutputs,
    secondaryTerminal,
    sessionSummary,
    splitActive,
    splitCandidates,
    splitStatusText,
    terminalIdList,
    terminalTabs,
  }
}
