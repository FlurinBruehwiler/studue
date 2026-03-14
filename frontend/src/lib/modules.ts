export const MODULE_OPTIONS = [
  { value: 'AN2', label: 'Analysis 2' },
  { value: 'COMB', label: 'Communication Competence Basic' },
  { value: 'LA', label: 'Lineare Algebra' },
  { value: 'OTHER', label: 'Other' },
  { value: 'PROG2', label: 'Programmieren 2' },
  { value: 'PM2', label: 'Software-Projekt 2' },
  { value: 'THIN', label: 'Theoretische Informatik' },
] as const

export function getModuleLabel(moduleCode: string): string {
  return MODULE_OPTIONS.find((module) => module.value === moduleCode)?.label ?? moduleCode
}
