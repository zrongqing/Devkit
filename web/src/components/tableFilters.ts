import type { FieldFilter } from '../api/tableQueries'
import { formatDisplayTime } from '../stores/systemSettings'
import type { ColumnFilterConfig, FilterDraft, FilterOption, RangeFilterDraft } from './tablePresentation'

export function hasFilter(value: FilterDraft | undefined): boolean {
  return typeof value === 'string' ? !!value.trim() : Array.isArray(value) ? value.length > 0 : !!(value?.min || value?.max || value?.invalidMin || value?.invalidMax)
}

export function rangeFilterError(value: RangeFilterDraft, type: ColumnFilterConfig['type'] | undefined): string {
  const invalid = value.invalidMin ?? value.invalidMax
  if (invalid) return invalid.message
  const parse = (bound: string) => type === 'dateRange' ? Date.parse(bound) : Number(bound)
  const { min, max } = value
  if ((min && !Number.isFinite(parse(min))) || (max && !Number.isFinite(parse(max)))) return '请输入有效范围。'
  return min && max && parse(min) > parse(max) ? '起始值不能大于结束值。' : ''
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
    const error = rangeFilterError(value, config?.type)
    if (error) throw new Error(error)
    return [
    ...(value.min ? [{ field, operator: 'gte' as const, value: value.min }] : []),
    ...(value.max ? [{ field, operator: 'lte' as const, value: value.max }] : []),
    ]
  }
  return [{ field, operator: 'contains', value: value.trim() }]
}

export function filterLabel(value: FilterDraft, options: FilterOption[] = [], type?: ColumnFilterConfig['type']): string {
  if (Array.isArray(value)) return value.map(v => options.find(o => o.value === v)?.label ?? v).join('、')
  if (typeof value === 'object') {
    const label = (bound: string) => !bound ? '不限' : type === 'dateRange' ? formatDisplayTime(bound) : bound
    return `${label(value.min)} ～ ${label(value.max)}`
  }
  return value
}
