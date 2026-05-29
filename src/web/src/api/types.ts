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
