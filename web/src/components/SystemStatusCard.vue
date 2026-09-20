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
  <el-card class="status-card" shadow="never">
    <template #header>
      <div class="card-heading">
        <div><strong>服务连接</strong><span>实时读取服务端系统信息</span></div>
        <el-button type="primary" plain :loading="loading" @click="refresh">刷新</el-button>
      </div>
    </template>
    <el-skeleton v-if="loading" :rows="4" animated />
    <el-alert v-else-if="error" :title="error" type="error" show-icon :closable="false" />
    <el-descriptions v-else-if="info" :column="1" border>
      <el-descriptions-item label="服务">{{ info.serviceName }}</el-descriptions-item>
      <el-descriptions-item label="版本">{{ info.version }}</el-descriptions-item>
      <el-descriptions-item label="环境">{{ info.environment }}</el-descriptions-item>
      <el-descriptions-item label="服务时间">{{ new Date(info.serverTime).toLocaleString() }}</el-descriptions-item>
    </el-descriptions>
  </el-card>
</template>
