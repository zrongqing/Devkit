<template>
  <section class="page-shell">
    <div class="page-heading">
      <div><p class="page-kicker">Devkit</p><h2>关于 Web</h2></div>
      <p>Vue 3、TypeScript、Vite 与 Element Plus 构建的模块化前端基础框架。</p>
    </div>
    <el-descriptions :column="1" border>
      <el-descriptions-item label="菜单接口">/api/v1/web/navigation/menus</el-descriptions-item>
      <el-descriptions-item label="路由模式">Hash</el-descriptions-item>
      <el-descriptions-item label="页面注册">本地 routeKey 白名单</el-descriptions-item>
      <el-descriptions-item label="页签恢复">首页与当前路由</el-descriptions-item>
    </el-descriptions>
  </section>
</template>
