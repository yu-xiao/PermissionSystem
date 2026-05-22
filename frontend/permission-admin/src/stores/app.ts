import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

const sidebarCollapsedKey = 'permission_system_sidebar_collapsed'
const themeKey = 'permission_system_theme'

type ThemeMode = 'light' | 'dark'

function readBoolean(key: string) {
  return localStorage.getItem(key) === 'true'
}

function readTheme(): ThemeMode {
  return localStorage.getItem(themeKey) === 'dark' ? 'dark' : 'light'
}

export const useAppStore = defineStore('app', () => {
  const sidebarCollapsed = ref(readBoolean(sidebarCollapsedKey))
  const theme = ref<ThemeMode>(readTheme())
  const isDark = computed(() => theme.value === 'dark')

  function toggleSidebar() {
    sidebarCollapsed.value = !sidebarCollapsed.value
    localStorage.setItem(sidebarCollapsedKey, String(sidebarCollapsed.value))
  }

  function setTheme(value: ThemeMode) {
    theme.value = value
    localStorage.setItem(themeKey, value)
    applyTheme()
  }

  function toggleTheme() {
    setTheme(isDark.value ? 'light' : 'dark')
  }

  function applyTheme() {
    document.documentElement.dataset.theme = theme.value
    document.documentElement.classList.toggle('dark', theme.value === 'dark')
  }

  return {
    sidebarCollapsed,
    theme,
    isDark,
    toggleSidebar,
    setTheme,
    toggleTheme,
    applyTheme,
  }
})
