let installed = false
let pendingScrollState: unknown

function isBrowser() {
  return typeof window !== 'undefined' && typeof document !== 'undefined' && typeof navigator !== 'undefined'
}

function isEdgeBrowser() {
  return /\bEdg\//.test(navigator.userAgent)
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

function hasScrollPosition(state: unknown) {
  if (!isRecord(state)) {
    return false
  }

  const scroll = state.scroll
  return isRecord(scroll) && typeof scroll.left === 'number' && typeof scroll.top === 'number'
}

function isScrollOnlyStateUpdate(nextState: unknown) {
  const currentState = window.history.state

  if (!isRecord(currentState) || !isRecord(nextState) || !hasScrollPosition(nextState)) {
    return false
  }

  const keys = new Set([...Object.keys(currentState), ...Object.keys(nextState)])
  keys.delete('scroll')

  for (const key of keys) {
    if (!Object.is(currentState[key], nextState[key])) {
      return false
    }
  }

  return true
}

export function installEdgeMinimizeHistoryPatch() {
  if (!isBrowser() || installed || !isEdgeBrowser()) {
    return
  }

  installed = true
  const originalReplaceState = window.history.replaceState

  function flushPendingScrollState() {
    if (document.visibilityState !== 'visible' || !pendingScrollState) {
      return
    }

    const state = pendingScrollState
    pendingScrollState = undefined

    if (isScrollOnlyStateUpdate(state)) {
      originalReplaceState.call(window.history, state, '')
    }
  }

  window.history.replaceState = function patchedReplaceState(
    data: unknown,
    unused: string,
    url?: string | URL | null,
  ) {
    const isHiddenScrollSave =
      document.visibilityState === 'hidden' && arguments.length < 3 && isScrollOnlyStateUpdate(data)

    if (isHiddenScrollSave) {
      pendingScrollState = data
      return
    }

    if (arguments.length < 3) {
      return originalReplaceState.call(this, data, unused)
    }

    return originalReplaceState.call(this, data, unused, url)
  }

  document.addEventListener('visibilitychange', flushPendingScrollState)
  window.addEventListener('pageshow', flushPendingScrollState)
}
