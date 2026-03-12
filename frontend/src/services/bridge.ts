import type { BackMessage, FrontMessage } from '../types'

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
      ext.sendMessage(payload)
      return
    }

    console.warn('[Bridge] window.external.sendMessage 不可用，当前可能是浏览器开发模式。', message)
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
      console.error('[Bridge] 解析消息失败:', error, raw)
    }
  }

  private getExternal(): PhotinoExternal | undefined {
    return (window as unknown as { external?: PhotinoExternal }).external
  }
}

export const bridge = new Bridge()
