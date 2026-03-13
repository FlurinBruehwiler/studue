/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_GIT_COMMIT_HASH?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
