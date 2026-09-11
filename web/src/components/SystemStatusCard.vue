<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { getSystemInfo } from '../api/system'
import type { SystemInfo } from '../api/types'

const info = ref<SystemInfo>()
const error = ref<string>()
const loading = ref(false)

async function refresh() {
  loading.value = true
  error.value = undefined
  try {
    info.value = await getSystemInfo()
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'Unable to reach the server.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <section class="status-card">
    <div class="card-heading"><h2>系统状态</h2><button :disabled="loading" @click="refresh">刷新</button></div>
    <p v-if="loading">正在读取服务状态…</p>
    <p v-else-if="error" class="error">{{ error }}</p>
    <dl v-else-if="info">
      <dt>服务</dt><dd>{{ info.serviceName }}</dd>
      <dt>版本</dt><dd>{{ info.version }}</dd>
      <dt>环境</dt><dd>{{ info.environment }}</dd>
      <dt>服务时间</dt><dd>{{ new Date(info.serverTime).toLocaleString() }}</dd>
    </dl>
  </section>
</template>
