export function formatDisplayDate(dateString) {
  const [year, month, day] = dateString.split('-')
  if (!year || !month || !day) {
    return dateString
  }

  return `${day}.${month}`
}

export function formatDisplayDateTime(dateTimeString) {
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
