<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";
import { useRoute } from "vue-router";
import { getSystemRuntime, type SystemRuntime } from "../api/system";
import { bytes } from "../modules/exam-study/shared";
const route = useRoute();
const runtime = ref<SystemRuntime>();
const error = ref("");
const loading = ref(false);
const lastChecked = ref("");
const latency = ref(0);
const automatic = ref(true);
const uptime = computed(() => {
  const seconds = Math.floor(runtime.value?.uptimeSeconds ?? 0);
  return `${Math.floor(seconds / 86400)} 天 ${Math.floor(seconds % 86400 / 3600)} 小时 ${Math.floor(seconds % 3600 / 60)} 分`;
});
let timer: ReturnType<typeof setInterval>;
let controller: AbortController | undefined;
async function refresh() {
  if (loading.value) return;
  loading.value = true;
  controller = new AbortController();
  const started = performance.now();
  try {
    runtime.value = await getSystemRuntime(controller.signal);
    latency.value = Math.round(performance.now() - started);
    error.value = "";
    lastChecked.value = new Date().toLocaleString("zh-CN");
  } catch (reason) {
    if (!(reason instanceof DOMException && reason.name === "AbortError"))
      error.value = reason instanceof Error ? reason.message : "无法连接服务端";
  } finally { loading.value = false; }
}
onMounted(() => {
  void refresh();
  timer = setInterval(() => {
    if (automatic.value && !document.hidden && route.params.routeKey === "system-status") void refresh();
  }, 30000);
});
onUnmounted(() => { clearInterval(timer); controller?.abort(); });
</script>
<template>
  <el-card class="status-card" shadow="never" v-loading="loading && !runtime">
    <template #header>
      <div class="card-heading monitor-heading">
        <div><strong>服务运行状态</strong><span>{{ lastChecked ? `最近成功采样：${lastChecked}` : '等待首次采样' }}</span></div>
        <div class="study-actions"><el-switch v-model="automatic" active-text="每 30 秒刷新" /><el-button type="primary" plain :loading="loading" @click="refresh">刷新</el-button></div>
      </div>
    </template>
    <el-alert v-if="error" :title="error" description="本次采样失败；下方保留的是最近一次成功采样，不能代表当前状态。" type="error" show-icon :closable="false" class="study-error" />
    <template v-if="runtime">
      <div class="study-grid">
        <div class="study-card"><div class="study-muted">服务连接</div><p><el-tag :type="error ? 'danger' : 'success'">{{ error ? '采样失败' : '在线' }}</el-tag></p><span class="study-muted">最近响应 {{ latency }} ms</span></div>
        <div class="study-card"><div class="study-muted">持续运行</div><p>{{ uptime }}</p><span class="study-muted">服务进程启动至采样时刻</span></div>
        <div class="study-card"><div class="study-muted">进程工作集</div><p class="study-stat">{{ bytes(runtime.workingSetBytes) }}</p><span class="study-muted">托管内存 {{ bytes(runtime.managedMemoryBytes) }}</span></div>
      </div>
      <el-descriptions :column="1" border>
        <el-descriptions-item label="服务">{{ runtime.info.serviceName }}</el-descriptions-item>
        <el-descriptions-item label="版本 / 环境">{{ runtime.info.version }} / {{ runtime.info.environment }}</el-descriptions-item>
        <el-descriptions-item label="服务时间">{{ new Date(runtime.info.serverTime).toLocaleString('zh-CN') }}</el-descriptions-item>
        <el-descriptions-item label="启动时间">{{ new Date(runtime.startedAtUtc).toLocaleString('zh-CN') }}</el-descriptions-item>
        <el-descriptions-item label="线程 / 可用处理器">{{ runtime.threadCount }} / {{ runtime.processorCount }}</el-descriptions-item>
        <el-descriptions-item label="进程累计 CPU 时间">{{ runtime.totalProcessorSeconds.toFixed(1) }} 秒</el-descriptions-item>
      </el-descriptions>
    </template>
  </el-card>
</template>
<style scoped>
.monitor-heading { gap: 16px; flex-wrap: wrap; }
</style>
