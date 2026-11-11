<script setup lang="ts">
import { ref } from 'vue'
const service = ref<'application' | 'business'>('application')
import SystemStatusCard from '../../components/SystemStatusCard.vue'
</script>

<template>
  <section class="page-shell">
    <el-tabs v-model="service"><el-tab-pane label="基础管理服务" name="application" /><el-tab-pane label="业务服务" name="business" /></el-tabs>
    <SystemStatusCard :key="service" :service="service" />
  </section>
</template>
