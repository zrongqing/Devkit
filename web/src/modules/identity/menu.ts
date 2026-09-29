import { directory, page } from '../../router/menuDefinition'
export default [
  directory('system.identity', '用户管理', 'system.management', 91, 'users'),
  page('system.identity.users', 'system-users', '用户管理', 'system.identity', 1, ['system.users.manage'], () => import('./IdentityView.vue')),
  page('system.identity.roles', 'system-roles', '角色管理', 'system.identity', 2, ['system.roles.manage'], () => import('./IdentityView.vue')),
  page('system.identity.permissions', 'system-identity', '权限管理', 'system.identity', 3, ['system.permissions.manage'], () => import('./IdentityView.vue')),
]
