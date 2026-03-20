import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

import { describe, expect, it } from 'vitest'

import {
  BRIDGE_MESSAGE_TYPES,
  LOG_CHANNELS,
} from '../bridgeMessageTypes'

function readMessagesSource(): string {
  return readFileSync(
    resolve(process.cwd(), '../ToolHub.App/Models/Messages.cs'),
    'utf-8',
  )
}

function extractConstValues(source: string, className: string): string[] {
  const classPattern = new RegExp(`public static class ${className}\\s*\\{([\\s\\S]*?)\\n\\}`, 'm')
  const body = source.match(classPattern)?.[1]

  if (!body) {
    throw new Error(`missing ${className} definition`)
  }

  return [...body.matchAll(/public const string \w+ = "([^"]+)";/g)].map((match) => match[1]!)
}

describe('bridge message types', () => {
  it('matches the C# bridge message type definitions', () => {
    const source = readMessagesSource()
    const csharpValues = extractConstValues(source, 'BridgeMessageTypes').sort()
    const frontendValues = Object.values(BRIDGE_MESSAGE_TYPES).sort()

    expect(frontendValues).toEqual(csharpValues)
  })

  it('matches the C# log channel definitions', () => {
    const source = readMessagesSource()
    const csharpValues = extractConstValues(source, 'LogChannels').sort()
    const frontendValues = Object.values(LOG_CHANNELS).sort()

    expect(frontendValues).toEqual(csharpValues)
  })
})
