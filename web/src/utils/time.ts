const formatters = new Map<string, Intl.DateTimeFormat>()

export function supportsTimeZone(timeZone: string): boolean {
  try {
    new Intl.DateTimeFormat('zh-CN', { timeZone })
    return true
  } catch {
    return false
  }
}

export function formatTime(value: string | number | null | undefined, timeZone: string): string {
  if (value == null || value === '') return '—'
  const time = typeof value === 'number' ? value : Date.parse(/(?:z|[+-]\d{2}:?\d{2})$/i.test(value) ? value : `${value}Z`)
  if (!Number.isFinite(time)) return '—'
  let formatter = formatters.get(timeZone)
  if (!formatter) {
    formatter = new Intl.DateTimeFormat('zh-CN', {
      timeZone, year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23',
    })
    formatters.set(timeZone, formatter)
  }
  return formatter.format(time)
}

