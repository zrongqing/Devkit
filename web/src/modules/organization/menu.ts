import { page } from '../../router/menuDefinition'
export default [
  page('system.organization', 'system-organization', '组织架构', 'system.management', 93, ['system.organization.manage'], () => import('./OrganizationView.vue')),
  page('system.monitor.audit', 'system-audit', '管理操作审计', 'system.monitor', 2, ['system.audit.view'], () => import('./AuditView.vue')),
]
