<script setup lang="ts">
import SystemStatusCard from '../../components/SystemStatusCard.vue'
</script>

<template>
  <section class="page-shell">
    <div class="page-heading">
      <div><p class="page-kicker">运行监控</p><h2>系统状态</h2></div>
      <p>查看当前服务版本、运行环境和服务器时间。</p>
    </div>
    <SystemStatusCard />
  </section>
</template>
