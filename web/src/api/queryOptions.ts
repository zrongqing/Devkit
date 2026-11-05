import type { FilterOption, TableFilterSchema } from '../components/tablePresentation'

// Values mirror the server HTTP contract. Labels are presentation only.
export const labels: Record<string, string> = {
  queued: "等待处理",
  running: "处理中",
  completed: "已完成",
  waiting: "待配置",
  failed: "失败",
  ready: "可检索",
  draft: "待审核",
  approved: "已审核",
  stale: "待复核",
  disabled: "已停用",
  ingest: "提取与关键词索引",
  dense: "语义增强",
  generate: "AI 出题",
  migrate: "文件迁移",
  cleanup: "清理旧副本",
  "remove-index": "移除索引",
  single: "单选",
  multiple: "多选",
  boolean: "判断",
  practice: "练习",
  exam: "模拟考试",
  active: "进行中",
  submitted: "已交卷",
  uploading: "上传中",
  purged: "已永久删除",
  purging: "待完成清理",
};
export const label = (value: string) => labels[value] ?? value;

const options = (...values: string[]): FilterOption[] => values.map(value => ({ value, label: label(value) }))
const booleanOptions = (yes: string, no: string): FilterOption[] => [{ value: 'true', label: yes }, { value: 'false', label: no }]
export const queryDictionaries = {
  menuType: [{ value: 'module', label: '模块' }, { value: 'directory', label: '目录' }],
  menuStatus: [{ value: 'new', label: '未同步' }, { value: 'synced', label: '已同步' }, { value: 'changed', label: '前端声明变更' }, { value: 'missing', label: '当前前端缺失' }, { value: 'manual', label: '后台自建目录' }],
  enabled: booleanOptions('已启用', '已停用'),
  grant: booleanOptions('已授权', '未授权'),
  access: booleanOptions('可访问', '未授权'),
  mastered: booleanOptions('已掌握', '未掌握'),
  questionType: options('single', 'multiple', 'boolean'),
  questionStatus: options('draft', 'approved', 'stale', 'disabled'),
  sourceStatus: options('queued', 'running', 'ready', 'failed', 'waiting'),
  jobStatus: options('queued', 'running', 'completed', 'waiting', 'failed'),
  studyJobKind: options('ingest', 'dense', 'generate', 'remove-index'),
  storageJobKind: options('migrate', 'cleanup'),
  fileStatus: options('ready', 'uploading', 'failed', 'purged', 'purging'),
  attemptStatus: options('active', 'submitted'),
  attemptMode: options('practice', 'exam'),
}
export function optionLabel(options: FilterOption[], value: string): string { return options.find(o => o.value === value)?.label ?? value }
const select = (options: FilterOption[]) => ({ type: 'select' as const, options })
const numberRange = { type: 'numberRange' as const }, dateRange = { type: 'dateRange' as const }
export const tableFilterSchemas = {
  menu: { type: select(queryDictionaries.menuType), enabled: select(queryDictionaries.enabled), order: numberRange },
  menuComparison: { status: select(queryDictionaries.menuStatus) },
  questions: { type: select(queryDictionaries.questionType), status: select(queryDictionaries.questionStatus) },
  sources: { status: select(queryDictionaries.sourceStatus), revision: numberRange },
  jobs: { kind: select(queryDictionaries.studyJobKind), status: select(queryDictionaries.jobStatus), createdAtUtc: dateRange, attempts: numberRange },
  storageJobs: { kind: select(queryDictionaries.storageJobKind), status: select(queryDictionaries.jobStatus), createdAtUtc: dateRange, attempts: numberRange },
  files: { status: select(queryDictionaries.fileStatus), length: numberRange, referenceCount: numberRange, createdAtUtc: dateRange },
  mistakes: { mastered: select(queryDictionaries.mastered), wrongCount: numberRange },
  attempts: { mode: select(queryDictionaries.attemptMode), status: select(queryDictionaries.attemptStatus), createdAtUtc: dateRange },
  moduleUsers: { directModuleGrant: select(queryDictionaries.grant), effectiveModuleGrant: select(queryDictionaries.access) },
  moduleRoles: { roleModuleGrant: select(queryDictionaries.grant) },
  audit: { occurredUtc: dateRange },
} satisfies Record<string, TableFilterSchema>
