import { directory, page } from '../../router/menuDefinition'
export default [
  directory('knowledge', '知识库', null, 20, 'book'),
  directory('knowledge.practice', '刷题', 'knowledge', 24, 'book'),
  page('knowledge.projects', 'study-project-management', '项目管理', 'knowledge', 21, ['study.projects.manage'], () => import('./ProjectsView.vue')),
  page('knowledge.bases', 'study-knowledge', '知识库管理', 'knowledge', 22, ['study.knowledge.manage'], () => import('./KnowledgeView.vue')),
  page('knowledge.search', 'study-projects', '知识检索', 'knowledge', 23, ['study.search'], () => import('./ProjectsView.vue'), 'search'),
  page('knowledge.practice.exam', 'study-practice', '刷题与模拟考', 'knowledge.practice', 25, ['study.practice'], () => import('./ProjectsView.vue')),
  page('knowledge.practice.questions', 'study-questions', '题库管理', 'knowledge.practice', 26, ['study.questions.manage'], () => import('./QuestionsView.vue')),
  page('knowledge.practice.progress', 'study-progress', '错题与学习记录', 'knowledge.practice', 27, ['study.practice'], () => import('./ProjectsView.vue')),
  page('system.monitor.jobs', 'study-jobs', '处理任务', 'system.monitor', 103, ['study.jobs.view'], () => import('./JobsView.vue')),
]
