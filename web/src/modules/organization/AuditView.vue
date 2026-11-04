<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { getAudit, type AuditEvent } from '../../api/organization'
import { date, useOperation } from '../exam-study/shared'
const rows = ref<AuditEvent[]>([])
const more = ref(false)
const { run, busy, error } = useOperation()
function load(append = false) {
  return run(async () => {
    const page = await getAudit(append ? rows.value[rows.value.length - 1]?.id : undefined)
    rows.value = append ? [...rows.value, ...page] : page
    more.value = page.length === 50
  })
}
onMounted(() => load())
</script>
<template>
  <section class="page-shell">
    <div class="page-heading"><div><p class="page-kicker">系统监控</p><h2>管理操作审计</h2></div>
      <el-button :loading="busy" @click="load()">刷新</el-button></div>
    <p>按时间倒序查看已保存的账号、授权、菜单和组织变更。请求标识可用于关联服务日志。</p>
    <el-alert v-if="error" :title="error" type="error" />
    <el-table :data="rows" v-loading="busy" empty-text="暂无管理操作">
      <el-table-column label="时间" width="180"><template #default="{row}">{{ date(row.occurredUtc) }}</template></el-table-column>
      <el-table-column prop="actorId" label="操作人 ID" min-width="180" />
      <el-table-column prop="operation" label="操作与资源" min-width="300" />
      <el-table-column prop="traceId" label="请求标识" min-width="160" />
    </el-table>
    <el-button v-if="more" :disabled="busy" @click="load(true)">加载更早记录</el-button>
  </section>
</template>
