import { page } from '../../router/menuDefinition'
export default [page('system.menus', 'system-menus', '菜单管理', 'system.management', 92, ['system.menus.manage'], () => import('./MenuManagementView.vue'), 'menu')]
