import { computed, ref } from 'vue'

export type Locale = 'zh-CN' | 'en-US'

type MessageParams = Record<string, string | number>

const LOCALE_STORAGE_KEY = 'toolhub.locale'

const messages: Record<Locale, Record<string, string>> = {
  'zh-CN': {
    'app.brand': 'ToolHub',
    'app.workspace': 'ToolHub 工作区',
    'app.layout': 'VS Code 风格布局',
    'app.activityBar': '活动栏',
    'app.settings': '设置',
    'app.explorer': '资源管理器',
    'app.noActiveTerminal': '无活动终端',
    'app.show': '显示',
    'app.hide': '隐藏',
    'app.defaultPythonUpdated': '已更新默认 Python 解释器',
    'app.lang.zh': '中文',
    'app.lang.en': 'EN',
    'app.menu.python': 'Python 包',
    'app.menu.tools': '工具',
    'app.menu.addTool': '新增工具',
    'app.sessions': '{count} 个会话',
    'app.session.one': '{count} 个会话',
    'app.session.other': '{count} 个会话',

    'terminal.panel': '终端',
    'terminal.noSessions': '暂无终端会话',
    'terminal.newProfile': '新建配置',
    'terminal.hideProfile': '隐藏配置',
    'terminal.split': '分屏',
    'terminal.unsplit': '取消分屏',
    'terminal.clear': '清空',
    'terminal.kill': '结束',
    'terminal.killAll': '全部结束',
    'terminal.killOthers': '结束其他',
    'terminal.killTitle': '结束终端',
    'terminal.shell': 'Shell',
    'terminal.workingDir': '工作目录',
    'terminal.create': '创建终端',
    'terminal.primary': '主终端',
    'terminal.closeSplit': '关闭分屏',
    'terminal.creatingSplit': '正在创建分屏终端...',
    'terminal.waitingSplit': '等待分屏终端启动...',
    'terminal.clearActive': '清空当前',
    'terminal.copyId': '复制终端 ID',
    'terminal.status.running': '运行中',
    'terminal.status.exited': '已退出',
    'terminal.status.stopped': '已停止',
    'terminal.status.failed': '失败',
    'terminal.shellFallback': '终端',
    'terminal.currentUnavailable': '当前终端不可用，请选择或新建终端。',

    'python.managerTitle': 'Python 包管理',
    'python.managerDesc': '查看、搜索、安装和卸载当前解释器的包。',
    'python.refresh': '刷新',
    'python.currentInterpreter': '当前 Python 解释器',
    'python.browse': '浏览...',
    'python.systemPython': '系统 Python',
    'python.installInput': '包名，例如 requests 或 pandas==2.2.3',
    'python.install': '安装',
    'python.ready': '就绪',
    'python.searchInstalled': '搜索已安装包',
    'python.noPackageData': '暂无包数据，请先点击刷新。',
    'python.name': '名称',
    'python.version': '版本',
    'python.action': '操作',
    'python.uninstallConfirm': '确认卸载 {name}？',
    'python.uninstall': '卸载',
    'python.cancel': '取消',
    'python.action.install': '安装',
    'python.action.uninstall': '卸载',
    'python.installing': '安装中：{packageName}',
    'python.uninstalling': '卸载中：{packageName}',
    'python.status.running': '{action}中：{packageName}',
    'python.status.succeeded': '{action}成功：{packageName}',
    'python.status.failed': '{action}失败：{packageName}{details}',

    'tools.catalog': '工具目录',
    'tools.items': '{filtered} / {total} 项',
    'tools.search': '按名称或标签搜索',
    'tools.ready': '就绪',
    'tools.invalidPath': '路径无效',
    'tools.run': '运行',
    'tools.noMatch': '没有匹配的工具',
    'tools.edit': '编辑',
    'tools.editSelected': '编辑所选',
    'tools.deleteSelected': '删除所选',
    'tools.selected': '已选',
    'tools.confirmDeleteTitle': '批量删除',
    'tools.confirmDeleteMessage': '确定删除所选工具吗？',
    'tools.save': '保存',
    'tools.cancel': '取消',
    'tools.browse': '浏览...',
    'tools.name': '名称',
    'tools.type': '类型',
    'tools.path': '路径',
    'tools.python': 'Python',
    'tools.cwd': '工作目录',
    'tools.argsTemplate': '参数模板',
    'tools.tags': '标签（逗号分隔）',
    'tools.description': '描述',
    'tools.noSelection': '请先选择工具',
    'tools.editDialog': '编辑工具',
    'tools.validationId': '工具 ID 不能为空',
    'tools.validationIdFormat': '工具 ID 格式不合法',
    'tools.validationName': '工具名称不能为空',
    'tools.validationPath': '工具路径不能为空',
    'tools.added': '工具已新增：{toolId}',
    'tools.updated': '工具已更新：{toolId}',
    'tools.deleted': '已删除 {count} 个工具',

    'runner.title': '工具详情',
    'runner.invalidConfig': '工具配置无效，禁止运行。',
    'runner.name': '名称',
    'runner.id': 'ID',
    'runner.type': '类型',
    'runner.path': '路径',
    'runner.cwd': '工作目录',
    'runner.python': 'Python',
    'runner.systemPython': '系统 python',
    'runner.argsTemplate': '参数模板',
    'runner.none': '(无)',
    'runner.description': '描述',
    'runner.pythonInterpreter': 'Python 解释器',
    'runner.pythonFallback': '回退顺序：自定义覆盖 -> tools.json python -> 系统 python',
    'runner.pythonFallbackWithAppDefault': '优先级：手动覆盖 -> 工具默认 -> 应用默认 -> 系统 Python',
    'runner.useToolDefault': '使用工具默认值',
    'runner.useAppDefault': '使用应用默认值',
    'runner.useSystemPython': '使用系统 Python',
    'runner.pythonExample': '示例：C:\\project\\.venv\\Scripts\\python.exe',
    'runner.argument': '参数 {field}',
    'runner.enterArgument': '输入 {field}',
    'runner.noDynamicArgs': '该工具没有动态参数，可直接运行。',
    'runner.cancel': '取消',
    'runner.runInTerminal': '在终端运行',

    'settings.title': '设置',
    'settings.description': '配置主题和全局默认 Python 解释器。默认解释器会作为工具运行和 Python 包管理的默认值。',
    'settings.themeTitle': '主题',
    'settings.themeDesc': '切换工作台整体外观。',
    'settings.themeDark': '深色',
    'settings.themeLight': '浅色',
    'settings.pythonTitle': '默认 Python',
    'settings.pythonDesc': '未设置时会回退到工具配置或系统 Python。',
    'settings.placeholder': '未设置时回退到工具配置或系统 Python',
    'settings.clear': '清除',

    'addTool.title': '新增工具',
    'addTool.subtitle': '将新的 EXE 或 Python 文件写入 tools.json，并在工具页立即可运行。',
    'addTool.toolType': '工具类型',
    'addTool.toolId': '工具 ID',
    'addTool.toolName': '工具名称',
    'addTool.toolPath': '工具路径',
    'addTool.pythonPath': 'Python 解释器',
    'addTool.cwd': '工作目录',
    'addTool.argsTemplate': '参数模板',
    'addTool.tags': '标签（逗号分隔）',
    'addTool.description': '描述',
    'addTool.submit': '保存工具',
    'addTool.clear': '清空',
    'addTool.pythonHelp': '可留空，留空时使用系统 python。',
    'addTool.exeHint': '示例: C:/Windows/System32/cmd.exe',
    'addTool.pyHint': '示例: Tools/testdemo.py',
    'addTool.idHint': '仅允许字母、数字、下划线、短横线和点',
    'addTool.tagsHint': '例如: 数据, 自动化, 系统',
    'addTool.validationId': '工具 ID 不能为空',
    'addTool.validationIdFormat': '工具 ID 格式不合法',
    'addTool.validationName': '工具名称不能为空',
    'addTool.validationPath': '工具路径不能为空',
    'addTool.resetDone': '已清空表单',

    'log.title': '日志输出',
    'log.noRunSelected': '请选择一条运行记录',
    'log.autoScrollOn': '自动滚动',
    'log.autoScrollOff': '手动',
    'log.copy': '复制',
    'log.clear': '清空',
    'log.noLogsCopy': '暂无日志可复制',
    'log.copied': '日志已复制',
    'log.copyFailed': '复制失败，请检查剪贴板权限',
    'log.empty': '当前没有日志',

  },
  'en-US': {
    'app.brand': 'ToolHub',
    'app.workspace': 'ToolHub Workspace',
    'app.layout': 'VS Code Style Layout',
    'app.activityBar': 'Activity Bar',
    'app.settings': 'Settings',
    'app.explorer': 'EXPLORER',
    'app.noActiveTerminal': 'No Active Terminal',
    'app.show': 'Show',
    'app.hide': 'Hide',
    'app.defaultPythonUpdated': 'Default Python interpreter updated',
    'app.lang.zh': '中文',
    'app.lang.en': 'EN',
    'app.menu.python': 'Python Packages',
    'app.menu.tools': 'Tools',
    'app.menu.addTool': 'Add Tool',
    'app.sessions': '{count} sessions',
    'app.session.one': '{count} session',
    'app.session.other': '{count} sessions',

    'terminal.panel': 'TERMINAL',
    'terminal.noSessions': 'No terminal sessions',
    'terminal.newProfile': 'New Profile',
    'terminal.hideProfile': 'Hide Profile',
    'terminal.split': 'Split',
    'terminal.unsplit': 'Unsplit',
    'terminal.clear': 'Clear',
    'terminal.kill': 'Kill',
    'terminal.killAll': 'Kill All',
    'terminal.killOthers': 'Kill Others',
    'terminal.killTitle': 'Kill terminal',
    'terminal.shell': 'Shell',
    'terminal.workingDir': 'Working Directory',
    'terminal.create': 'Create Terminal',
    'terminal.primary': 'Primary',
    'terminal.closeSplit': 'Close Split',
    'terminal.creatingSplit': 'Creating split terminal...',
    'terminal.waitingSplit': 'Waiting for split terminal...',
    'terminal.clearActive': 'Clear Active',
    'terminal.copyId': 'Copy Terminal ID',
    'terminal.status.running': 'running',
    'terminal.status.exited': 'exited',
    'terminal.status.stopped': 'stopped',
    'terminal.status.failed': 'failed',
    'terminal.shellFallback': 'shell',
    'terminal.currentUnavailable': 'Current terminal is unavailable. Please select or create a terminal.',

    'python.managerTitle': 'Python Package Manager',
    'python.managerDesc': 'Inspect, search, install, and uninstall packages for the selected interpreter.',
    'python.refresh': 'Refresh',
    'python.currentInterpreter': 'Current Python interpreter',
    'python.browse': 'Browse...',
    'python.systemPython': 'System Python',
    'python.installInput': 'Package name, e.g. requests or pandas==2.2.3',
    'python.install': 'Install',
    'python.ready': 'Ready',
    'python.searchInstalled': 'Search installed packages',
    'python.noPackageData': 'No package data. Try Refresh first.',
    'python.name': 'Name',
    'python.version': 'Version',
    'python.action': 'Action',
    'python.uninstallConfirm': 'Uninstall {name}?',
    'python.uninstall': 'Uninstall',
    'python.cancel': 'Cancel',
    'python.action.install': 'Install',
    'python.action.uninstall': 'Uninstall',
    'python.installing': 'Installing: {packageName}',
    'python.uninstalling': 'Uninstalling: {packageName}',
    'python.status.running': '{action} in progress: {packageName}',
    'python.status.succeeded': '{action} succeeded: {packageName}',
    'python.status.failed': '{action} failed: {packageName}{details}',

    'tools.catalog': 'Tool Catalog',
    'tools.items': '{filtered} / {total} items',
    'tools.search': 'Search by name or tag',
    'tools.ready': 'Ready',
    'tools.invalidPath': 'Invalid Path',
    'tools.run': 'Run',
    'tools.noMatch': 'No matching tools',
    'tools.edit': 'Edit',
    'tools.editSelected': 'Edit Selected',
    'tools.deleteSelected': 'Delete Selected',
    'tools.selected': 'Selected',
    'tools.confirmDeleteTitle': 'Batch Delete',
    'tools.confirmDeleteMessage': 'Delete selected tools?',
    'tools.save': 'Save',
    'tools.cancel': 'Cancel',
    'tools.browse': 'Browse...',
    'tools.name': 'Name',
    'tools.type': 'Type',
    'tools.path': 'Path',
    'tools.python': 'Python',
    'tools.cwd': 'Working Directory',
    'tools.argsTemplate': 'Args Template',
    'tools.tags': 'Tags (comma separated)',
    'tools.description': 'Description',
    'tools.noSelection': 'Select at least one tool',
    'tools.editDialog': 'Edit Tool',
    'tools.validationId': 'Tool ID is required',
    'tools.validationIdFormat': 'Tool ID format is invalid',
    'tools.validationName': 'Tool name is required',
    'tools.validationPath': 'Tool path is required',
    'tools.added': 'Tool added: {toolId}',
    'tools.updated': 'Tool updated: {toolId}',
    'tools.deleted': '{count} tool(s) deleted',

    'runner.title': 'Tool Details',
    'runner.invalidConfig': 'Invalid tool configuration. Running is disabled.',
    'runner.name': 'Name',
    'runner.id': 'ID',
    'runner.type': 'Type',
    'runner.path': 'Path',
    'runner.cwd': 'Working Directory',
    'runner.python': 'Python',
    'runner.systemPython': 'system python',
    'runner.argsTemplate': 'Args Template',
    'runner.none': '(none)',
    'runner.description': 'Description',
    'runner.pythonInterpreter': 'Python Interpreter',
    'runner.pythonFallback': 'Fallback order: custom override -> tools.json python -> system python',
    'runner.pythonFallbackWithAppDefault': 'Priority: manual override -> tool default -> app default -> system Python',
    'runner.useToolDefault': 'Use Tool Default',
    'runner.useAppDefault': 'Use App Default',
    'runner.useSystemPython': 'Use System Python',
    'runner.pythonExample': 'Example: C:\\project\\.venv\\Scripts\\python.exe',
    'runner.argument': 'Argument {field}',
    'runner.enterArgument': 'Enter {field}',
    'runner.noDynamicArgs': 'This tool has no dynamic arguments. You can run it directly.',
    'runner.cancel': 'Cancel',
    'runner.runInTerminal': 'Run In Terminal',

    'settings.title': 'Settings',
    'settings.description': 'Configure the theme and the global default Python interpreter used by tools and the package manager.',
    'settings.themeTitle': 'Theme',
    'settings.themeDesc': 'Switch the overall workbench appearance.',
    'settings.themeDark': 'Dark',
    'settings.themeLight': 'Light',
    'settings.pythonTitle': 'Default Python',
    'settings.pythonDesc': 'Falls back to tool config or system Python when empty.',
    'settings.placeholder': 'Falls back to tool config or system Python when empty',
    'settings.clear': 'Clear',

    'addTool.title': 'Add Tool',
    'addTool.subtitle': 'Write a new EXE or Python tool into tools.json so it can run immediately.',
    'addTool.toolType': 'Tool Type',
    'addTool.toolId': 'Tool ID',
    'addTool.toolName': 'Tool Name',
    'addTool.toolPath': 'Tool Path',
    'addTool.pythonPath': 'Python Interpreter',
    'addTool.cwd': 'Working Directory',
    'addTool.argsTemplate': 'Args Template',
    'addTool.tags': 'Tags (comma separated)',
    'addTool.description': 'Description',
    'addTool.submit': 'Save Tool',
    'addTool.clear': 'Clear',
    'addTool.pythonHelp': 'Optional. Leave empty to use system python.',
    'addTool.exeHint': 'Example: C:/Windows/System32/cmd.exe',
    'addTool.pyHint': 'Example: Tools/testdemo.py',
    'addTool.idHint': 'Allowed: letters, numbers, underscore, dash, dot',
    'addTool.tagsHint': 'Example: data, automation, system',
    'addTool.validationId': 'Tool ID is required',
    'addTool.validationIdFormat': 'Tool ID format is invalid',
    'addTool.validationName': 'Tool name is required',
    'addTool.validationPath': 'Tool path is required',
    'addTool.resetDone': 'Form cleared',

    'log.title': 'Log Output',
    'log.noRunSelected': 'Please select a run',
    'log.autoScrollOn': 'Auto Scroll',
    'log.autoScrollOff': 'Manual',
    'log.copy': 'Copy',
    'log.clear': 'Clear',
    'log.noLogsCopy': 'No logs to copy',
    'log.copied': 'Logs copied to clipboard',
    'log.copyFailed': 'Copy failed, check clipboard permissions',
    'log.empty': 'No logs available',

  },
}

