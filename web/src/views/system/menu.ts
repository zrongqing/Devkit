import { directory, page } from '../../router/menuDefinition'
export default [
  page('home', 'home', '首页', null, 0, [], () => import('./HomeView.vue'), 'home'),
  directory('system.monitor', '系统监控', null, 100, 'system'),
  directory('system.management', '系统管理', null, 90, 'settings'),
  page('system.monitor.status', 'system-status', '运行状态', 'system.monitor', 101, ['system.monitor.view'], () => import('./SystemStatusView.vue'), 'pulse'),
  page('system.settings', 'settings', '基础参数', 'system.management', 93, [], () => import('./SettingsView.vue'), 'settings'),
  page('system.about', 'about', '关于 Web', 'system.management', 99, [], () => import('./AboutView.vue'), 'info'),
]
