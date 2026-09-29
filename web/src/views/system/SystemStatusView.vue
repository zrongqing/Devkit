<script setup lang="ts">
import SystemStatusCard from '../../components/SystemStatusCard.vue'
</script>

<template>
  <section class="page-shell">
    <div class="page-heading">
      <div><p class="page-kicker">运行监控</p><h2>系统监控</h2></div>
      <p>监控服务连接、运行时长、内存和线程状态，自动刷新运行指标。</p>
    </div>
    <SystemStatusCard />
  </section>
</template>
