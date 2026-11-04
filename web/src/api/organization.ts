import { request } from './session'

export interface Department { id: string; name: string; parentId: string | null }
export interface Position { id: string; name: string; departmentId: string }
export interface Directory {
  departments: Department[]
  positions: Position[]
  users: { id: string; name: string; disabled: boolean }[]
  members: { positionId: string; userId: string }[]
}
export interface AuditEvent { id: number; occurredUtc: string; actorId: string; operation: string; traceId: string }

const root = '/api/v1/identity/organization'
export const directoryApi = {
  read: () => request<Directory>(root),
  saveDepartment: (id: string, name: string, parentId: string | null) =>
    request<string>(root + '/departments' + (id ? '/' + id : ''), {
      method: id ? 'PUT' : 'POST', body: JSON.stringify({ name, parentId }),
    }),
  savePosition: (id: string, name: string, departmentId: string) =>
    request<string>(root + '/positions' + (id ? '/' + id : ''), {
      method: id ? 'PUT' : 'POST', body: JSON.stringify({ name, departmentId }),
    }),
  remove: (type: 'departments' | 'positions', id: string) => request<void>(root + '/' + type + '/' + id, { method: 'DELETE' }),
  member: (positionId: string, userId: string, assigned: boolean) =>
    request<void>(root + '/positions/' + positionId + '/members/' + userId, { method: assigned ? 'PUT' : 'DELETE' }),
}
export const getAudit = (before?: number) => request<AuditEvent[]>('/api/v1/identity/audit' + (before ? '?before=' + before : ''))
