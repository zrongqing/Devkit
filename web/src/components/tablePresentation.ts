export type ColumnPin = 'left' | 'right' | false
export interface ColumnOption {
  key: string
  label: string
  field?: string
  visible: boolean
  pin: ColumnPin
}
export interface TableFilterTag { key: string; label: string; value: string; external?: boolean }
