<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { ElMessage } from "element-plus";
import {
  studyApi,
  type KnowledgeBase,
  type Question,
  type QuestionInput,
  type Source,
  type Citation,
  type QuestionOption,
} from "../../api/examStudy";
import { permissions } from "../../api/session";
import { label, parse, useOperation } from "./shared";
import CitationPanel from "./CitationPanel.vue";
const { run, busy, error } = useOperation();
const bases = ref<KnowledgeBase[]>([]);
const questions = ref<Question[]>([]);
const all = ref(false);
const kb = ref("");
const search = ref("");
const status = ref("");
const optionsOf = (value: string) => parse<QuestionOption[]>(value);
const visible = computed(() =>
  questions.value.filter(
    (q) =>
      (!kb.value || q.knowledgeBaseId === kb.value) &&
      (!status.value || q.status === status.value) &&
      q.stem.includes(search.value),
  ),
);
const dialog = ref(false);
const editing = ref<Question>();
const blank = (): QuestionInput => ({
  knowledgeBaseId: kb.value || bases.value[0]?.id || "",
  type: "single",
  stem: "",
  options: [
    { id: "A", text: "" },
    { id: "B", text: "" },
    { id: "C", text: "" },
    { id: "D", text: "" },
  ],
  answers: [],
  explanation: "",
  citations: [],
  tags: "",
  difficulty: "normal",
});
const form = ref<QuestionInput>(blank());
const sources = ref<Source[]>([]);
const citationSource = ref("");
const citationChoices = ref<Citation[]>([]);
const selectedCitations = ref<string[]>([]);
const generationDialog = ref(false);
const generation = ref({
  knowledgeBaseId: "",
  count: 10,
  type: "single",
  difficulty: "normal",
  tags: "",
});
async function load() {
  [bases.value, questions.value] = await Promise.all([
    studyApi.bases(all.value),
    studyApi.questions(undefined, all.value),
  ]);
}
async function loadCitationSources() {
  sources.value = form.value.knowledgeBaseId
    ? await studyApi.sources(form.value.knowledgeBaseId)
    : [];
  citationChoices.value = [];
  citationSource.value = "";
}
async function loadCitationChunks() {
  if (!citationSource.value) return;
  const d = await studyApi.source(citationSource.value);
  citationChoices.value = d.chunks
    .filter((c) => c.ordinal < 100000)
    .map((c) => ({
      chunkId: c.id,
      sourceId: c.sourceId,
      versionId: c.versionId,
      knowledgeBaseId: c.knowledgeBaseId,
      fileId: d.versions.find((v) => v.id === c.versionId)?.fileId ?? null,
      title: d.source.title,
      location: c.location,
      page: c.page,
      text: c.text,
    }));
}
async function edit(q?: Question) {
  editing.value = q;
  form.value = q
    ? {
        knowledgeBaseId: q.knowledgeBaseId,
        type: q.type,
        stem: q.stem,
        options: parse(q.optionsJson),
        answers: parse(q.answersJson),
        explanation: q.explanation,
        citations: parse(q.citationsJson),
        tags: q.tags,
        difficulty: q.difficulty,
        revision: q.revision,
      }
    : blank();
  selectedCitations.value = [];
  await loadCitationSources();
  dialog.value = true;
}
function typeChanged() {
  form.value.answers = [];
  if (form.value.type === "boolean")
    form.value.options = [
      { id: "A", text: "正确" },
      { id: "B", text: "错误" },
    ];
}
function answersText(value: string) {
  return parse<string[]>(value).join("、");
}
function addOption() {
  form.value.options.push({
    id: "ABCDEFGH"
      .split("")
      .find((id) => !form.value.options.some((o) => o.id === id))!,
    text: "",
  });
}
function addCitations() {
  const additions = citationChoices.value.filter((c) =>
    selectedCitations.value.includes(c.chunkId),
  );
  form.value.citations = [
    ...form.value.citations.filter(
      (c) => !additions.some((a) => a.chunkId === c.chunkId),
    ),
    ...additions,
  ];
  selectedCitations.value = [];
}
async function save() {
  await run(async () => {
    await studyApi.saveQuestion(form.value, editing.value?.id);
    dialog.value = false;
    await load();
    ElMessage.success("题目已保存为待审核。");
  });
}
function generate() {
  generation.value = {
    ...generation.value,
    knowledgeBaseId: kb.value || bases.value[0]?.id || "",
  };
  generationDialog.value = true;
}
async function startGeneration() {
  await run(async () => {
    await studyApi.generate(generation.value);
    generationDialog.value = false;
    ElMessage.success("出题任务已创建，可在处理任务中查看进度。");
  });
}
onMounted(() => {
  void run(load);
});
</script>
<template>
  <main class="study-page">
    <header class="study-heading">
      <div>
        <h2>题库管理</h2>
        <p>题目跟随知识库复用，审核后即可参与项目练习和模拟考试。</p>
      </div>
      <div class="study-actions">
        <el-switch
          v-if="permissions?.administrator"
          v-model="all"
          active-text="全部用户"
          @change="run(load)"
        /><el-button @click="generate">AI 辅助出题</el-button
        ><el-button type="primary" @click="run(() => edit())"
          >手动录题</el-button
        >
      </div>
    </header>
    <el-alert
      v-if="error"
      :title="error"
      type="error"
      class="study-error"
      :closable="false"
    />
    <div class="study-card" v-loading="busy">
      <div class="study-toolbar">
        <el-input
          v-model="search"
          placeholder="搜索题干"
          clearable
          style="width: 260px"
        /><el-select v-model="kb" clearable placeholder="全部知识库"
          ><el-option
            v-for="b in bases"
            :key="b.id"
            :label="b.name"
            :value="b.id" /></el-select
        ><el-select v-model="status" clearable placeholder="全部状态"
          ><el-option
            v-for="s in ['draft', 'approved', 'stale', 'disabled']"
            :key="s"
            :value="s"
            :label="label(s)" /></el-select
        ><el-button @click="run(load)">刷新</el-button>
      </div>
      <el-table :data="visible"
        ><el-table-column type="expand"
          ><template #default="{ row }"
            ><div style="padding: 16px 35px">
              <p v-for="o in optionsOf(row.optionsJson)" :key="o.id">
                {{ o.id }}. {{ o.text }}
              </p>
              <p><strong>答案：</strong>{{ answersText(row.answersJson) }}</p>
              <p class="study-text">{{ row.explanation }}</p>
              <CitationPanel
                :citations="parse(row.citationsJson)"
              /></div></template></el-table-column
        ><el-table-column label="题型" width="75"
          ><template #default="{ row }">{{
            label(row.type)
          }}</template></el-table-column
        ><el-table-column
          prop="stem"
          label="题干"
          min-width="260"
        /><el-table-column label="状态" width="110"
          ><template #default="{ row }"
            ><el-tag
              :type="
                row.status === 'approved'
                  ? 'success'
                  : row.status === 'stale'
                    ? 'warning'
                    : 'info'
              "
              >{{ label(row.status) }}</el-tag
            ></template
          ></el-table-column
        ><el-table-column label="操作" width="175"
          ><template #default="{ row }"
            ><el-button link type="primary" @click="run(() => edit(row))"
              >编辑</el-button
            ><el-button
              v-if="row.status !== 'approved'"
              link
              type="success"
              @click="
                run(async () => {
                  await studyApi.questionStatus(row.id, 'approved');
                  await load();
                })
              "
              >审核通过</el-button
            ><el-button
              v-else
              link
              @click="
                run(async () => {
                  await studyApi.questionStatus(row.id, 'disabled');
                  await load();
                })
              "
              >停用</el-button
            ></template
          ></el-table-column
        ></el-table
      >
    </div>
    <el-dialog
      v-model="dialog"
      :title="editing ? '编辑题目' : '手动录题'"
      width="min(840px,96vw)"
      append-to-body
      ><el-form label-position="top" class="study-form"
        ><div class="study-toolbar">
          <el-select
            v-model="form.knowledgeBaseId"
            :disabled="!!editing"
            placeholder="所属知识库"
            @change="run(loadCitationSources)"
            ><el-option
              v-for="b in bases"
              :key="b.id"
              :label="b.name"
              :value="b.id" /></el-select
          ><el-select v-model="form.type" @change="typeChanged"
            ><el-option
              v-for="t in ['single', 'multiple', 'boolean']"
              :key="t"
              :value="t"
              :label="label(t)"
          /></el-select>
        </div>
        <el-form-item label="题干"
          ><el-input
            v-model="form.stem"
            type="textarea"
            :rows="3"
            maxlength="4000" /></el-form-item
        ><el-form-item label="选项 · 勾选正确答案"
          ><div style="width: 100%">
            <div
              v-for="o in form.options"
              :key="o.id"
              class="study-option-editor"
            >
              <el-checkbox v-model="form.answers" :value="o.id">{{
                o.id
              }}</el-checkbox
              ><el-input v-model="o.text" /><el-button
                v-if="form.options.length > 2"
                text
                type="danger"
                @click="
                  form.options = form.options.filter((x) => x.id !== o.id);
                  form.answers = form.answers.filter((x) => x !== o.id);
                "
                >移除</el-button
              >
            </div>
            <el-button
              v-if="form.type !== 'boolean' && form.options.length < 8"
              text
              type="primary"
              @click="addOption"
              >添加选项</el-button
            >
          </div></el-form-item
        ><el-form-item label="答案解析"
          ><el-input v-model="form.explanation" type="textarea" :rows="3"
        /></el-form-item>
        <div class="study-toolbar">
          <el-input
            v-model="form.tags"
            placeholder="标签"
            style="width: 260px"
          /><el-select v-model="form.difficulty"
            ><el-option value="easy" label="简单" /><el-option
              value="normal"
              label="普通" /><el-option value="hard" label="较难"
          /></el-select>
        </div>
        <el-collapse
          ><el-collapse-item title="关联制度依据"
            ><div class="study-toolbar">
              <el-select
                v-model="citationSource"
                placeholder="选择资料"
                @change="run(loadCitationChunks)"
                ><el-option
                  v-for="s in sources"
                  :key="s.id"
                  :label="s.title"
                  :value="s.id"
              /></el-select>
            </div>
            <el-checkbox-group v-model="selectedCitations"
              ><el-checkbox
                v-for="c in citationChoices"
                :key="c.chunkId"
                :value="c.chunkId"
                style="
                  display: flex;
                  height: auto;
                  margin: 10px 0;
                  white-space: normal;
                "
                ><span style="white-space: normal"
                  >{{ c.location }} · {{ c.text.slice(0, 140) }}</span
                ></el-checkbox
              ></el-checkbox-group
            ><el-button v-if="selectedCitations.length" @click="addCitations"
              >加入引用</el-button
            ></el-collapse-item
          ></el-collapse
        >
        <div
          v-for="c in form.citations"
          :key="c.chunkId"
          class="study-toolbar"
          style="margin-top: 12px"
        >
          <span>{{ c.title }} · {{ c.location }}</span
          ><el-button
            text
            type="danger"
            @click="
              form.citations = form.citations.filter(
                (x) => x.chunkId !== c.chunkId,
              )
            "
            >移除引用</el-button
          >
        </div></el-form
      ><template #footer
        ><el-button @click="dialog = false">取消</el-button
        ><el-button type="primary" :loading="busy" @click="save"
          >保存待审核</el-button
        ></template
      ></el-dialog
    >
    <el-dialog
      v-model="generationDialog"
      title="AI 辅助出题"
      width="min(530px,94vw)"
      append-to-body
      ><el-form label-position="top"
        ><el-form-item label="知识库"
          ><el-select v-model="generation.knowledgeBaseId"
            ><el-option
              v-for="b in bases"
              :key="b.id"
              :label="b.name"
              :value="b.id" /></el-select></el-form-item
        ><el-form-item label="题型"
          ><el-select v-model="generation.type"
            ><el-option
              v-for="t in ['single', 'multiple', 'boolean']"
              :key="t"
              :value="t"
              :label="label(t)" /></el-select></el-form-item
        ><el-form-item label="数量"
          ><el-input-number
            v-model="generation.count"
            :min="1"
            :max="30" /></el-form-item
        ><el-form-item label="考点 / 标签"
          ><el-input
            v-model="generation.tags"
            placeholder="可填写希望重点考核的条款"
        /></el-form-item>
        <p class="study-muted">
          生成结果附带原文引用，经你审核后进入练习题池。
        </p></el-form
      ><template #footer
        ><el-button @click="generationDialog = false">取消</el-button
        ><el-button type="primary" :loading="busy" @click="startGeneration"
          >开始生成</el-button
        ></template
      ></el-dialog
    >
  </main>
</template>
