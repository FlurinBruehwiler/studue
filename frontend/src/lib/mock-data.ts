import type { Assignment, AuthState } from '@/lib/types'

export const mockAssignments: Assignment[] = [
  {
    id: '2026-03-20--a1b2c3d4',
    className: 'it25ta_win',
    module: 'PM2',
    title: 'Project sketch submission',
    dueDate: '2026-03-20',
    dueTime: '15:00',
    note:
      'Bewertungsmatrix: https://moodle.zhaw.ch/2561979/ITPM2_Bewertung_Projektskizze.pdf\n\nAbgabe (pro Team): https://moodle.zhaw.ch/mod/assign/view.php?id=1921293',
    mandatory: true,
    createdBy: {
      githubLogin: 'jdoe',
      displayName: 'Jane Doe',
      email: 'jane.doe@students.zhaw.ch',
    },
    updatedBy: {
      githubLogin: 'jdoe',
      displayName: 'Jane Doe',
      email: 'jane.doe@students.zhaw.ch',
    },
    createdAt: '2026-03-10T18:42:00Z',
    updatedAt: '2026-03-10T18:42:00Z',
  },
  {
    id: '2026-03-24--f9e8d7c6',
    className: 'it25ta_win',
    module: 'THIN',
    title: 'Quiz 3: Endliche Automaten',
    dueDate: '2026-03-24',
    dueTime: '',
    note: 'Review the automata topics and prepare your notes before class.',
    mandatory: true,
    createdBy: {
      githubLogin: 'musteri',
      displayName: 'Max Muster',
      email: 'max.muster@students.zhaw.ch',
    },
    updatedBy: {
      githubLogin: 'musteri',
      displayName: 'Max Muster',
      email: 'max.muster@students.zhaw.ch',
    },
    createdAt: '2026-03-11T07:20:00Z',
    updatedAt: '2026-03-11T07:20:00Z',
  },
  {
    id: '2026-03-28--m4n5b6v7',
    className: 'it25ta_win',
    module: 'LA',
    title: 'Blatt 2, Aufgaben 7-15',
    dueDate: '2026-03-28',
    dueTime: '10:00',
    note: 'Solve the worksheet and bring your notes for the exercise discussion.',
    mandatory: false,
    createdBy: {
      githubLogin: 'cstudent',
      displayName: 'Chris Student',
      email: 'chris.student@students.zhaw.ch',
    },
    updatedBy: {
      githubLogin: 'cstudent',
      displayName: 'Chris Student',
      email: 'chris.student@students.zhaw.ch',
    },
    createdAt: '2026-03-11T11:00:00Z',
    updatedAt: '2026-03-12T08:30:00Z',
  },
]

export const mockUser: AuthState = {
  authenticated: false,
  user: null,
}
