import type { ArgFieldKind, ArgFieldOption, ArgFieldSpec, ArgsSpecV1, ArgTokenSpec, ToolItem } from '../types'

const PLACEHOLDER_REGEX = /\{([a-zA-Z_][a-zA-Z0-9_]*)\}/g

export interface RunnerArgField {
  name: string
  label: string
  description?: string
  kind: ArgFieldKind
  required: boolean
  defaultValue: string
  placeholder: string
  options: Array<{ label: string; value: string }>
}

export interface StructuredArgFieldDraft {
  name: string
  label: string
  description: string
  kind: ArgFieldKind
  token: string
  required: boolean
  defaultValue: string
  placeholder: string
  optionsText: string
}

export interface StructuredArgFieldExtraction {
  fields: StructuredArgFieldDraft[]
  compatible: boolean
}

function splitCommandLine(input: string): string[] {
  const tokens: string[] = []
  let current = ''
  let quote: '"' | "'" | null = null

  for (let index = 0; index < input.length; index += 1) {
    const char = input[index]!

    if (quote) {
      if (char === quote) {
        quote = null
      } else {
        current += char
      }
      continue
    }

    if (char === '"' || char === "'") {
      quote = char
      continue
    }

    if (/\s/.test(char)) {
      if (current.length > 0) {
        tokens.push(current)
        current = ''
      }
      continue
    }

    current += char
  }

  if (current.length > 0) {
    tokens.push(current)
  }

  return tokens
}

function normalizeFieldKind(kind?: string): ArgFieldKind {
  switch (kind?.trim().toLowerCase()) {
    case 'path':
      return 'path'
    case 'number':
      return 'number'
    case 'flag':
      return 'flag'
    case 'select':
      return 'select'
    case 'secret':
      return 'secret'
    default:
      return 'text'
  }
}

function normalizeTokenKind(kind?: string): ArgTokenSpec['kind'] {
  switch (kind?.trim().toLowerCase()) {
    case 'field':
      return 'field'
    case 'switch':
      return 'switch'
    default:
      return 'literal'
  }
}

function normalizeOptional(value?: string): string | undefined {
  const trimmed = value?.trim()
  return trimmed ? trimmed : undefined
}

