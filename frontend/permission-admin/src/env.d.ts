/// <reference types="vite/client" />

import type { displayText, yesNo } from './utils/display'

declare global {
  interface ImportMetaEnv {
    readonly VITE_API_BASE_URL: string
    readonly VITE_OAUTH_CLIENT_ID: string
    readonly VITE_OAUTH_CLIENT_SECRET: string
  }

  interface ImportMeta {
    readonly env: ImportMetaEnv
  }
}

declare module '@vue/runtime-core' {
  interface ComponentCustomProperties {
    $displayText: typeof displayText
    $yesNo: typeof yesNo
  }
}

declare module 'vue-router' {
  interface RouteMeta {
    public?: boolean
    title?: string
    icon?: string
    hidden?: boolean
    affix?: boolean
    noCache?: boolean
    cacheName?: string
    alwaysShowTab?: boolean
    activeMenu?: string
    permissionCode?: string
    order?: number
  }
}

export {}
