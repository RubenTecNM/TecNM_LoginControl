import { http } from '../../../shared/api/http'

export interface Session {
  user: {
    id: string
    firstNames: string
    paternalLastName: string
    maternalLastName: string | null
    userType: { code: string; name: string }
  }
  expiresAt: string
}

export async function getSession(): Promise<Session> {
  const response = await http.get<Session>('/session', { withCredentials: true })
  return response.data
}

export async function logout(): Promise<void> {
  await http.post('/session/logout', undefined, { withCredentials: true })
}

export function login(provider: 'google' | 'microsoft'): void {
  window.location.assign(`${http.defaults.baseURL}/session/login/${provider}`)
}
