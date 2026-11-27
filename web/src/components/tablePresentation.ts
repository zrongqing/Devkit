export type ColumnPin = 'left' | 'right' | false
export interface ColumnOption {
  key: string
  label: string
  field?: string
  visible: boolean
  pin: ColumnPin
}
export interface TableFilterTag { key: string; label: string; value: string; external?: boolean }

export interface FilterOption { value: string; label: string }
export interface RangeFilterDraft {
  min: string
  max: string
  invalidMin?: { value: string; message: string }
  invalidMax?: { value: string; message: string }
}
export type FilterDraft = string | string[] | RangeFilterDraft
export interface ColumnFilterConfig {
  type: 'text' | 'select' | 'numberRange' | 'dateRange'
  /** Filtering may use a stable relation ID while sorting uses the displayed column. */
  field?: string
  operator?: 'in' | 'anyOf'
  options?: FilterOption[]
  /** Fetch authorized candidates, independent of the currently displayed page. */
  loadOptions?: (keyword: string, signal: AbortSignal) => Promise<FilterOption[]>
}
export type TableFilterSchema = Record<string, ColumnFilterConfig>
