import { request } from './request'
import { getAccessToken } from './token'

const recordSeparator = String.fromCharCode(0x1e)

interface SignalRInvocationMessage {
  type: number
  target?: string
  arguments?: unknown[]
}

export interface SignalRLiteConnection {
  stop: () => void
  subscribeRun?: (runId: string) => void
}

export async function startNotificationConnection(
  onNotification: (message: unknown) => void,
): Promise<SignalRLiteConnection | undefined> {
  return startHubConnection('/hubs/notifications', 'ReceiveNotification', onNotification)
}

export async function startAiRunConnection(
  onRunEvent: (message: unknown) => void,
  onReconnect?: () => void,
): Promise<SignalRLiteConnection | undefined> {
  return startHubConnection('/hubs/ai', 'ReceiveAiRunEvent', onRunEvent, onReconnect)
}

async function startHubConnection(
  hubPath: string,
  target: string,
  onMessage: (message: unknown) => void,
  onReconnect?: () => void,
): Promise<SignalRLiteConnection | undefined> {
  const accessToken = getAccessToken()
  if (!accessToken) {
    return undefined
  }

  let stopped = false
  let socket: WebSocket | undefined
  let subscribedRun = ''
  let ready = false
  let reconnectTimer: ReturnType<typeof setTimeout> | undefined
  function subscribeRun(runId: string) {
    subscribedRun = runId
    if (ready && socket?.readyState === WebSocket.OPEN && runId)
      socket.send(
        `${JSON.stringify({ type: 1, target: 'SubscribeRun', arguments: [runId] })}${recordSeparator}`,
      )
  }

  async function connect() {
    const token = getAccessToken()
    if (!token || stopped) {
      return
    }

    const negotiateUrl = `${getHttpBaseUrl()}${hubPath}/negotiate?negotiateVersion=1`
    const negotiate = await fetch(negotiateUrl, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
      },
    })

    if (!negotiate.ok) {
      throw new Error(`SignalR negotiate failed: ${negotiate.status}`)
    }

    const payload = (await negotiate.json()) as { connectionToken?: string; connectionId?: string }
    const connectionToken = payload.connectionToken ?? payload.connectionId
    if (!connectionToken) {
      throw new Error('SignalR negotiate response does not contain a connection token.')
    }

    socket = new WebSocket(
      `${getWebSocketBaseUrl()}${hubPath}?id=${encodeURIComponent(connectionToken)}&access_token=${encodeURIComponent(token)}`,
    )
    ready = false

    socket.onopen = () => {
      socket?.send(`${JSON.stringify({ protocol: 'json', version: 1 })}${recordSeparator}`)
    }

    socket.onmessage = (event) => {
      const frames = String(event.data).split(recordSeparator).filter(Boolean)

      for (const frame of frames) {
        const message = JSON.parse(frame) as SignalRInvocationMessage
        if (message.type === undefined) {
          ready = true
          subscribeRun(subscribedRun)
          onReconnect?.()
        }
        if (message.type === 1 && message.target === target) {
          onMessage(message.arguments?.[0])
        }
      }
    }

    socket.onclose = () => {
      if (!stopped) {
        reconnectTimer = window.setTimeout(() => {
          connect().catch(() => undefined)
        }, 5000)
      }
    }
  }

  await connect()

  return {
    stop() {
      stopped = true
      if (reconnectTimer) clearTimeout(reconnectTimer)
      socket?.close()
    },
    subscribeRun,
  }
}

function getHttpBaseUrl() {
  return (request.defaults.baseURL || window.location.origin).replace(/\/+$/, '')
}

function getWebSocketBaseUrl() {
  const httpBaseUrl = getHttpBaseUrl()
  if (httpBaseUrl.startsWith('https://')) {
    return httpBaseUrl.replace(/^https:\/\//, 'wss://')
  }

  if (httpBaseUrl.startsWith('http://')) {
    return httpBaseUrl.replace(/^http:\/\//, 'ws://')
  }

  const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:'
  return `${protocol}//${window.location.host}`
}
