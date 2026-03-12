import { enUSMessages } from './en-US'
import { zhCNMessages } from './zh-CN'

export type MessageDictionary = Record<string, string>

export const messages = {
  'zh-CN': zhCNMessages,
  'en-US': enUSMessages,
} satisfies Record<'zh-CN' | 'en-US', MessageDictionary>
