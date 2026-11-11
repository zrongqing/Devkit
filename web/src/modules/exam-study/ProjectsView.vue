<script setup lang="ts">
import PageDialog from '../../components/PageDialog.vue'
import DataTable from "../../components/DataTable";
import { tableQueries } from "../../api/tableQueries";
import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import {
  studyApi,
  type KnowledgeBase,
  type Project,
} from "../../api/examStudy";
import { permissions, session } from "../../api/session";
import { parse, useOperation } from "./shared";
import ProjectWorkspace from "./ProjectWorkspace.vue";
const props = defineProps<{ routeKey: string }>();
const managing = computed(() => props.routeKey === "study-project-management");
const workspaceMode = computed(() => props.routeKey === "study-progress" ? "progress" : props.routeKey === "study-practice" ? "attempt" : "search");
const route = useRoute();
const router = useRouter();
const { run, busy, error } = useOperation();
const projects = ref<Project[]>([]);
const bases = ref<KnowledgeBase[]>([]);
const all = ref(false);
const selected = ref<Project>();
const dialog = ref(false);
const editing = ref<Project>();
const workspace = ref<InstanceType<typeof ProjectWorkspace>>();
const form = ref({
  name: "",
  description: "",
  targetDate: null as string | null,
  knowledgeBaseIds: [] as string[],
});
async function load() {
  [projects.value, bases.value] = await Promise.all([
    studyApi.projects(all.value),
    studyApi.bases(all.value),
  ]);
  if (selected.value)
    selected.value = projects.value.find((x) => x.id === selected.value!.id);
}
async function select(p: Project) {
  await workspace.value?.flush();
  selected.value = p;
  await router.replace({ query: { projectId: p.id } });
}
function edit(p?: Project) {
  editing.value = p;
  form.value = {
    name: p?.name ?? "",
    description: p?.description ?? "",
    targetDate: p?.targetDate ?? null,
    knowledgeBaseIds: p ? parse(p.knowledgeBaseIdsJson) : [],
  };
  dialog.value = true;
}
async function save() {
  await run(async () => {
    await workspace.value?.flush();
    const p = await studyApi.saveProject(
      { ...form.value, revision: editing.value?.revision },
      editing.value?.id,
    );
    dialog.value = false;
    await load();
    await select(p);
  });
}
onMounted(() => {
  void run(async () => {
    await load();
    selected.value = projects.value.find(
      (x) => x.id === String(route.query.projectId ?? ""),
    );
  });
});

const tableValues = {
  knowledgeBases: ({ knowledgeBaseIdsJson }: { knowledgeBaseIdsJson: string }) => bases.value.filter(b => parse<string[]>(knowledgeBaseIdsJson).includes(b.id)).map(b => b.name),
};
</script>
<template>
  <main class="study-page">
    <el-alert
      v-if="error"
      :title="error"
      type="error"
      class="study-error"
      :closable="false"
    />
    <div v-if="!managing" class="study-toolbar">
      <el-select
        :model-value="selected?.id ?? ''"
        placeholder="选择项目"
        style="min-width: 280px"
        @change="
          (id: string) => run(() => select(projects.find((x) => x.id === id)!))
        "
        ><el-option
          v-for="p in projects"
          :key="p.id"
          :value="p.id"
          :label="p.name" /></el-select
      >
      <el-switch v-if="permissions?.administrator" v-model="all" active-text="全部用户" @change="run(load)" />
      <el-button @click="run(load)">刷新</el-button>
    </div>
    <div v-if="managing" class="study-card" v-loading="busy">
      <DataTable table-key="study-projects" :load-page="tableQueries.projects(all)" :query-context="String(all)" :data="projects" empty-text="暂无项目，点击新建项目开始配置">
        <template #actions><el-button type="primary" @click="edit()">新建项目</el-button></template>
        <template v-if="permissions?.administrator" #toolbar>
          <el-switch v-model="all" active-text="全部用户" @change="run(load)" />
        </template>
        <el-table-column prop="name" label="项目名称" min-width="160" />
        <el-table-column prop="description" label="说明" min-width="200" />
        <el-table-column column-key="knowledgeBases" :table-value="tableValues.knowledgeBases" label="关联知识库" min-width="200"><template #default="{ row }">
          <el-tag v-for="base in bases.filter(b => parse<string[]>(row.knowledgeBaseIdsJson).includes(b.id))" :key="base.id" style="margin: 4px">{{ base.name }}</el-tag>
        </template></el-table-column>
        <el-table-column column-key="actions" label="操作" width="160"><template #default="{ row }"><el-button link type="primary" @click="edit(row)">编辑 / 关联知识库</el-button></template></el-table-column>
      </DataTable>
    </div>
    <ProjectWorkspace
      ref="workspace"
      v-else-if="selected"
      :key="selected.id + ':' + selected.revision"
      :workspace-mode="workspaceMode"
      :project="selected"
      :bases="bases"
    />
    <div v-else class="study-card study-empty" v-loading="busy">
      <el-empty description="请选择项目；项目及关联资料可在项目管理中维护" />
    </div>
    <PageDialog
      v-model="dialog"
      title="项目管理"
      :width="620"
      ><el-form label-position="top"
        ><el-form-item label="项目名称"
          ><el-input v-model="form.name" maxlength="200" /></el-form-item
        ><el-form-item label="说明"
          ><el-input
            v-model="form.description"
            type="textarea"
            :rows="3" /></el-form-item
        ><el-form-item label="目标日期"
          ><el-date-picker
            v-model="form.targetDate"
            type="date"
            value-format="YYYY-MM-DDTHH:mm:ss"
            clearable /></el-form-item
        ><el-form-item label="关联知识库"
          ><el-select
            v-model="form.knowledgeBaseIds"
            multiple
            style="width: 100%"
            placeholder="可以关联多个知识库"
            ><el-option
              v-for="b in bases.filter(
                (x) => x.ownerId === (editing?.ownerId ?? session?.user.id),
              )"
              :key="b.id"
              :label="b.name"
              :value="b.id" /></el-select
        ></el-form-item>
        <p class="study-muted">
          查询和抽题仅覆盖关联的知识库，取消关联后立即从后续查询与组卷范围移除。
        </p></el-form
      ><template #footer
        ><el-button @click="dialog = false">取消</el-button
        ><el-button type="primary" :loading="busy" @click="save"
          >保存</el-button
        ></template
      ></PageDialog
    >
  </main>
</template>
