<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import DataTable from '../../components/DataTable'
import PageDialog from '../../components/PageDialog.vue'
import DefinitionEditor from './DefinitionEditor.vue'
import RuleHistory from './RuleHistory.vue'
import {
  blankDefinition,
  canCode,
  codeApi,
  formula,
  resetLabels,
  segmentInputError,
  type Definition,
  type Detail,
  type Scenario,
  type Version,
} from '../../api/codeSystem'
import { tableQueries } from '../../api/tableQueries'
import { optionLabel, queryDictionaries } from '../../api/queryOptions'
import type { TableFilterSchema } from '../../components/tablePresentation'
import { formatDisplayTime } from '../../stores/systemSettings'

const scenarios = ref<Scenario[]>([]),
  loaded = ref(false),
  listError = ref(''),
  refresh = ref(0)
const controller = new AbortController()
async function loadScenarios() {
  listError.value = ''
  try {
    scenarios.value = await codeApi.scenarios(controller.signal)
    loaded.value = true
  } catch (e) {
    if (!controller.signal.aborted) listError.value = (e as Error).message
  }
}
onMounted(loadScenarios)
onUnmounted(() => controller.abort())
const editable = computed(() =>
  scenarios.value.filter((s) => canCode(s.moduleKey, 'edit')),
)
const schema = computed<TableFilterSchema>(() => ({
  status: { type: 'select', options: queryDictionaries.codeStatus },
  moduleKey: {
    type: 'select',
    options: [...new Set(scenarios.value.map((s) => s.moduleKey))].map(
      (value) => ({ value, label: value }),
    ),
  },
  activeVersionNo: { type: 'numberRange' },
  updatedAtUtc: { type: 'dateRange' },
}))
const open = ref(false),
  detail = ref<Detail>(),
  tab = ref('rule'),
  error = ref(''),
  busy = ref(false),
  key = ref(''),
  name = ref('')
const targetId = ref('')
const working = ref<Definition>(blankDefinition()),
  reason = ref(''),
  sampleTime = ref(new Date()),
  sampleValue = ref(1),
  example = ref('')
const moduleKey = computed(
  () =>
    detail.value?.rule.moduleKey ??
    scenarios.value.find((s) => s.ruleKey === key.value)?.moduleKey ??
    '',
)
const edit = computed(() => canCode(moduleKey.value, 'edit')),
  publish = computed(() => canCode(moduleKey.value, 'publish'))
