/* PermissionSystem mobile shell worker.
 *
 * The worker deliberately has a narrow cache boundary: only same-origin
 * static shell files and Vite hashed assets are eligible. API, OAuth,
 * websocket and all non-GET traffic always go to the network.
 */
const CACHE_NAME = 'permission-mobile-shell-v1'
const PRECACHE_URLS = ['/', '/index.html', '/manifest.webmanifest', '/icons/app-icon.svg']
const EXCLUDED_PREFIXES = ['/api/', '/connect/', '/hubs/']
const HASHED_ASSET = /^\/assets\/[^/]+-[A-Za-z0-9_-]{8,}\.[A-Za-z0-9]+$/

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then(async (cache) => {
      // A missing optional icon must not prevent the shell from installing.
      await Promise.all(
        PRECACHE_URLS.map(async (url) => {
          try {
            const response = await fetch(url, { credentials: 'same-origin' })
            if (response.ok && isCacheableStaticResponse(response, new URL(url, self.location.href))) {
              await cache.put(url, response)
            }
          } catch {
            // The next navigation will retry the resource from the network.
          }
        }),
      )
    }),
  )
  self.skipWaiting()
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key))))
      .then(() => self.clients.claim()),
  )
})

self.addEventListener('fetch', (event) => {
  const request = event.request
  if (request.method !== 'GET') return

  const url = new URL(request.url)
  if (url.origin !== self.location.origin || isExcludedPath(url.pathname) || hasSensitiveQuery(url.searchParams)) return

  if (request.mode === 'navigate') {
    // Network-first keeps deployments current; the cached shell only handles
    // an offline navigation and is never used for protocol endpoints.
    event.respondWith(
      fetch(request).catch(async () => {
        const cached = await caches.match('/index.html')
        return cached || Response.error()
      }),
    )
    return
  }

  if (!HASHED_ASSET.test(url.pathname)) return

  event.respondWith(
    caches.match(request).then(async (cached) => {
      if (cached) return cached
      try {
        const response = await fetch(request)
        if (isCacheableStaticResponse(response, url)) {
          const cache = await caches.open(CACHE_NAME)
          await cache.put(request, response.clone())
        }
        return response
      } catch {
        return Response.error()
      }
    }),
  )
})

function isExcludedPath(pathname) {
  return EXCLUDED_PREFIXES.some((prefix) => pathname === prefix.slice(0, -1) || pathname.startsWith(prefix))
}

function hasSensitiveQuery(params) {
  return ['access_token', 'refresh_token', 'id_token', 'code', 'state', 'token', 'authorization'].some((key) => params.has(key))
}

function isCacheableStaticResponse(response, url) {
  if (!response || !response.ok || response.type !== 'basic') return false
  if (url.origin !== self.location.origin || isExcludedPath(url.pathname)) return false
  // Never persist responses that could carry an authenticated/session cookie.
  return !response.headers.has('set-cookie')
}
