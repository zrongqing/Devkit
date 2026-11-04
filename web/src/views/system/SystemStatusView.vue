<script setup lang="ts">
import { ref } from 'vue'
const service = ref<'application' | 'business'>('application')
import SystemStatusCard from '../../components/SystemStatusCard.vue'
</script>

<template>
  <section class="page-shell">
    <div class="page-heading">
      <div><p class="page-kicker">运行监控</p><h2>系统监控</h2></div>
      <p>监控服务连接、运行时长、内存和线程状态，自动刷新运行指标。</p>
    </div>
    <el-tabs v-model="service"><el-tab-pane label="基础管理服务" name="application" /><el-tab-pane label="业务服务" name="business" /></el-tabs>
    <SystemStatusCard :key="service" :service="service" />
  </section>
</template>
