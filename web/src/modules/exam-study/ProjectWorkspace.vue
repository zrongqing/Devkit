<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";
import { onBeforeRouteLeave, useRoute, useRouter } from "vue-router";
import { ElMessage, ElMessageBox } from "element-plus";
import {
  studyApi,
  type Project,
  type KnowledgeBase,
  type SearchHit,
  type History,
  type Attempt,
  type Question,
  type Progress,
  type Citation,
} from "../../api/examStudy";
import { label, date, parse, useOperation } from "./shared";
import CitationPanel from "./CitationPanel.vue";
const props = defineProps<{ project: Project; bases: KnowledgeBase[] }>();
const route = useRoute();
const router = useRouter();
const { run, busy, error } = useOperation();
const tab = ref("search");
const query = ref("");
const lastQuery = ref("");
const mode = ref("keyword");
const kb = ref("");
const results = ref<SearchHit[]>([]);
const answer = ref<History>();
const history = ref<History[]>([]);
const searched = ref(false);
const offset = ref(0);
const searchInput = ref<{ focus: () => void }>();
const searching = ref(false);
const explaining = ref(false);
let searchController: AbortController | undefined;
let explainController: AbortController | undefined;
let searchSequence = 0;
const linked = computed(() =>
  props.bases.filter((x) =>
    parse<string[]>(props.project.knowledgeBaseIdsJson).includes(x.id),
  ),
);
const capabilities = ref({
  chatConfigured: false,
  embeddingConfigured: false,
  qdrantAvailable: false,
});
const progress = ref<Progress>({
  attempts: [],
  mistakes: [],
  answered: 0,
  correct: 0,
});
const questionNames = ref<Record<string, string>>({});
const attemptDialog = ref(false);
const attemptForm = ref({
  mode: "practice",
  count: 20,
  minutes: 30,
  passScore: 60,
  selection: "random",
  types: ["single", "multiple", "boolean"],
});
const attempt = ref<Attempt>();
const drafts = ref<Record<string, string[]>>({});
const saving = ref(false);
const remaining = ref("");
const saveMessage = ref("");
let clock: ReturnType<typeof setInterval> | undefined;
let submitStarted = false;
let serverOffset = 0;
let pending: Promise<void> = Promise.resolve();
const timers = new Map<string, ReturnType<typeof setTimeout>>();
const failedSaves = new Set<string>();
let lastScope = "";
const cacheKey = (id: string) => `study-drafts:${id}`;
function persistDrafts() {
  if (attempt.value?.status === "active") {
    const unsaved = Object.fromEntries(
      attempt.value.questions
        .filter((q) => !(attempt.value!.mode === "practice" && q.answered))
        .filter(
          (q) =>
            JSON.stringify([...(drafts.value[q.id] ?? [])].sort()) !==
            JSON.stringify([...q.selected].sort()),
        )
        .map((q) => [q.id, drafts.value[q.id] ?? []]),
    );
    sessionStorage.setItem(cacheKey(attempt.value.id), JSON.stringify(unsaved));
  }
}
function beforeUnload(event: BeforeUnloadEvent) {
  if (timers.size || saving.value || failedSaves.size) {
    event.preventDefault();
    event.returnValue = "";
  }
}
const completed = computed(
  () => attempt.value?.questions.filter((x) => x.answered).length ?? 0,
);
async function search(more = false) {
  if (!query.value.trim()) return;
  searchController?.abort();
  searchController = new AbortController();
  const sequence = ++searchSequence;
  searching.value = true;
  error.value = "";
  answer.value = undefined;
  const scope = JSON.stringify([query.value, mode.value, kb.value]);
  more = more && scope === lastScope;
  const nextOffset = more ? offset.value + 10 : 0;
  try {
    const response = await studyApi.search(
      props.project.id,
      {
        query: query.value,
        mode: mode.value,
        knowledgeBaseId: kb.value || undefined,
        offset: nextOffset,
      },
      searchController.signal,
    );
    if (sequence !== searchSequence) return;
    results.value = more
      ? [...results.value, ...response.items]
      : response.items;
    offset.value = nextOffset;
    lastScope = scope;
    lastQuery.value = query.value;
    searched.value = true;
    const key = `study-recent:${props.project.id}`;
    const previous = parse<string[]>(sessionStorage.getItem(key) ?? "[]");
    sessionStorage.setItem(
      key,
      JSON.stringify(
        [query.value, ...previous.filter((x) => x !== query.value)].slice(0, 8),
      ),
    );
    recent.value = parse(sessionStorage.getItem(key)!);
  } catch (e) {
    if (e instanceof DOMException && e.name === "AbortError") return;
    if (sequence === searchSequence)
      error.value = e instanceof Error ? e.message : "查询失败";
  } finally {
    if (sequence === searchSequence) searching.value = false;
  }
}
const recent = ref<string[]>([]);
async function explain() {
  if (!query.value.trim()) return;
  explainController?.abort();
  explainController = new AbortController();
  explaining.value = true;
  try {
    answer.value = await studyApi.ask(
      props.project.id,
      {
        query: query.value,
        mode: mode.value,
        knowledgeBaseId: kb.value || undefined,
      },
      explainController.signal,
    );
    history.value = await studyApi.history(props.project.id);
  } catch (e) {
    if (!(e instanceof DOMException && e.name === "AbortError"))
      error.value = e instanceof Error ? e.message : "问答失败";
  } finally {
    explaining.value = false;
  }
}
function parts(text: string) {
  const terms = lastQuery.value
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 10)
    .map((x) => x.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"));
  if (!terms.length) return [{ text, hit: false }];
  const regex = new RegExp(`(${terms.join("|")})`, "gi");
  return text
    .split(regex)
    .map((value, i) => ({ text: value, hit: i % 2 === 1 }));
}
async function copy(text: string) {
  try {
    await navigator.clipboard.writeText(text);
    ElMessage.success("已复制原文");
  } catch {
    ElMessage.info("请选中原文后复制");
  }
}
function applyAttempt(value: Attempt, reset = false) {
  attempt.value = value;
  serverOffset = new Date(value.serverTimeUtc).getTime() - Date.now();
  if (reset) {
    const cached = parse<Record<string, string[]>>(
      sessionStorage.getItem(cacheKey(value.id)) ?? "{}",
    );
    drafts.value = Object.fromEntries(
      value.questions.map((q) => [
        q.id,
        value.status === "active" && !(value.mode === "practice" && q.answered)
          ? (cached[q.id] ?? [...q.selected])
          : [...q.selected],
      ]),
    );
    failedSaves.clear();
  }
  if (value.status === "submitted") {
    for (const t of timers.values()) clearTimeout(t);
    timers.clear();
    failedSaves.clear();
    sessionStorage.removeItem(cacheKey(value.id));
  } else persistDrafts();
  tick();
}
async function loadProgress() {
  progress.value = await studyApi.progress(props.project.id);
  const q = await studyApi.questions(undefined, true);
  questionNames.value = Object.fromEntries(q.map((x) => [x.id, x.stem]));
}
async function start() {
  await run(async () => {
    await flush();
    const value = await studyApi.startAttempt(
      props.project.id,
      attemptForm.value,
    );
    applyAttempt(value, true);
    attemptDialog.value = false;
    tab.value = "attempt";
    await router.replace({
      query: { projectId: props.project.id, attemptId: value.id },
    });
    await loadProgress();
  });
}
async function resume(id: string) {
  await run(async () => {
    await flush();
    const value = await studyApi.attempt(id);
    if (value.projectId !== props.project.id)
      throw new Error("考试不属于此项目");
    applyAttempt(value, true);
    tab.value = "attempt";
    await router.replace({
      query: { projectId: props.project.id, attemptId: id },
    });
  });
}
function enqueue(id: string) {
  if (!attempt.value || attempt.value.status !== "active") return;
  persistDrafts();
  const attemptId = attempt.value.id;
  const selected = [...(drafts.value[id] ?? [])];
  pending = pending.then(async () => {
    if (attempt.value?.id !== attemptId || attempt.value.status !== "active")
      return;
    saving.value = true;
    saveMessage.value = "正在保存…";
    try {
      const value = await studyApi.answer(
        attemptId,
        id,
        selected,
        attempt.value.revision,
      );
      applyAttempt(value);
      failedSaves.delete(id);
      saveMessage.value = "已保存";
    } catch (e) {
      failedSaves.add(id);
      saveMessage.value = "保存失败，答案已暂存本机";
      error.value = e instanceof Error ? e.message : "保存失败";
      try {
        applyAttempt(await studyApi.attempt(attemptId));
      } catch {
        /* Retain local answers until retry. */
      }
    } finally {
      saving.value = false;
    }
  });
}
function changed(id: string) {
  persistDrafts();
  if (attempt.value?.mode !== "exam") return;
  const previous = timers.get(id);
  if (previous) clearTimeout(previous);
  timers.set(
    id,
    setTimeout(() => {
      timers.delete(id);
      enqueue(id);
    }, 400),
  );
}
async function flush() {
  for (const [id, t] of timers) {
    clearTimeout(t);
    enqueue(id);
  }
  timers.clear();
  await pending;
  if (attempt.value?.mode === "exam" && attempt.value.status === "active")
    for (const q of attempt.value.questions)
      if (
        JSON.stringify([...(drafts.value[q.id] ?? [])].sort()) !==
        JSON.stringify([...q.selected].sort())
      )
        failedSaves.add(q.id);
  for (const id of failedSaves) enqueue(id);
  await pending;
  if (failedSaves.size)
    throw new Error("部分答案尚未保存，请恢复连接并点击保存本题后重试。");
}
defineExpose({ flush });
onBeforeRouteLeave(async () => {
  try {
    await flush();
    return true;
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : "保存失败");
    return false;
  }
});

