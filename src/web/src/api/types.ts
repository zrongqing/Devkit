export interface ApiResponse<T> {
  data: T
  traceId: string
}

export interface SystemInfo {
  serviceName: string
  version: string
  environment: string
  serverTime: string
}

export interface UserProfile {
  id: string
  userName: string
  email: string
  roles: string[]
  createdAtUtc: string
}

export interface TokenPair {
  accessToken: string
  tokenType: 'Bearer'
  accessTokenExpiresAtUtc: string
  refreshToken: string
  refreshTokenExpiresAtUtc: string
  user: UserProfile
}
