import type { ApiResponse, TokenPair, UserProfile } from './types'

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:12511').replace(/\/$/, '')

export async function register(
  userName: string,
  email: string,
  password: string,
  signal?: AbortSignal,
): Promise<UserProfile> {
  return send<UserProfile>('/api/v1/auth/register', {
    method: 'POST',
    body: JSON.stringify({ userName, email, password }),
    signal,
  })
}

export async function login(account: string, password: string, signal?: AbortSignal): Promise<TokenPair> {
  return send<TokenPair>('/api/v1/auth/login', {
    method: 'POST',
    body: JSON.stringify({ account, password }),
    signal,
  })
}

export async function refresh(refreshToken: string, signal?: AbortSignal): Promise<TokenPair> {
  return send<TokenPair>('/api/v1/auth/refresh', {
    method: 'POST',
    body: JSON.stringify({ refreshToken }),
    signal,
  })
}

export async function logout(refreshToken: string, signal?: AbortSignal): Promise<void> {
  await sendWithoutBody('/api/v1/auth/logout', {
    method: 'POST',
    body: JSON.stringify({ refreshToken }),
    signal,
  })
}

export async function logoutAll(accessToken: string, signal?: AbortSignal): Promise<void> {
  await sendWithoutBody('/api/v1/auth/logout-all', {
    method: 'POST',
    headers: bearer(accessToken),
    signal,
  })
}

export async function getCurrentUser(accessToken: string, signal?: AbortSignal): Promise<UserProfile> {
  return send<UserProfile>('/api/v1/auth/me', { headers: bearer(accessToken), signal })
}

function bearer(accessToken: string): HeadersInit {
  return { Authorization: `Bearer ${accessToken}` }
}

async function send<T>(path: string, init: RequestInit): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, withJsonHeaders(init))
  if (!response.ok) {
    throw new Error(`Authentication request failed (${response.status})`)
  }

  const body = (await response.json()) as ApiResponse<T>
  return body.data
}

async function sendWithoutBody(path: string, init: RequestInit): Promise<void> {
  const response = await fetch(`${baseUrl}${path}`, withJsonHeaders(init))
  if (!response.ok) {
    throw new Error(`Authentication request failed (${response.status})`)
  }
}

function withJsonHeaders(init: RequestInit): RequestInit {
  return {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init.headers,
    },
  }
}
