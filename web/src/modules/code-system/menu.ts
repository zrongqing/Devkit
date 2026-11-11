import { page } from '../../router/menuDefinition'
export default [
  page(
    'system.coderules',
    'system-code-rules',
    '编码规则',
    'system.management',
    96,
    ['code.rules.view'],
    () => import('./CodeRulesView.vue'),
  ),
]
