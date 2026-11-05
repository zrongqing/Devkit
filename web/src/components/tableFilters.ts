import type { FieldFilter } from '../api/tableQueries'
import type { ColumnFilterConfig, FilterDraft, FilterOption } from './tablePresentation'

export function hasFilter(value: FilterDraft | undefined): boolean {
  return typeof value === 'string' ? !!value.trim() : Array.isArray(value) ? value.length > 0 : !!(value?.min || value?.max)
}

export function copyDrafts(drafts: Record<string, FilterDraft>): Record<string, FilterDraft> {
  return Object.fromEntries(Object.entries(drafts).map(([field, value]) => [field,
    Array.isArray(value) ? [...value] : typeof value === 'object' ? { ...value } : value,
  ]))
}

export function buildFilter(field: string, value: FilterDraft, config?: ColumnFilterConfig): FieldFilter[] {
  if (!hasFilter(value)) return []
  field = config?.field ?? field
  if (Array.isArray(value)) return [{ field, operator: config?.operator ?? 'in', values: [...value] }]
  if (typeof value === 'object') {
    const parse = (v: string) => config?.type === 'dateRange' ? Date.parse(v) : Number(v)
    if ((value.min && !Number.isFinite(parse(value.min))) || (value.max && !Number.isFinite(parse(value.max))) ||
      (value.min && value.max && parse(value.min) > parse(value.max))) throw new Error('请检查筛选范围，起始值不能大于结束值。')
    return [
    ...(value.min ? [{ field, operator: 'gte' as const, value: value.min }] : []),
    ...(value.max ? [{ field, operator: 'lte' as const, value: value.max }] : []),
    ]
  }
  return [{ field, operator: 'contains', value: value.trim() }]
}

export function filterLabel(value: FilterDraft, options: FilterOption[] = []): string {
  if (Array.isArray(value)) return value.map(v => options.find(o => o.value === v)?.label ?? v).join('、')
  if (typeof value === 'object') return `${value.min || '不限'} ～ ${value.max || '不限'}`
  return value
}
