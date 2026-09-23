<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue";
import { useRoute } from "vue-router";
import { studyApi, type Job } from "../../api/examStudy";
import { label, date, useOperation } from "./shared";
const { run, busy } = useOperation();
const jobs = ref<Job[]>([]);
const route = useRoute();
async function load() {
  jobs.value = await studyApi.jobs();
}
let timer: ReturnType<typeof setInterval> | undefined;
onMounted(() => {
  void run(load);
  timer = setInterval(() => {
    if (route.params.routeKey === "study-jobs") void load().catch(() => {});
  }, 5000);
});
onUnmounted(() => clearInterval(timer));
</script>
<template>
  <main class="study-page">
    <header class="study-heading">
      <div>
        <h2>处理任务</h2>
        <p>查看资料解析、检索索引、AI 出题及文件迁移进度。</p>
      </div>
      <el-button @click="run(load)">刷新</el-button>
    </header>
    <div class="study-card" v-loading="busy">
      <el-table :data="jobs"
        ><el-table-column label="任务" width="180"
          ><template #default="{ row }">{{
            label(row.kind)
          }}</template></el-table-column
        ><el-table-column label="状态" width="100"
          ><template #default="{ row }"
            ><el-tag
              :type="
                row.status === 'completed'
                  ? 'success'
                  : row.status === 'failed'
                    ? 'danger'
                    : 'info'
              "
              >{{ label(row.status) }}</el-tag
            ></template
          ></el-table-column
        ><el-table-column label="创建时间" width="185"
          ><template #default="{ row }">{{
            date(row.createdAtUtc)
          }}</template></el-table-column
        ><el-table-column
          prop="attempts"
          label="尝试"
          width="70"
        /><el-table-column
          prop="error"
          label="说明"
          min-width="220"
        /><el-table-column label="操作" width="90"
          ><template #default="{ row }"
            ><el-button
              v-if="['failed', 'waiting'].includes(row.status)"
              link
              type="primary"
              @click="
                run(async () => {
                  await studyApi.retry(row.id);
                  await load();
                })
              "
              >重试</el-button
            ></template
          ></el-table-column
        ></el-table
      >
      <p class="study-muted">“语义增强”待配置不会影响已经就绪的关键词查询。</p>
    </div>
  </main>
</template>
