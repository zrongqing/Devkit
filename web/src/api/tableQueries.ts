import { request } from './session'
import type { MenuDeclaration } from '../types/navigation'

export interface FieldFilter { field: string; value: string; operator?: 'contains' | 'eq' | 'gte' | 'lte' }
export interface FieldSort { field: string; direction: 'asc' | 'desc' }
export interface PageQuery { page: number; pageSize: number; filters: FieldFilter[]; sorts: FieldSort[] }
export interface PageResult<T = Record<string, any>> { items: T[]; total: number; page: number; pageSize: number }
export type PageLoader = (query: PageQuery, signal: AbortSignal) => Promise<PageResult>
const query = (path: string, body: unknown, signal: AbortSignal) => request<PageResult>(path, { method: 'POST', body: JSON.stringify(body), signal })
const study = '/api/v1/exam-study', identity = '/api/v1/identity', menus = '/api/v1/web/menu-management'
export const tableQueries = {
  projects: (all: boolean): PageLoader => (q, signal) => query(`${study}/projects/query?all=${all}`, q, signal),
  sources: (id: string): PageLoader => (q, signal) => query(`${study}/knowledge-bases/${id}/sources/query`, q, signal),
  questions: (all: boolean, kb: string, search: string, status: string): PageLoader => (q, signal) => query(`${study}/questions/query?all=${all}${kb ? `&knowledgeBaseId=${encodeURIComponent(kb)}` : ''}`, {
    ...q, filters: [...q.filters, ...(search ? [{ field: 'stem', value: search }] : []), ...(status ? [{ field: 'status', value: status }] : [])],
  }, signal),
  jobs: ((q, signal) => query(`${study}/jobs/query`, q, signal)) as PageLoader,
  mistakes: (id: string): PageLoader => (q, signal) => query(`${study}/projects/${id}/mistakes/query`, q, signal),
  attempts: (id: string): PageLoader => (q, signal) => query(`${study}/projects/${id}/attempts/query`, q, signal),
  files: (search: string, unreferenced: boolean): PageLoader => (q, signal) => query('/api/v1/files/query', { ...q,
    filters: [...q.filters, ...(search ? [{ field: 'name', value: search }] : []), ...(unreferenced ? [{ field: 'referenceCount', value: '0', operator: 'eq' }] : [])],
  }, signal),
  references: (id: string): PageLoader => (q, signal) => query(`/api/v1/files/${id}/references/query`, q, signal),
  storageJobs: ((q, signal) => query('/api/v1/storage/jobs/query', q, signal)) as PageLoader,
  users: (module = ''): PageLoader => (q, signal) => query(`${identity}/users/query?module=${encodeURIComponent(module)}`, q, signal),
  roles: (module = ''): PageLoader => (q, signal) => query(`${identity}/roles/query?module=${encodeURIComponent(module)}`, q, signal),
  disabledUsers: ((q, signal) => query(`${identity}/users/disabled/query`, q, signal)) as PageLoader,
  positions: (id: string): PageLoader => (q, signal) => query(`${identity}/organization/departments/${id}/positions/query`, q, signal),
  audit: ((q, signal) => query(`${identity}/audit/query`, q, signal)) as PageLoader,
  menuTree: (async (q, signal) => {
    const page = await query(`${menus}/menus/query`, q, signal)
    const nodes = new Map<string, Record<string, any>>(page.items.map(row => [row.menuCode, { ...row, children: [] as Record<string, any>[] }]))
    const roots: Record<string, any>[] = []
    for (const node of nodes.values()) {
      const parent = node.parentCode ? nodes.get(node.parentCode) : undefined
      if (parent) parent.children.push(node); else roots.push(node)
    }
    return { ...page, items: roots }
  }) as PageLoader,
  menuDiffs: (declarations: MenuDeclaration[], status: string): PageLoader => (q, signal) => query(`${menus}/compare/query`, {
    menus: declarations, query: { ...q, filters: [...q.filters, ...(status ? [{ field: 'status', value: status }] : [])] },
  }, signal),
  menuFields: (declarations: MenuDeclaration[], code: string): PageLoader => (q, signal) => query(`${menus}/fields/query`, { menus: declarations, menuCode: code, query: q }, signal),
}
