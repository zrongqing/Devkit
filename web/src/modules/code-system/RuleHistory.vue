<script setup lang="ts">
import DataTable from '../../components/DataTable'
import PageDialog from '../../components/PageDialog.vue'
import { tableQueries } from '../../api/tableQueries'
import { optionLabel, queryDictionaries } from '../../api/queryOptions'
import {
  codeApi,
  formula,
  resetLabels,
  type Version,
} from '../../api/codeSystem'
import type { TableFilterSchema } from '../../components/tablePresentation'
import { ref } from 'vue'
const props = defineProps<{
  id: string
  kind: 'versions' | 'events' | 'issues'
  revision: number
}>()
const selected = ref<Version>()
const error = ref('')
const emit = defineEmits<{ copy: [Version] }>()
const schema: TableFilterSchema = {
  versionNo: { type: 'numberRange' },
  revision: { type: 'numberRange' },
  sequenceValue: { type: 'numberRange' },
  action: { type: 'select', options: queryDictionaries.codeAction },
  occurredAtUtc: { type: 'dateRange' },
  issuedAtUtc: { type: 'dateRange' },
  publishedAtUtc: { type: 'dateRange' },
}
let sequence = 0
async function version(no: number) {
  const current = ++sequence
  error.value = ''
  try {
    const v = await codeApi.version(props.id, no)
    if (current === sequence) selected.value = v
  } catch (e) {
    if (current === sequence) error.value = (e as Error).message
  }
}
</script>
<template>
  <el-alert v-if="error" :title="error" type="error" />
  <DataTable
    :table-key="'code-' + kind"
    :query-context="id + revision"
    :filter-schema="schema"
    :load-page="tableQueries.codeHistory(id, kind)"
  >
    <el-table-column prop="versionNo" label="版本号" />
    <template v-if="kind === 'versions'">
      <el-table-column prop="publishedAtUtc" label="发布时间" /><el-table-column
        prop="publishedBy"
        label="发布人 ID"
      />
      <el-table-column label="操作"
        ><template #default="{ row }"
          ><el-button link @click="version(row.versionNo)"
            >查看版本</el-button
          ></template
        ></el-table-column
      >
    </template>
    <template v-else-if="kind === 'events'">
      <el-table-column prop="action" label="动作"
        ><template #default="{ row }">{{
          optionLabel(queryDictionaries.codeAction, row.action)
        }}</template></el-table-column
      >
      <el-table-column prop="revision" label="修订号" /><el-table-column
        prop="actorId"
        label="操作人 ID"
      /><el-table-column prop="occurredAtUtc" label="操作时间" />
      <el-table-column prop="reason" label="变更原因" />
    </template>
    <template v-else>
      <el-table-column prop="code" label="正式编码" /><el-table-column
        prop="businessId"
        label="业务对象 ID"
      /><el-table-column prop="periodKey" label="期间" />
      <el-table-column prop="sequenceValue" label="流水值" /><el-table-column
        prop="issuedBy"
        label="经办人 ID"
      /><el-table-column prop="issuedAtUtc" label="签发时间" />
    </template>
  </DataTable>
  <PageDialog
    :model-value="!!selected"
    title="发布版本"
    @update:model-value="selected = undefined"
  >
    <template v-if="selected"
      ><p>
        版本 {{ selected.version.versionNo }} ·
        {{ formula(selected.definition) }}
      </p>
      <p>
        {{ resetLabels[selected.definition.reset] }}，起值
        {{ selected.definition.start }}，步长 {{ selected.definition.step }}
      </p>
      <p>发布原因：{{ selected.reason }}</p>
      <slot
        name="copy"
        :copy="
          () => {
            emit('copy', selected!)
            selected = undefined
          }
        "
    /></template>
  </PageDialog>
</template>