function loadLocale(): Locale {
  if (typeof window === 'undefined') {
    return 'zh-CN'
  }

  try {
    const saved = window.localStorage.getItem(LOCALE_STORAGE_KEY)
    if (saved === 'zh-CN' || saved === 'en-US') {
      return saved
    }
  } catch {
    // ignore
  }

  return 'zh-CN'
}

const locale = ref<Locale>(loadLocale())

function persistLocale(next: Locale): void {
  if (typeof window === 'undefined') {
    return
  }

  try {
    window.localStorage.setItem(LOCALE_STORAGE_KEY, next)
  } catch {
    // ignore
  }
}

function setLocale(next: Locale): void {
  if (locale.value === next) {
    return
  }

  locale.value = next
  persistLocale(next)
}

function interpolate(template: string, params?: MessageParams): string {
  if (!params) {
    return template
  }

  return template.replace(/\{([a-zA-Z0-9_]+)\}/g, (full, key: string) => {
    const value = params[key]
    return value === undefined ? full : String(value)
  })
}

function t(key: string, params?: MessageParams): string {
  const template = messages[locale.value][key] ?? messages['en-US'][key] ?? key
  return interpolate(template, params)
}

function formatSessionCount(count: number): string {
  if (locale.value === 'en-US') {
    return t(count === 1 ? 'app.session.one' : 'app.session.other', { count })
  }

  return t('app.sessions', { count })
}

export function useI18n() {
  return {
    locale: computed(() => locale.value),
    setLocale,
    t,
    formatSessionCount,
  }
}
