import { ref, watch } from 'vue'
import { bridge } from '../services/bridge'

type Theme = 'dark' | 'light'
type BrowsePythonPurpose = 'toolRunner' | 'packageManager' | 'settingsDefaultPython'

const THEME_KEY = 'toolhub.theme'
const PYTHON_PATH_KEY = 'toolhub.defaultPythonPath'

const theme = ref<Theme>(loadTheme())
const defaultPythonPath = ref(loadPythonPath())

function loadTheme(): Theme {
    if (typeof window === 'undefined') {
        return 'dark'
    }

    try {
        const saved = window.localStorage.getItem(THEME_KEY)
        return saved === 'light' ? 'light' : 'dark'
    } catch {
        return 'dark'
    }
}

function loadPythonPath(): string {
    if (typeof window === 'undefined') {
        return ''
    }

    try {
        return window.localStorage.getItem(PYTHON_PATH_KEY) || ''
    } catch {
        return ''
    }
}

function applyTheme(newTheme: Theme) {
    const root = document.documentElement
    if (newTheme === 'light') {
        root.classList.add('light-theme')
    } else {
        root.classList.remove('light-theme')
    }
}

watch(theme, (newTheme) => {
    if (typeof window === 'undefined') {
        return
    }

    try {
        window.localStorage.setItem(THEME_KEY, newTheme)
    } catch {
        // ignore storage failures
    }
    applyTheme(newTheme)
})

watch(defaultPythonPath, (newPath) => {
    if (typeof window === 'undefined') {
        return
    }

    try {
        window.localStorage.setItem(PYTHON_PATH_KEY, newPath)
    } catch {
        // ignore storage failures
    }
})

function pickDefaultPython() {
    bridge.send({
        type: 'browsePython',
        defaultPath: defaultPythonPath.value,
        purpose: 'settingsDefaultPython' satisfies BrowsePythonPurpose,
    })
}

function setDefaultPythonPath(path: string): void {
    defaultPythonPath.value = path.trim()
}

function clearDefaultPythonPath(): void {
    defaultPythonPath.value = ''
}

// Initial application
if (typeof document !== 'undefined') {
    applyTheme(theme.value)
}

export function useSettings() {
    return {
        theme,
        defaultPythonPath,
        pickDefaultPython,
        setDefaultPythonPath,
        clearDefaultPythonPath,
    }
}