function toCliOptionName(name: string): string {
  const normalized = name
    .trim()
    .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
    .replace(/[^a-zA-Z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .toLowerCase()

  return normalized ? `--${normalized}` : ''
}

function formatArgOptions(options?: ArgFieldOption[]): string {
  return (options ?? [])
    .map((option) => {
      const label = option.label?.trim() || option.value.trim()
      const value = option.value.trim()
      return label === value ? value : `${label}=${value}`
    })
    .join('\n')
}

function parseArgOptions(optionsText: string): ArgFieldOption[] {
  return optionsText
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .reduce<ArgFieldOption[]>((list, line) => {
      const separator = line.indexOf('=')
      const label = separator >= 0 ? line.slice(0, separator).trim() : line
      const value = separator >= 0 ? line.slice(separator + 1).trim() : line

      if (!value || list.some((option) => option.value === value)) {
        return list
      }

      list.push({
        label: label || value,
        value,
      })

      return list
    }, [])
}

function createStructuredDraft(field?: Partial<StructuredArgFieldDraft>): StructuredArgFieldDraft {
  const name = normalizeOptional(field?.name) ?? ''
  const kind = normalizeFieldKind(field?.kind)
  return {
    name,
    label: normalizeOptional(field?.label) ?? '',
    description: normalizeOptional(field?.description) ?? '',
    kind,
    token: normalizeOptional(field?.token) ?? (name ? toCliOptionName(name) : ''),
    required: field?.required === true,
    defaultValue: normalizeOptional(field?.defaultValue) ?? (kind === 'flag' ? 'false' : ''),
    placeholder: normalizeOptional(field?.placeholder) ?? '',
    optionsText: normalizeOptional(field?.optionsText) ?? '',
  }
}

function isTruthy(value?: string): boolean {
  switch (value?.trim().toLowerCase()) {
    case '1':
    case 'true':
    case 'yes':
    case 'on':
      return true
    default:
      return false
  }
}

function createDraftFromField(field: ArgFieldSpec): StructuredArgFieldDraft {
  return createStructuredDraft({
    name: field.name,
    label: field.label,
    description: field.description,
    kind: field.kind,
    token: field.kind === 'flag' ? toCliOptionName(field.name) : toCliOptionName(field.name),
    required: field.required === true,
    defaultValue: field.defaultValue,
    placeholder: field.placeholder,
    optionsText: formatArgOptions(field.options),
  })
}

function normalizeDraftName(name: string): string {
  return name.trim().replace(/[^a-zA-Z0-9_]+/g, '_').replace(/^_+|_+$/g, '')
}

function renderLegacyToken(token: ArgTokenSpec): string {
  switch (token.kind) {
    case 'literal':
      return token.value
    case 'field':
      return `${token.prefix ?? ''}{${token.field}}${token.suffix ?? ''}`
    case 'switch':
      return `{${token.field}}`
    default:
      return ''
  }
}

function convertLegacyTokenToSpec(token: string): ArgTokenSpec | null {
  const matches = [...token.matchAll(PLACEHOLDER_REGEX)]
  if (matches.length === 0) {
    return {
      kind: 'literal',
      value: token,
    }
  }

  if (matches.length === 1) {
    const match = matches[0]!
    const fullMatch = match[0]
    const field = match[1] ?? ''
    const start = match.index ?? 0
    const end = start + fullMatch.length
    return {
      kind: 'field',
      field,
      prefix: start > 0 ? token.slice(0, start) : undefined,
      suffix: end < token.length ? token.slice(end) : undefined,
      omitWhenEmpty: false,
    }
  }

  return null
}

function buildArgumentsFromTemplate(
  template: string,
  values: Record<string, string>,
): string[] {
  if (!template.trim()) {
    return []
  }

  return splitCommandLine(template).map((token) =>
    token.replace(PLACEHOLDER_REGEX, (_, key: string) => values[key] ?? ''),
  )
}

function buildArgumentsFromSpec(
  spec: ArgsSpecV1,
  values: Record<string, string>,
): string[] {
  const fieldMap = new Map(spec.fields.map((field) => [field.name, field]))
  const resolved: string[] = []

  for (const token of spec.argv) {
    if (token.kind === 'literal') {
      if (token.value.trim()) {
        resolved.push(token.value)
      }
      continue
    }

    if (token.kind === 'field') {
      const explicit = values[token.field] ?? ''
      const fallback = fieldMap.get(token.field)?.defaultValue ?? ''
      const fieldValue = explicit || fallback
      const rendered = `${token.prefix ?? ''}${fieldValue}${token.suffix ?? ''}`

      if (!fieldValue && token.omitWhenEmpty !== false) {
        continue
      }

      resolved.push(rendered)
      continue
    }

    const explicit = values[token.field] ?? fieldMap.get(token.field)?.defaultValue ?? ''
    const selected = isTruthy(explicit) ? token.whenTrue : token.whenFalse
    if (selected?.trim()) {
      resolved.push(selected)
    }
  }

  return resolved
}

export function normalizeArgsSpec(spec?: ArgsSpecV1 | null): ArgsSpecV1 | undefined {
  if (!spec) {
    return undefined
  }

  const fields = (spec.fields ?? [])
    .filter((field) => field?.name?.trim())
    .reduce<ArgFieldSpec[]>((list, field) => {
      const normalizedName = field.name.trim()
      if (list.some((item) => item.name.toLowerCase() === normalizedName.toLowerCase())) {
        return list
      }

      list.push({
        name: normalizedName,
        label: normalizeOptional(field.label),
        description: normalizeOptional(field.description),
        kind: normalizeFieldKind(field.kind),
        required: field.required === true,
        defaultValue: normalizeOptional(field.defaultValue),
        placeholder: normalizeOptional(field.placeholder),
        options: (field.options ?? [])
          .filter((option) => option?.value?.trim())
          .map((option) => ({
            label: option.label?.trim() || option.value.trim(),
            value: option.value.trim(),
          })),
      })

      return list
    }, [])

  const argv = (spec.argv ?? [])
    .map((token): ArgTokenSpec | null => {
      const kind = normalizeTokenKind((token as { kind?: string }).kind)
      if (kind === 'literal') {
        const value = 'value' in token ? normalizeOptional(token.value) : undefined
        return value ? { kind, value } : null
      }

      if (kind === 'field') {
        const field = 'field' in token ? normalizeOptional(token.field) : undefined
        return field
          ? {
              kind,
              field,
              prefix: 'prefix' in token ? normalizeOptional(token.prefix) : undefined,
              suffix: 'suffix' in token ? normalizeOptional(token.suffix) : undefined,
              omitWhenEmpty: 'omitWhenEmpty' in token ? token.omitWhenEmpty !== false : true,
            }
          : null
      }

      const field = 'field' in token ? normalizeOptional(token.field) : undefined
      const whenTrue = 'whenTrue' in token ? normalizeOptional(token.whenTrue) : undefined
      const whenFalse = 'whenFalse' in token ? normalizeOptional(token.whenFalse) : undefined
      return field && (whenTrue || whenFalse)
        ? {
            kind,
            field,
            whenTrue: whenTrue ?? '',
            whenFalse,
          }
        : null
    })
    .filter((token): token is ArgTokenSpec => token !== null)

  if (fields.length === 0 && argv.length === 0) {
    return undefined
  }

  return {
    version: 1,
    fields,
    argv,
  }
}

export function inferArgsSpecFromTemplate(template?: string): ArgsSpecV1 | undefined {
  const trimmedTemplate = template?.trim()
  if (!trimmedTemplate) {
    return undefined
  }

  const seen = new Set<string>()
  const fields: ArgFieldSpec[] = []
  for (const match of trimmedTemplate.matchAll(PLACEHOLDER_REGEX)) {
    const name = match[1]
    if (name && !seen.has(name)) {
      seen.add(name)
      fields.push({
        name,
        kind: 'text',
      })
    }
  }

  const argv: ArgTokenSpec[] = []
  for (const token of splitCommandLine(trimmedTemplate)) {
    const converted = convertLegacyTokenToSpec(token)
    if (!converted) {
      return {
        version: 1,
        fields,
        argv: [],
      }
    }

    argv.push(converted)
  }

  return {
    version: 1,
    fields,
    argv,
  }
}

export function buildLegacyArgsTemplate(spec?: ArgsSpecV1 | null, fallback = ''): string {
  const normalized = normalizeArgsSpec(spec)
  if (!normalized || normalized.argv.length === 0) {
    return fallback.trim()
  }

  return normalized.argv.map(renderLegacyToken).join(' ').trim()
}

export function extractStructuredArgFields(
  source: Pick<ToolItem, 'argsTemplate' | 'argsSpec'> | { argsTemplate?: string; argsSpec?: ArgsSpecV1 | null },
): StructuredArgFieldExtraction {
  const normalizedSpec = normalizeArgsSpec(source.argsSpec) ?? inferArgsSpecFromTemplate(source.argsTemplate)
  if (!normalizedSpec) {
    return {
      fields: [],
      compatible: true,
    }
  }

  const drafts = normalizedSpec.fields.map((field) => createDraftFromField(field))
  const fieldIndex = new Map(drafts.map((field, index) => [field.name, index]))
  const consumed = new Set<number>()
  let compatible = true

  for (let index = 0; index < normalizedSpec.argv.length; index += 1) {
    const token = normalizedSpec.argv[index]!
    if (consumed.has(index)) {
      continue
    }

    if (token.kind === 'switch') {
      const targetIndex = fieldIndex.get(token.field)
      if (targetIndex === undefined || drafts[targetIndex]!.kind !== 'flag') {
        compatible = false
        break
      }

      drafts[targetIndex]!.token = token.whenTrue
      consumed.add(index)
      continue
    }

    if (token.kind !== 'field') {
      continue
    }

    const targetIndex = fieldIndex.get(token.field)
    if (targetIndex === undefined) {
      compatible = false
      break
    }

    const draft = drafts[targetIndex]!
    draft.required = token.omitWhenEmpty === false || draft.required

    if (token.suffix) {
      compatible = false
      break
    }

    if (token.prefix) {
      draft.token = token.prefix
      consumed.add(index)
      continue
    }

    const previous = index > 0 ? normalizedSpec.argv[index - 1] : undefined
    if (previous?.kind === 'literal' && !consumed.has(index - 1)) {
      draft.token = previous.value
      consumed.add(index - 1)
      consumed.add(index)
      continue
    }

    draft.token = ''
    consumed.add(index)
  }

  if (compatible) {
    for (let index = 0; index < normalizedSpec.argv.length; index += 1) {
      const token = normalizedSpec.argv[index]!
      if (consumed.has(index)) {
        continue
      }

      if (token.kind === 'literal' && token.value.trim()) {
        compatible = false
        break
      }

      if (token.kind !== 'literal') {
        compatible = false
        break
      }
    }
  }

  return {
    fields: compatible ? drafts : normalizedSpec.fields.map((field) => createDraftFromField(field)),
    compatible,
  }
}

export function buildArgsSpecFromStructuredFields(fields: StructuredArgFieldDraft[]): ArgsSpecV1 | undefined {
  const normalizedFields = fields
    .map((field) => {
      const name = normalizeDraftName(field.name)
      if (!name) {
        return null
      }

      const kind = normalizeFieldKind(field.kind)
      return {
        name,
        label: normalizeOptional(field.label),
        description: normalizeOptional(field.description),
        kind,
        token: normalizeOptional(field.token),
        required: kind === 'flag' ? false : field.required === true,
        defaultValue: normalizeOptional(field.defaultValue) ?? (kind === 'flag' ? 'false' : undefined),
        placeholder: normalizeOptional(field.placeholder),
        options: kind === 'select' ? parseArgOptions(field.optionsText) : [],
      }
    })
    .filter((field): field is NonNullable<typeof field> => field !== null)

  if (normalizedFields.length === 0) {
    return undefined
  }

  const fieldsSpec: ArgFieldSpec[] = normalizedFields.map((field) => ({
    name: field.name,
    label: field.label,
    description: field.description,
    kind: field.kind,
    required: field.required,
    defaultValue: field.defaultValue,
    placeholder: field.placeholder,
    options: field.options,
  }))

  const argv: ArgTokenSpec[] = []
  for (const field of normalizedFields) {
    const token = field.token?.trim() ?? ''
    if (field.kind === 'flag') {
      argv.push({
        kind: 'switch',
        field: field.name,
        whenTrue: token || toCliOptionName(field.name),
      })
      continue
    }

    if (!token) {
      argv.push({
        kind: 'field',
        field: field.name,
        omitWhenEmpty: field.required !== true,
      })
      continue
    }

    if (token.endsWith('=')) {
      argv.push({
        kind: 'field',
        field: field.name,
        prefix: token,
        omitWhenEmpty: field.required !== true,
      })
      continue
    }

    argv.push({
      kind: 'literal',
      value: token,
    })
    argv.push({
      kind: 'field',
      field: field.name,
      omitWhenEmpty: field.required !== true,
    })
  }

  return normalizeArgsSpec({
    version: 1,
    fields: fieldsSpec,
    argv,
  })
}

export function extractArgumentFields(
  source: Pick<ToolItem, 'argsTemplate' | 'argsSpec'> | { argsTemplate?: string; argsSpec?: ArgsSpecV1 | null },
): RunnerArgField[] {
  const normalizedSpec = normalizeArgsSpec(source.argsSpec)
  const inferred = normalizedSpec ?? inferArgsSpecFromTemplate(source.argsTemplate)
  const fields = inferred?.fields ?? []

  return fields.map((field) => ({
    name: field.name,
    label: field.label || field.name,
    description: field.description,
    kind: field.kind,
    required: field.required === true,
    defaultValue: field.defaultValue ?? (field.kind === 'flag' ? 'false' : ''),
    placeholder: field.placeholder ?? '',
    options: field.kind === 'flag'
      ? [
          { label: 'false', value: 'false' },
          { label: 'true', value: 'true' },
        ]
      : field.options ?? [],
  }))
}

export function buildArgumentTokens(
  source: Pick<ToolItem, 'argsTemplate' | 'argsSpec'> | { argsTemplate?: string; argsSpec?: ArgsSpecV1 | null },
  values: Record<string, string>,
): string[] {
  const normalizedSpec = normalizeArgsSpec(source.argsSpec)
  if (normalizedSpec?.argv.length) {
    return buildArgumentsFromSpec(normalizedSpec, values)
  }

  return buildArgumentsFromTemplate(source.argsTemplate?.trim() ?? '', values)
}

export function getCompatibleArgsSpec(
  currentArgsSpec: ArgsSpecV1 | null | undefined,
  argsTemplate: string,
): ArgsSpecV1 | undefined {
  const trimmedTemplate = argsTemplate.trim()
  const normalized = normalizeArgsSpec(currentArgsSpec)

  if (!trimmedTemplate) {
    return normalized
  }

  if (normalized && buildLegacyArgsTemplate(normalized) === trimmedTemplate) {
    return normalized
  }

  return inferArgsSpecFromTemplate(trimmedTemplate)
}
