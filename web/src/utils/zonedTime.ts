import { Temporal } from '@js-temporal/polyfill'

export function localTimeInput(utc: string, timeZone: string): string {
  return Temporal.Instant.from(utc).toZonedDateTimeISO(timeZone).toPlainDateTime().toString({ smallestUnit: 'second' })
}

export function localTimeToUtc(value: string, timeZone: string): string {
  const time = Temporal.PlainDateTime.from(value)
  if (time.year < 1 || time.year > 9999) throw new RangeError('时间年份须在 1 至 9999 之间。')
  const instant = time.toZonedDateTime(timeZone, { disambiguation: 'reject' }).toInstant()
  const utc = instant.toString({ smallestUnit: 'second' })
  if (!/^\d{4}-/.test(utc) || utc.startsWith('0000-')) throw new RangeError('时间超出可查询范围。')
  return utc
}
