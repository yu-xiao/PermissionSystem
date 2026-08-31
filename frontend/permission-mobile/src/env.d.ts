/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string
  readonly VITE_APP_VERSION?: string
  readonly VITE_OAUTH_ISSUER?: string
  readonly VITE_OAUTH_CLIENT_ID?: string
  readonly VITE_OAUTH_REDIRECT_URI?: string
  readonly VITE_OAUTH_SCOPE?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
