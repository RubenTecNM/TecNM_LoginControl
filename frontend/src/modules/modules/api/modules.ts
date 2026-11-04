import { http } from '../../../shared/api/http'
import type { Module, User } from './types'

const credentials = { withCredentials: true }
export const modulesApi = {
  async list() { return (await http.get<Module[]>('/modules', credentials)).data },
  async catalog() { return (await http.get<Module[]>('/admin/modules', credentials)).data },
  async launch(id: string) { return (await http.post<{ url: string }>(`/modules/${id}/launch`, undefined, credentials)).data },
  async create(body: Omit<Module, 'id'>) { return (await http.post<{ apiKey: string }>('/admin/modules', body, credentials)).data },
  async update(id: string, body: Omit<Module, 'id'>) { await http.patch(`/admin/modules/${id}`, body, credentials) },
  async rotate(id: string) { return (await http.post<{ apiKey: string }>(`/admin/modules/${id}/key`, undefined, credentials)).data },
  async users(search: string) { return (await http.get<User[]>('/admin/users', { ...credentials, params: { search } })).data },
  async userModules(id: string) { return (await http.get<Module[]>(`/admin/users/${id}/modules`, credentials)).data },
  async setUserModule(userId: string, moduleId: string, isActive: boolean) { await http.put(`/admin/users/${userId}/modules/${moduleId}`, { isActive }, credentials) },
  async moduleUsers(id: string) { return (await http.get<User[]>(`/admin/modules/${id}/users`, credentials)).data },
  async setModuleUsers(id: string, mode: 'selected' | 'all', userIds: string[], isActive: boolean) { return (await http.put<{ created: number; changed: number; unchanged: number }>(`/admin/modules/${id}/users`, { mode, userIds, isActive }, credentials)).data },
}
