import type { BackMessage, FrontMessage } from '../types'
import { BRIDGE_MESSAGE_TYPES } from '../types/bridgeMessageTypes'

type MessageListener = (message: BackMessage) => void

type PhotinoExternal = {
  sendMessage?: (message: string) => void
  receiveMessage?: (callback: (message: string) => void) => void
}

class Bridge {
  private readonly listeners = new Set<MessageListener>()
  private initialized = false

  send(message: FrontMessage): void {
    const payload = JSON.stringify(message)

    const ext = this.getExternal()
    if (ext?.sendMessage) {
      try {
        ext.sendMessage(payload)
      } catch (error) {
        console.error('[Bridge] failed to send message to host:', error, message)
        this.dispatch(JSON.stringify({
          type: BRIDGE_MESSAGE_TYPES.ERROR,
          message: 'UI Bridge Communication Failed',
          details: error instanceof Error ? error.message : String(error),
        }))
      }
      return
    }

    console.warn('[Bridge] window.external.sendMessage is unavailable, running in browser mode?', message)
  }

  onMessage(listener: MessageListener): () => void {
    this.listeners.add(listener)
    this.ensureReceiver()

    return () => {
      this.listeners.delete(listener)
    }
  }

  private ensureReceiver(): void {
    if (this.initialized) {
      return
    }

    this.initialized = true

    const ext = this.getExternal()
    if (ext?.receiveMessage) {
      ext.receiveMessage((raw: string) => this.dispatch(raw))
      return
    }

    window.addEventListener('message', (event: MessageEvent) => {
      if (typeof event.data === 'string') {
        this.dispatch(event.data)
      }
    })
  }

  private dispatch(raw: string): void {
    try {
      const parsed = JSON.parse(raw) as BackMessage
      this.listeners.forEach((listener) => listener(parsed))
    } catch (error) {
      console.error('[Bridge] failed to parse message:', error, raw)
    }
  }

  private getExternal(): PhotinoExternal | undefined {
    return (window as unknown as { external?: PhotinoExternal }).external
  }
}

export const bridge = new Bridge()
