import { page } from '../../router/menuDefinition'
export default [
  page('system.files', 'system-files', '文件管理', 'system.management', 94, ['system.files.manage'], () => import('./FilesView.vue')),
  page('system.storage', 'system-storage', '存储与迁移', 'system.management', 95, ['system.storage.manage'], () => import('./StorageView.vue')),
]