const source = computed(
  () => detail.value?.draft ?? detail.value?.active ?? blankDefinition(),
)
const dirty = computed(
  () => JSON.stringify(working.value) !== JSON.stringify(source.value),
)
const segmentErrors = computed(() => working.value.segments.map(segmentInputError))
const hasSegmentErrors = computed(() => segmentErrors.value.some(Boolean))
watch(
  [working, sampleTime, sampleValue, key],
  () => {
    example.value = ''
  },
  { deep: true },
)
function copyDefinition(d: Definition) {
  working.value = JSON.parse(JSON.stringify(d))
}
function copyVersion(v: Version) {
  copyDefinition(v.definition)
  tab.value = 'rule'
}
async function run(fn: () => Promise<void>) {
  if (busy.value) return
  busy.value = true
  error.value = ''
  try {
    await fn()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
async function reload(id: string, preserve = false) {
  const next = await codeApi.detail(id)
  targetId.value = id
  detail.value = next
  key.value = next.rule.ruleKey
  name.value = next.rule.name
  if (!preserve) copyDefinition(source.value)
}
function show(id?: string) {
  targetId.value = id ?? ''
  error.value = ''
  example.value = ''
  reason.value = ''
  detail.value = undefined
  tab.value = 'rule'
  key.value = editable.value[0]?.ruleKey ?? ''
  name.value = ''
  working.value = blankDefinition()
  open.value = true
  if (id) void run(() => reload(id))
}
async function save() {
  if (detail.value) {
    await codeApi.save(
      detail.value.rule.id,
      detail.value.revision,
      working.value,
    )
    await reload(detail.value.rule.id)
  } else {
    const r = await codeApi.create(key.value, name.value, working.value)
    targetId.value = r.id
    await reload(r.id)
  }
  refresh.value++
  ElMessage.success('草稿已保存')
}
async function transition(action: 'publish' | 'enable' | 'disable') {
  const current = detail.value!
  await codeApi.transition(
    current.rule.id,
    action,
    current.revision,
    reason.value.trim(),
  )
  await reload(current.rule.id)
  reason.value = ''
  refresh.value++
  ElMessage.success('规则已更新')
}
async function preview() {
  if (!sampleTime.value) throw new Error('请选择样例时刻。')
  example.value = (
    await codeApi.preview(
      key.value,
      working.value,
      sampleTime.value.toISOString(),
      sampleValue.value,
    )
  ).code
}
function description(d: Definition | null) {
  return d
    ? formula(d) +
        ' · ' +
        resetLabels[d.reset] +
        ' · 起值 ' +
        d.start +
        ' · 步长 ' +
        d.step
    : '暂无'
}
</script>
<template>
  <section class="page-shell">
    <el-alert v-if="listError" type="error" :closable="false"
      >{{ listError }}
      <el-button link @click="loadScenarios">重试</el-button></el-alert
    >
    <el-alert
      v-if="loaded && !scenarios.length"
      type="info"
      :closable="false"
      title="暂无可用场景。请在 Business 的 CodeSystem:Scenarios 登记场景，并核对模块读取权限。"
    />
    <DataTable
      table-key="code-rules"
      :query-context="String(refresh)"
      :load-page="tableQueries.codeRules"
      :filter-schema="schema"
      search-field="ruleKey"
    >
      <template #actions
        ><el-button v-if="editable.length" type="primary" @click="show()"
          >新建规则</el-button
        ></template
      >
      <el-table-column prop="ruleKey" label="规则键" /><el-table-column
        prop="name"
        label="业务名称"
      /><el-table-column prop="moduleKey" label="归属模块" />
      <el-table-column prop="status" label="状态"
        ><template #default="{ row }">{{
          optionLabel(queryDictionaries.codeStatus, row.status)
        }}</template></el-table-column
      >
      <el-table-column
        prop="activeVersionNo"
        label="生效版本"
      /><el-table-column prop="updatedAtUtc" label="最后变更时间" min-width="180">
        <template #default="{ row }">{{ formatDisplayTime(row.updatedAtUtc) }}</template>
      </el-table-column>
      <el-table-column label="操作"
        ><template #default="{ row }"
          ><el-button link type="primary" @click="show(row.id)"
            >查看规则</el-button
          ></template
        ></el-table-column
      >
    </DataTable>
    <PageDialog
      v-model="open"
      :title="targetId ? name || '编码规则详情' : '新建编码规则'"
      :width="860"
      :closable="!busy"
      :close-on-click-mask="false"
    >
      <el-alert v-if="error" type="error" :closable="false"
        >{{ error }}
        <el-button
          v-if="detail"
          link
          @click="run(() => reload(detail!.rule.id, true))"
          >刷新版本并保留输入</el-button
        >
        <el-button
          v-else-if="targetId"
          link
          @click="run(() => reload(targetId))"
          >重试读取详情</el-button
        >
      </el-alert>
      <el-tabs v-if="!targetId || detail" v-model="tab" v-loading="busy">
        <el-tab-pane name="rule" label="规则定义">
          <el-form v-if="!detail" label-position="top">
            <el-form-item label="场景"
              ><el-select v-model="key"
                ><el-option
                  v-for="s in editable"
                  :key="s.ruleKey"
                  :value="s.ruleKey"
                  :label="s.objectType + ' · ' + s.ruleKey" /></el-select
            ></el-form-item>
            <el-form-item label="业务名称"
              ><el-input v-model="name" maxlength="100"
            /></el-form-item>
          </el-form>
          <template v-else
            ><p>
              {{ key }} · 修订 {{ detail.revision }} ·
              {{
                optionLabel(queryDictionaries.codeStatus, detail.rule.status)
              }}
            </p>
            <dl>
              <dt>
                当前生效{{
                  detail.rule.activeVersionNo
                    ? ' · v' + detail.rule.activeVersionNo
                    : ''
                }}
              </dt>
              <dd>{{ description(detail.active) }}</dd>
              <dt>待发布草稿</dt>
              <dd>{{ description(detail.draft) }}</dd>
            </dl></template
          >
          <DefinitionEditor
            v-if="edit"
            v-model="working"
            :locked-policy="!!detail?.active"
            :segment-errors="segmentErrors"
          />
          <div class="operation">
            <el-date-picker
              v-model="sampleTime"
              type="datetime"
              aria-label="样例时刻"
            /><el-input-number
              v-model="sampleValue"
              :min="1"
              :precision="0"
              aria-label="样例序号"
            />
            <el-button :disabled="!key || hasSegmentErrors" @click="run(preview)"
              >预览编码</el-button
            ><output v-if="example">{{ example }}</output>
          </div>
          <el-button
            v-if="edit"
            type="primary"
            :disabled="
              !key || !name.trim() || hasSegmentErrors || (!!detail && !dirty && !!detail.draft)
            "
            @click="run(save)"
            >保存草稿</el-button
          >
          <section v-if="detail && publish" class="publish">
            <h3>发布与状态</h3>
            <el-input
              v-model="reason"
              placeholder="填写变更原因"
              maxlength="256"
              show-word-limit
            />
            <div class="operation">
              <el-button
                v-if="detail.draft"
                type="primary"
                :disabled="dirty || !reason.trim()"
                @click="run(() => transition('publish'))"
                >发布草稿</el-button
              >
              <el-button
                v-if="detail.rule.status === 'enabled'"
                :disabled="dirty || !reason.trim()"
                @click="run(() => transition('disable'))"
                >停用规则</el-button
              >
              <el-button
                v-if="detail.rule.status === 'disabled'"
                :disabled="dirty || !reason.trim()"
                @click="run(() => transition('enable'))"
                >恢复规则</el-button
              >
            </div>
          </section>
        </el-tab-pane>
        <template v-if="detail">
          <el-tab-pane name="versions" label="历史版本"
            ><RuleHistory
              v-if="tab === 'versions'"
              :id="detail.rule.id"
              kind="versions"
              :revision="detail.revision"
              @copy="copyVersion"
            >
              <template v-if="edit" #copy="{ copy }"
                ><el-button @click="copy">复制到编辑器</el-button></template
              ></RuleHistory
            ></el-tab-pane
          >
          <template v-if="canCode(moduleKey, 'audit')">
            <el-tab-pane name="events" label="变更记录"
              ><RuleHistory
                v-if="tab === 'events'"
                :id="detail.rule.id"
                kind="events"
                :revision="detail.revision"
            /></el-tab-pane>
            <el-tab-pane name="issues" label="签发流水"
              ><RuleHistory
                v-if="tab === 'issues'"
                :id="detail.rule.id"
                kind="issues"
                :revision="detail.revision"
            /></el-tab-pane>
          </template>
        </template>
      </el-tabs>
    </PageDialog>
  </section>
</template>
<style scoped>
.operation {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  margin: 16px 0;
}
.operation output {
  font-family: monospace;
  overflow-wrap: anywhere;
}
.publish {
  margin-top: 24px;
  border-top: 1px solid var(--el-border-color-light);
}
dt {
  color: var(--el-text-color-secondary);
}
dd {
  margin: 4px 0 12px;
  overflow-wrap: anywhere;
}
</style>
