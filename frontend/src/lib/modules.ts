export const MODULE_OPTIONS = [
  { value: 'AN2', label: 'Analysis 2 (AN2)' },
  { value: 'COMB', label: 'Communication Competence Basic (COMB)' },
  { value: 'LA', label: 'Lineare Algebra (LA)' },
  { value: 'PROG2', label: 'Programmieren2 (PROG2)' },
  { value: 'PM2', label: 'Software-Projekt 2 (PM2)' },
  { value: 'THIN', label: 'Theoretische Informatik (THIN)' },
] as const

export function getModuleLabel(moduleCode: string): string {
  return MODULE_OPTIONS.find((module) => module.value === moduleCode)?.label ?? moduleCode
}
