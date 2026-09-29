// Read compatibility for the deployment window before NormalizeWebMenuCodes runs.
// Only known legacy identities are translated; this never adds menus or grants.
const legacyCodes: Readonly<Record<string, string>> = {
  'study-project-management': 'knowledge.projects',
  'study-knowledge': 'knowledge.bases',
  'study-projects': 'knowledge.search',
  'study-practice': 'knowledge.practice.exam',
  'study-questions': 'knowledge.practice.questions',
  'study-progress': 'knowledge.practice.progress',
  'study-jobs': 'system.monitor.jobs',
  'system-users': 'system.identity.users',
  'system-roles': 'system.identity.roles',
  'system-identity': 'system.identity.permissions',
  'system-menus': 'system.menus',
  'system-files': 'system.files',
  'system-storage': 'system.storage',
  'system-status': 'system.monitor.status',
  settings: 'system.settings',
  about: 'system.about',
  'system-management': 'system.management',
  'system-management.users': 'system.identity',
  system: 'system.monitor',
}

export const canonicalMenuCode = (code: string): string =>
  Object.prototype.hasOwnProperty.call(legacyCodes, code) ? legacyCodes[code] : code