async function submit(confirm = true) {
  if (!attempt.value || submitStarted) return;
  if (confirm) {
    try {
      await ElMessageBox.confirm(
        "交卷后将显示答案和成绩，是否交卷？",
        "提交试卷",
      );
    } catch {
      return;
    }
  }
  submitStarted = true;
  try {
    await flush();
    const value = await studyApi.submit(attempt.value.id);
    applyAttempt(value);
    await loadProgress();
  } catch (e) {
    error.value = e instanceof Error ? e.message : "交卷失败";
  } finally {
    submitStarted = false;
  }
}
function tick() {
  if (!attempt.value?.deadlineUtc || attempt.value.status !== "active") {
    remaining.value = "";
    return;
  }
  const seconds = Math.max(
    0,
    Math.ceil(
      (new Date(
        /[Zz]|[+-]\d\d:\d\d$/.test(attempt.value.deadlineUtc)
          ? attempt.value.deadlineUtc
          : attempt.value.deadlineUtc + "Z",
      ).getTime() -
        Date.now() -
        serverOffset) /
        1000,
    ),
  );
  remaining.value = `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`;
  if (seconds === 0 && !submitStarted) void submit(false);
}
function keyboard(event: KeyboardEvent) {
  if (route.params.routeKey !== "study-projects" || tab.value !== "search")
    return;
  if (
    event.key === "/" &&
    !(event.target instanceof HTMLInputElement) &&
    !(event.target instanceof HTMLTextAreaElement)
  ) {
    event.preventDefault();
    searchInput.value?.focus();
  }
}
onMounted(() => {
  recent.value = parse(
    sessionStorage.getItem(`study-recent:${props.project.id}`) ?? "[]",
  );
  void run(async () => {
    await loadProgress();
    history.value = await studyApi.history(props.project.id);
    capabilities.value = await studyApi.capabilities();
    const id = String(route.query.attemptId ?? "");
    if (id) await resume(id);
  });
  clock = setInterval(tick, 1000);
  window.addEventListener("keydown", keyboard);
  window.addEventListener("beforeunload", beforeUnload);
});
onUnmounted(() => {
  persistDrafts();
  void flush().catch(() => {});
  window.removeEventListener("beforeunload", beforeUnload);
  searchController?.abort();
  explainController?.abort();
  clearInterval(clock);
  for (const timer of timers.values()) clearTimeout(timer);
  window.removeEventListener("keydown", keyboard);
});
</script>
<template>
  <section>
    <div class="study-toolbar">
      <span class="study-chip">{{ project.name }}</span
      ><span v-for="b in linked" :key="b.id" class="study-muted">{{
        b.name
      }}</span
      ><span v-if="!linked.length" class="study-muted">尚未关联知识库</span>
    </div>
    <el-alert
      v-if="error"
      :title="error"
      type="error"
      class="study-error"
      :closable="false"
    />
    <el-tabs
      v-model="tab"
      @tab-change="
        (name: string | number) => {
          if (name === 'progress') void run(loadProgress);
        }
      "
      ><el-tab-pane label="快速查原文" name="search"
        ><div class="study-search">
          <h3>制度条款，随查随用</h3>
          <p>
            输入关键词或粘贴完整题干。快速查询直接返回原文，不等待 AI 生成。按 /
            聚焦搜索。
          </p>
          <el-input
            ref="searchInput"
            v-model="query"
            size="large"
            clearable
            placeholder="例如：员工请假审批流程、差旅报销时限…"
            @keyup.enter="search()"
            ><template #append
              ><el-button :loading="searching" @click="search()"
                >查询</el-button
              ></template
            ></el-input
          >
          <div class="study-toolbar" style="margin: 16px 0 0">
            <el-select v-model="mode" style="width: 160px"
              ><el-option value="keyword" label="关键词 · 快速" /><el-option
                value="semantic"
                label="语义 + 关键词"
                :disabled="!capabilities.embeddingConfigured" /></el-select
            ><el-select
              v-model="kb"
              clearable
              placeholder="全部关联知识库"
              style="width: 230px"
              ><el-option
                v-for="b in linked"
                :key="b.id"
                :label="b.name"
                :value="b.id" /></el-select
            ><el-button
              :loading="explaining"
              :disabled="!capabilities.chatConfigured || !query.trim()"
              @click="explain"
              >AI 解释并附出处</el-button
            >
          </div>
        </div>
        <div v-if="recent.length" class="study-toolbar">
          <span class="study-muted">最近查询</span
          ><el-button
            v-for="item in recent"
            :key="item"
            size="small"
            text
            @click="
              query = item;
              search();
            "
            >{{ item.length > 24 ? item.slice(0, 24) + "…" : item }}</el-button
          >
        </div>
        <div v-if="answer" class="study-card">
          <h3>依据资料的回答</h3>
          <div class="study-text">{{ answer.answer }}</div>
          <CitationPanel :citations="parse(answer.citationsJson)" />
        </div>
        <div v-loading="searching">
          <div
            v-for="(hit, i) in results"
            :key="hit.citation.chunkId"
            class="study-result"
          >
            <div class="study-toolbar" style="margin-bottom: 8px">
              <span class="study-chip">{{ i + 1 }}</span
              ><span class="study-muted">{{ hit.citation.location }}</span
              ><el-button
                text
                style="margin-left: auto"
                @click="copy(hit.citation.text)"
                >复制原文</el-button
              >
            </div>
            <h3>{{ hit.citation.title }}</h3>
            <div class="study-text">
              <template v-for="(p, j) in parts(hit.citation.text)" :key="j"
                ><mark v-if="p.hit">{{ p.text }}</mark
                ><span v-else>{{ p.text }}</span></template
              >
            </div>
            <CitationPanel :citations="[hit.citation]" />
          </div>
          <el-empty
            v-if="searched && !results.length && !searching"
            description="关联资料中没有找到结果，可换用关键词或语义检索"
          /><el-button
            v-if="results.length >= 10 && results.length % 10 === 0"
            :loading="searching"
            @click="search(true)"
            >继续查看</el-button
          >
          <div v-if="!searched" class="study-card">
            <h3>查询范围</h3>
            <p class="study-muted">
              当前关联
              {{ linked.length }}
              个知识库。资料处理完成后即可查原文；模型未配置也可使用关键词查询。
            </p>
          </div>
        </div>
        <el-collapse v-if="history.length" style="margin-top: 20px"
          ><el-collapse-item title="历史问答 · 按生成时的资料版本保存"
            ><div v-for="h in history" :key="h.id" class="study-card">
              <strong>{{ h.query }}</strong>
              <p class="study-muted">{{ date(h.createdAtUtc) }}</p>
              <div class="study-text">{{ h.answer }}</div>
              <CitationPanel
                :citations="parse(h.citationsJson)"
              /></div></el-collapse-item></el-collapse
      ></el-tab-pane>
      <el-tab-pane label="刷题与模拟考试" name="attempt"
        ><div class="study-toolbar">
          <el-button
            type="primary"
            @click="
              attemptForm.mode = 'practice';
              attemptDialog = true;
            "
            >开始刷题</el-button
          ><el-button
            @click="
              attemptForm.mode = 'exam';
              attemptDialog = true;
            "
            >创建模拟考试</el-button
          ><span class="study-muted"
            >只抽取关联知识库中已审核、未过期的题目</span
          >
        </div>
        <template v-if="attempt"
          ><div v-if="attempt.status === 'submitted'" class="study-score">
            {{ label(attempt.mode) }}完成 ·
            <strong>{{ attempt.score }} 分</strong
            ><span v-if="attempt.mode === 'exam'">
              ·
              {{
                (attempt.score ?? 0) >= attempt.passScore
                  ? "达到及格线"
                  : "继续加油"
              }}</span
            >
          </div>
          <div v-else class="study-sticky study-toolbar">
            <el-tag>{{ label(attempt.mode) }}</el-tag
            ><strong v-if="remaining">剩余 {{ remaining }}</strong
            ><span>已答 {{ completed }} / {{ attempt.questions.length }}</span
            ><span class="study-muted">{{ saveMessage }}</span
            ><el-button type="primary" :loading="saving" @click="submit()"
              >交卷</el-button
            >
          </div>
          <article
            v-for="(q, i) in attempt.questions"
            :key="q.id"
            class="study-question"
          >
            <div class="study-toolbar">
              <span class="study-chip">{{ i + 1 }} · {{ label(q.type) }}</span
              ><el-tag
                v-if="q.correct !== null"
                :type="q.correct ? 'success' : 'danger'"
                >{{ q.correct ? "正确" : "错误" }}</el-tag
              >
            </div>
            <h3 class="study-text" style="font-size: 16px">{{ q.stem }}</h3>
            <el-checkbox-group
              v-if="q.type === 'multiple'"
              v-model="drafts[q.id]"
              :disabled="
                attempt.status === 'submitted' ||
                (attempt.mode === 'practice' && q.answered)
              "
              @change="changed(q.id)"
              ><el-checkbox v-for="o in q.options" :key="o.id" :value="o.id"
                >{{ o.id }}. {{ o.text }}</el-checkbox
              ></el-checkbox-group
            ><el-radio-group
              v-else
              :model-value="drafts[q.id]?.[0] ?? ''"
              :disabled="
                attempt.status === 'submitted' ||
                (attempt.mode === 'practice' && q.answered)
              "
              @change="
                (value: string | number | boolean) => {
                  drafts[q.id] = [String(value)];
                  changed(q.id);
                }
              "
              ><el-radio v-for="o in q.options" :key="o.id" :value="o.id"
                >{{ o.id }}. {{ o.text }}</el-radio
              ></el-radio-group
            ><el-button
              v-if="
                attempt.mode === 'practice' &&
                !q.answered &&
                attempt.status === 'active'
              "
              type="primary"
              :loading="saving"
              style="margin-top: 12px"
              @click="enqueue(q.id)"
              >提交本题</el-button
            ><el-button
              v-if="attempt.mode === 'exam' && attempt.status === 'active'"
              link
              @click="enqueue(q.id)"
              >保存本题</el-button
            >
            <div
              v-if="q.answers"
              class="study-answer"
              :class="{ wrong: q.correct === false }"
            >
              <strong>正确答案：{{ q.answers.join("、") }}</strong>
              <p class="study-text">{{ q.explanation }}</p>
              <CitationPanel :citations="q.citations ?? []" />
            </div></article></template
        ><el-empty
          v-else
          description="创建一轮练习，或从学习记录恢复未完成的考试"
      /></el-tab-pane>
      <el-tab-pane label="错题与学习记录" name="progress"
        ><div class="study-grid">
          <div class="study-card">
            <div class="study-muted">累计作答</div>
            <p class="study-stat">{{ progress.answered }}</p>
          </div>
          <div class="study-card">
            <div class="study-muted">正确率</div>
            <p class="study-stat">
              {{
                progress.answered
                  ? Math.round((progress.correct / progress.answered) * 100)
                  : 0
              }}%
            </p>
          </div>
          <div class="study-card">
            <div class="study-muted">待复习错题</div>
            <p class="study-stat">
              {{ progress.mistakes.filter((x) => !x.mastered).length }}
            </p>
          </div>
        </div>
        <div class="study-card">
          <div class="study-heading">
            <h3>错题</h3>
            <el-button
              @click="
                attemptForm.mode = 'practice';
                attemptForm.selection = 'mistakes';
                attemptForm.count = Math.max(
                  1,
                  Math.min(
                    20,
                    progress.mistakes.filter((x) => !x.mastered).length,
                  ),
                );
                attemptDialog = true;
              "
              >重练错题</el-button
            >
          </div>
          <el-table :data="progress.mistakes"
            ><el-table-column label="题目" min-width="250"
              ><template #default="{ row }">{{
                questionNames[row.questionId] ?? "题目已归档，可在历史试卷查看"
              }}</template></el-table-column
            ><el-table-column
              prop="wrongCount"
              label="答错次数"
              width="100" /><el-table-column label="掌握" width="100"
              ><template #default="{ row }"
                ><el-switch
                  :model-value="row.mastered"
                  @change="
                    (value: string | number | boolean) =>
                      run(async () => {
                        await studyApi.master(row.id, Boolean(value));
                        await loadProgress();
                      })
                  " /></template></el-table-column
          ></el-table>
        </div>
        <div class="study-card">
          <h3>学习记录</h3>
          <el-table :data="progress.attempts"
            ><el-table-column label="开始时间" min-width="185"
              ><template #default="{ row }">{{
                date(row.createdAtUtc)
              }}</template></el-table-column
            ><el-table-column label="类型" width="100"
              ><template #default="{ row }">{{
                label(row.mode)
              }}</template></el-table-column
            ><el-table-column label="状态" width="110"
              ><template #default="{ row }">{{
                label(row.status)
              }}</template></el-table-column
            ><el-table-column
              prop="score"
              label="成绩"
              width="90"
            /><el-table-column label="操作" width="120"
              ><template #default="{ row }"
                ><el-button link type="primary" @click="resume(row.id)">{{
                  row.status === "active" ? "继续作答" : "查看试卷"
                }}</el-button></template
              ></el-table-column
            ></el-table
          >
        </div></el-tab-pane
      ></el-tabs
    >
    <el-dialog
      v-model="attemptDialog"
      :title="attemptForm.mode === 'exam' ? '创建模拟考试' : '开始练习'"
      width="min(540px,94vw)"
      append-to-body
      ><el-form label-position="top"
        ><el-form-item label="抽题范围"
          ><el-checkbox-group v-model="attemptForm.types"
            ><el-checkbox
              v-for="t in ['single', 'multiple', 'boolean']"
              :key="t"
              :value="t"
              >{{ label(t) }}</el-checkbox
            ></el-checkbox-group
          ></el-form-item
        ><el-form-item label="题目数量"
          ><el-input-number
            v-model="attemptForm.count"
            :min="1"
            :max="200" /></el-form-item
        ><el-form-item label="抽题方式"
          ><el-select v-model="attemptForm.selection"
            ><el-option value="random" label="随机抽题" /><el-option
              value="sequential"
              label="顺序练习" /><el-option
              value="mistakes"
              label="未掌握错题" /></el-select></el-form-item
        ><template v-if="attemptForm.mode === 'exam'"
          ><el-form-item label="考试时长（分钟）"
            ><el-input-number
              v-model="attemptForm.minutes"
              :min="1"
              :max="300" /></el-form-item
          ><el-form-item label="及格分数"
            ><el-input-number
              v-model="attemptForm.passScore"
              :min="0"
              :max="100" /></el-form-item></template></el-form
      ><template #footer
        ><el-button @click="attemptDialog = false">取消</el-button
        ><el-button type="primary" :loading="busy" @click="start"
          >开始</el-button
        ></template
      ></el-dialog
    >
  </section>
</template>
