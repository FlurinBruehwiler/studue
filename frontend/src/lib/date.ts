export function formatDisplayDate(dateString: string): string {
  const [year, month, day] = dateString.split('-')
  if (!year || !month || !day) {
    return dateString
  }

  return `${day}.${month}`
}

export function formatSwissDate(dateString: string): string {
  const [year, month, day] = dateString.split('-')
  if (!year || !month || !day) {
    return dateString
  }

  return `${day}.${month}.${year}`
}

export function formatSwissDateAndTime(dateString: string, timeString?: string): string {
  const date = formatSwissDate(dateString)
  return timeString ? `${date} ${timeString}` : date
}

export function formatDisplayDateTime(dateTimeString: string): string {
  const date = new Date(dateTimeString)

  if (Number.isNaN(date.getTime())) {
    return dateTimeString
  }

  return new Intl.DateTimeFormat('de-CH', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date)
}
