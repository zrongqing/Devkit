<script setup lang="ts">
import { computed, provide, ref, useTemplateRef } from 'vue'
import { getPageDefinition, resolvePageComponent } from '../router/pageRegistry'
import { hasPermission } from '../api/session'
import { pageDialogContext } from './pageDialogContext'
const props = defineProps<{ routeKey: string }>()
const missing = computed(() => (getPageDefinition(props.routeKey)?.requiredPermissions ?? []).filter(p => !hasPermission(p)))
const dialogs = ref<string[]>([])
provide(pageDialogContext, { host: useTemplateRef<HTMLElement>('dialogHost'), dialogs })
</script>
<template>
  <div class="workspace-page-scroll" :inert="dialogs.length > 0">
    <div class="workspace-page-content">
      <section v-if="missing.length" class="page-shell">
        <el-result icon="warning" title="已获得菜单访问权，尚缺少业务权限" sub-title="请联系管理员配置该页面所需的业务权限，配置后点击顶部刷新权限。" />
      </section>
      <component v-else :is="resolvePageComponent(routeKey)" :route-key="routeKey" />
    </div>
  </div>
  <div ref="dialogHost" class="workspace-page-dialogs" />
</template>
