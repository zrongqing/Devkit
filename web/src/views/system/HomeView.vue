<script setup lang="ts">
import { Connection, Menu, Tickets } from '@element-plus/icons-vue'
</script>

<template>
  <section class="page-shell home-view">
    <div class="page-hero">
      <p class="page-kicker">工作台</p>
      <h2>你好，欢迎回来</h2>
      <p>从左侧菜单进入业务模块；同一个菜单始终复用同一个页签。</p>
    </div>
    <div class="feature-grid">
      <article class="feature-card">
        <el-icon><Menu /></el-icon>
        <div><strong>递归菜单</strong><p>服务端配置任意层级，客户端负责安全映射。</p></div>
      </article>
      <article class="feature-card">
        <el-icon><Tickets /></el-icon>
        <div><strong>页签工作区</strong><p>保留首页，并在模块间快速切换。</p></div>
      </article>
      <article class="feature-card">
        <el-icon><Connection /></el-icon>
        <div><strong>分端接口</strong><p>Web 与 Desktop 独立演进，共享服务端逻辑。</p></div>
      </article>
    </div>
  </section>
</template>
