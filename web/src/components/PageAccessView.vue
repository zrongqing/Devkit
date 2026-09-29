<script setup lang="ts">
import { computed } from 'vue'
import { getPageDefinition, resolvePageComponent } from '../router/pageRegistry'
import { hasPermission } from '../api/session'
const props = defineProps<{ routeKey: string }>()
const missing = computed(() => (getPageDefinition(props.routeKey)?.requiredPermissions ?? []).filter(p => !hasPermission(p)))
</script>
<template>
  <section v-if="missing.length" class="page-shell">
    <el-result icon="warning" title="已获得菜单访问权，尚缺少业务权限" sub-title="请联系管理员配置该页面所需的业务权限，配置后点击顶部刷新权限。" />
  </section>
  <component v-else :is="resolvePageComponent(routeKey)" :route-key="routeKey" />
</template>
