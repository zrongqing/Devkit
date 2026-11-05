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
export { labels, label } from "../../api/queryOptions";
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
