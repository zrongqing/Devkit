import { hasPermission, request } from './session'
import type { PageResult } from './tableQueries'

export type SegmentType = 'literal' | 'year' | 'month' | 'day' | 'sequence'
export type Segment = { type: SegmentType; text?: string; width?: number }
export type Definition = {
  schemaVersion: number
  utcOffset: string
  reset: string
  start: number
  step: number
  segments: Segment[]
}
export interface Scenario {
  ruleKey: string
  moduleKey: string
  objectType: string
}
export interface Rule {
  id: string
  ruleKey: string
  moduleKey: string
  name: string
  status: string
  activeVersionNo: number | null
}
export interface Detail {
  rule: Rule
  revision: number
  active: Definition | null
  draft: Definition | null
}
export interface Version {
  version: { versionNo: number }
  definition: Definition
  reason: string
}
export const canCode = (module: string, action: string) =>
  hasPermission(`code.scope.${module}.${action}`)
const root = '/api/v1/code-system'
const send = <T>(path: string, body: unknown, method = 'POST') =>
  request<T>(root + path, { method, body: JSON.stringify(body) })
export const codeApi = {
  async scenarios(signal: AbortSignal) {
    const items: Scenario[] = []
    for (let page = 1; ; page++) {
      const result = await request<PageResult<Scenario>>(
        root + '/scenarios/query',
        {
          method: 'POST',
          signal,
          body: JSON.stringify({
            page,
            pageSize: 100,
            filters: [],
            sorts: [{ field: 'ruleKey', direction: 'asc' }],
          }),
        },
      )
      items.push(...result.items)
      if (items.length >= result.total || !result.items.length) return items
    }
  },
  detail: (id: string) => request<Detail>(root + '/rules/' + id),
  version: (id: string, no: number) =>
    request<Version>(root + '/rules/' + id + '/versions/' + no),
  create: (ruleKey: string, name: string, definition: Definition) =>
    send<{ id: string }>('/rules', { ruleKey, name, definition }),
  save: (id: string, expectedRevision: number, definition: Definition) =>
    send('/rules/' + id + '/draft', { expectedRevision, definition }, 'PUT'),
  preview: (
    ruleKey: string,
    definition: Definition,
    sampleAt: string,
    sampleValue: number,
  ) =>
    send<{ code: string; periodKey: string }>('/preview', {
      ruleKey,
      definition,
      sampleAt,
      sampleValue,
    }),
  transition: (
    id: string,
    action: 'publish' | 'enable' | 'disable',
    expectedRevision: number,
    reason: string,
  ) => send('/rules/' + id + '/' + action, { expectedRevision, reason }),
}
export const blankDefinition = (): Definition => ({
  schemaVersion: 1,
  utcOffset: '+08:00',
  reset: 'none',
  start: 1,
  step: 1,
  segments: [{ type: 'sequence', width: 4 }],
})
export const resetLabels: Record<string, string> = {
  none: '全程累计',
  year: '每年',
  month: '每月',
  day: '每日',
}
export const segmentLabels: Record<SegmentType, string> = {
  literal: '固定文本',
  year: '年',
  month: '月',
  day: '日',
  sequence: '流水',
}
export function segmentInputError(segment: Segment): string {
  if (segment.type === 'literal') {
    const text = segment.text ?? ''
    if (!text.length || text.length > 32 || /[^A-Z0-9_./-]/.test(text)) {
      return '固定文本须为 1–32 个字符，只能使用大写英文字母、数字和 - _ . /。'
    }
  }
  if (segment.type === 'sequence') {
    const width = segment.width
    if (width == null || !Number.isInteger(width) || width < 1 || width > 12) {
      return '流水位宽须为 1–12 的整数。'
    }
  }
  return ''
}
export function formula(d: Definition | null) {
  return (
    d?.segments
      .map((s) =>
        s.type === 'literal'
          ? s.text
          : s.type === 'sequence'
            ? `〔流水 ${s.width} 位〕`
            : `〔${segmentLabels[s.type]}〕`,
      )
      .join('') ?? '—'
  )
}
