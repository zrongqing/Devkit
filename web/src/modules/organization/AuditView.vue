<script setup lang="ts">
import DataTable from "../../components/DataTable";
import { tableFilterSchemas } from "../../api/queryOptions";
import { tableQueries } from "../../api/tableQueries";
import { onMounted, ref } from 'vue'
import type { AuditEvent } from '../../api/organization'
import { date, useOperation } from '../exam-study/shared'
const rows = ref<AuditEvent[]>([])
const { run, busy, error } = useOperation()
function load() { rows.value = [] }
onMounted(() => load())

const tableValues = {
  occurredAt: ({ occurredUtc }: { occurredUtc: string }) => date(occurredUtc),
};
</script>
<template>
  <section class="page-shell">
    <p class="study-muted">请求标识可用于关联服务日志。</p>
    <el-alert v-if="error" :title="error" type="error" />
    <DataTable table-key="organization-audit" :filter-schema="tableFilterSchemas.audit" :load-page="tableQueries.audit" :data="rows" v-loading="busy" empty-text="暂无管理操作">
      <el-table-column :table-value="tableValues.occurredAt" prop="occurredUtc" label="时间" width="180"><template #default="{row}">{{ date(row.occurredUtc) }}</template></el-table-column>
      <el-table-column prop="actorId" label="操作人 ID" min-width="180" />
      <el-table-column prop="operation" label="操作与资源" min-width="300" />
      <el-table-column prop="traceId" label="请求标识" min-width="160" />
    </DataTable>
  </section>
</template>
