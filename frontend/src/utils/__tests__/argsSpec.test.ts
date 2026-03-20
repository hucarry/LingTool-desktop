import { describe, expect, it } from 'vitest'

import {
  buildArgsSpecFromStructuredFields,
  buildLegacyArgsTemplate,
  extractStructuredArgFields,
} from '../argsSpec'

describe('argsSpec structured editor helpers', () => {
  it('extracts editable structured fields from a compatible args spec', () => {
    const extraction = extractStructuredArgFields({
      argsTemplate: '',
      argsSpec: {
        version: 1,
        fields: [
          { name: 'input', label: 'Input', kind: 'path', required: true },
          { name: 'verbose', label: 'Verbose', kind: 'flag', defaultValue: 'false' },
        ],
        argv: [
          { kind: 'literal', value: '--input' },
          { kind: 'field', field: 'input', omitWhenEmpty: false },
          { kind: 'switch', field: 'verbose', whenTrue: '--verbose' },
        ],
      },
    })

    expect(extraction.compatible).toBe(true)
    expect(extraction.fields).toEqual([
      expect.objectContaining({
        name: 'input',
        token: '--input',
        kind: 'path',
        required: true,
      }),
      expect.objectContaining({
        name: 'verbose',
        token: '--verbose',
        kind: 'flag',
      }),
    ])
  })

  it('builds args spec and legacy template from structured editor rows', () => {
    const spec = buildArgsSpecFromStructuredFields([
      {
        name: 'input',
        label: 'Input',
        description: '',
        kind: 'path',
        token: '--input',
        required: true,
        defaultValue: '',
        placeholder: 'C:/data.txt',
        optionsText: '',
      },
      {
        name: 'format',
        label: 'Format',
        description: '',
        kind: 'select',
        token: '--format',
        required: false,
        defaultValue: 'json',
        placeholder: '',
        optionsText: 'JSON=json\nCSV=csv',
      },
    ])

    expect(spec).toMatchObject({
      version: 1,
      fields: [
        {
          name: 'input',
          label: 'Input',
          kind: 'path',
          required: true,
          placeholder: 'C:/data.txt',
          options: [],
        },
        {
          name: 'format',
          label: 'Format',
          kind: 'select',
          defaultValue: 'json',
          options: [
            { label: 'JSON', value: 'json' },
            { label: 'CSV', value: 'csv' },
          ],
        },
      ],
      argv: [
        { kind: 'literal', value: '--input' },
        { kind: 'field', field: 'input', omitWhenEmpty: false },
        { kind: 'literal', value: '--format' },
        { kind: 'field', field: 'format', omitWhenEmpty: true },
      ],
    })
    expect(buildLegacyArgsTemplate(spec, '')).toBe('--input {input} --format {format}')
  })
})
