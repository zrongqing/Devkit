import { ref } from "vue";
import { ElMessage } from "element-plus";
export function useOperation() {
  const busy = ref(false);
  const error = ref("");
  async function run<T>(action: () => Promise<T>): Promise<T | undefined> {
    busy.value = true;
    error.value = "";
    try {
      return await action();
    } catch (e) {
      if (e instanceof DOMException && e.name === "AbortError")
        return undefined;
      error.value = e instanceof Error ? e.message : String(e);
      ElMessage.error(error.value);
      return undefined;
    } finally {
      busy.value = false;
    }
  }
  return { busy, error, run };
}
export const labels: Record<string, string> = {
  queued: "等待处理",
  running: "处理中",
  completed: "已完成",
  waiting: "待配置",
  failed: "失败",
  ready: "可检索",
  draft: "待审核",
  approved: "已审核",
  stale: "待复核",
  disabled: "已停用",
  ingest: "提取与关键词索引",
  dense: "语义增强",
  generate: "AI 出题",
  migrate: "文件迁移",
  cleanup: "清理旧副本",
  "remove-index": "移除索引",
  single: "单选",
  multiple: "多选",
  boolean: "判断",
  practice: "练习",
  exam: "模拟考试",
  active: "进行中",
  submitted: "已交卷",
  uploading: "上传中",
  purged: "已永久删除",
  purging: "待完成清理",
};
export const label = (value: string) => labels[value] ?? value;
export const date = (value?: string | null) =>
  value
    ? new Date(
        value.endsWith("Z") || /[+-]\d\d:\d\d$/.test(value)
          ? value
          : `${value}Z`,
      ).toLocaleString("zh-CN")
    : "—";
export const bytes = (value: number | null) =>
  value === null
    ? "未知"
    : value > 1024 ** 3
      ? `${(value / 1024 ** 3).toFixed(2)} GiB`
      : value > 1024 ** 2
        ? `${(value / 1024 ** 2).toFixed(1)} MiB`
        : `${Math.round(value / 1024)} KiB`;
export function parse<T>(value: string): T {
  return JSON.parse(value) as T;
}
