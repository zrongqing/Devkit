<script setup lang="ts">
import { onMounted, ref } from "vue";
import { ElMessage, ElMessageBox } from "element-plus";
import { identityApi, type Account, type Role } from "../../api/examStudy";
import { loadPermissions } from "../../api/session";
import { useNavigationStore } from "../../stores/navigation";
import { useOperation } from "../exam-study/shared";
const { run, busy, error } = useOperation();
const navigation = useNavigationStore();
const users = ref<Account[]>([]);
const roles = ref<Role[]>([]);
const userDialog = ref(false);
const roleDialog = ref(false);
const assignDialog = ref(false);
const selected = ref<Account>();
const roleEditing = ref<Role>();
const userForm = ref({
  userName: "",
  email: "",
  password: "",
  roleIds: [] as string[],
});
const roleForm = ref({ name: "", permissions: [] as string[] });
const roleIds = ref<string[]>([]);
const catalog = [
  { key: "exam-study.access", name: "制度知识库、快速查询与备考" },
  { key: "system.files.manage", name: "通用文件管理" },
  { key: "system.storage.manage", name: "存储位置及迁移管理" },
  { key: "system.identity.manage", name: "用户、角色与授权管理" },
];
async function load() {
  [users.value, roles.value] = await Promise.all([
    identityApi.users(),
    identityApi.roles(),
  ]);
}
async function refreshPermissions() {
  await loadPermissions();
  await navigation.load(true);
}
function editRole(r?: Role) {
  roleEditing.value = r;
  roleForm.value = {
    name: r?.name ?? "",
    permissions: [...(r?.permissions ?? [])],
  };
  roleDialog.value = true;
}
async function saveRole() {
  await run(async () => {
    await identityApi.saveRole(roleForm.value, roleEditing.value?.id);
    roleDialog.value = false;
    await load();
    await refreshPermissions();
  });
}
function assign(u: Account) {
  selected.value = u;
  roleIds.value = roles.value
    .filter((r) => u.roles.includes(r.name))
    .map((r) => r.id);
  assignDialog.value = true;
}
async function saveAssignment() {
  await run(async () => {
    await identityApi.assign(selected.value!.id, roleIds.value);
    assignDialog.value = false;
    await load();
    await refreshPermissions();
  });
}
async function create() {
  await run(async () => {
    await identityApi.create(userForm.value);
    userDialog.value = false;
    userForm.value = { userName: "", email: "", password: "", roleIds: [] };
    await load();
    ElMessage.success("账号已创建");
  });
}
async function disable(u: Account) {
  try {
    await ElMessageBox.confirm(
      `停用账号 ${u.userName}？该账号将无法继续访问系统。`,
      "停用账号",
      { type: "warning" },
    );
  } catch {
    return;
  }
  await run(async () => {
    await identityApi.disable(u.id);
    await load();
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
        <h2>用户与权限</h2>
        <p>
          按角色授予功能权限。Administrator
          拥有全部权限和全部用户数据的管理能力。
        </p>
      </div>
      <el-button @click="run(load)">刷新</el-button>
    </header>
    <el-alert
      v-if="error"
      :title="error"
      type="error"
      class="study-error"
      :closable="false"
    /><el-tabs v-loading="busy"
      ><el-tab-pane label="用户"
        ><div class="study-card">
          <div class="study-toolbar">
            <el-button type="primary" @click="userDialog = true"
              >创建用户</el-button
            >
          </div>
          <el-table :data="users"
            ><el-table-column prop="userName" label="账号" /><el-table-column
              prop="email"
              label="邮箱"
            /><el-table-column label="角色"
              ><template #default="{ row }"
                ><el-tag
                  v-for="r in row.roles"
                  :key="r"
                  style="margin-right: 6px"
                  >{{ r }}</el-tag
                ></template
              ></el-table-column
            ><el-table-column label="操作" width="190"
              ><template #default="{ row }"
                ><el-button link type="primary" @click="assign(row)"
                  >分配角色</el-button
                ><el-button link type="danger" @click="disable(row)"
                  >停用</el-button
                ></template
              ></el-table-column
            ></el-table
          >
        </div></el-tab-pane
      ><el-tab-pane label="角色与权限"
        ><div class="study-card">
          <div class="study-toolbar">
            <el-button type="primary" @click="editRole()">新建角色</el-button>
          </div>
          <el-table :data="roles"
            ><el-table-column
              prop="name"
              label="角色"
              width="180"
            /><el-table-column label="权限"
              ><template #default="{ row }"
                ><span v-if="row.name === 'Administrator'"
                  >全部权限（含未来新增权限）</span
                ><el-tag
                  v-for="p in row.name === 'Administrator'
                    ? []
                    : row.permissions"
                  :key="p"
                  style="margin: 4px"
                  >{{ catalog.find((x) => x.key === p)?.name ?? p }}</el-tag
                ></template
              ></el-table-column
            ><el-table-column label="操作" width="95"
              ><template #default="{ row }"
                ><el-button
                  v-if="row.name !== 'Administrator'"
                  link
                  type="primary"
                  @click="editRole(row)"
                  >编辑</el-button
                ></template
              ></el-table-column
            ></el-table
          >
        </div></el-tab-pane
      ></el-tabs
    >
    <el-dialog
      v-model="userDialog"
      title="创建用户"
      width="min(540px,94vw)"
      append-to-body
      ><el-form label-position="top"
        ><el-form-item label="账号"
          ><el-input v-model="userForm.userName" /></el-form-item
        ><el-form-item label="邮箱"
          ><el-input v-model="userForm.email" /></el-form-item
        ><el-form-item label="初始密码"
          ><el-input
            v-model="userForm.password"
            type="password"
            show-password
            placeholder="至少12位，包含大写、小写和数字" /></el-form-item
        ><el-form-item label="角色"
          ><el-select v-model="userForm.roleIds" multiple style="width: 100%"
            ><el-option
              v-for="r in roles"
              :key="r.id"
              :label="r.name"
              :value="r.id" /></el-select></el-form-item></el-form
      ><template #footer
        ><el-button @click="userDialog = false">取消</el-button
        ><el-button type="primary" :loading="busy" @click="create"
          >创建</el-button
        ></template
      ></el-dialog
    >
    <el-dialog
      v-model="roleDialog"
      title="角色权限"
      width="min(600px,94vw)"
      append-to-body
      ><el-form label-position="top"
        ><el-form-item label="角色名称"
          ><el-input v-model="roleForm.name" /></el-form-item
        ><el-form-item label="功能权限"
          ><el-checkbox-group v-model="roleForm.permissions"
            ><el-checkbox
              v-for="p in catalog"
              :key="p.key"
              :value="p.key"
              style="display: flex"
              >{{ p.name }}</el-checkbox
            ></el-checkbox-group
          ></el-form-item
        ></el-form
      ><template #footer
        ><el-button @click="roleDialog = false">取消</el-button
        ><el-button type="primary" :loading="busy" @click="saveRole"
          >保存</el-button
        ></template
      ></el-dialog
    >
    <el-dialog
      v-model="assignDialog"
      :title="'分配角色 · ' + selected?.userName"
      width="min(500px,94vw)"
      append-to-body
      ><el-select v-model="roleIds" multiple style="width: 100%"
        ><el-option
          v-for="r in roles"
          :key="r.id"
          :value="r.id"
          :label="r.name" /></el-select
      ><template #footer
        ><el-button @click="assignDialog = false">取消</el-button
        ><el-button type="primary" :loading="busy" @click="saveAssignment"
          >保存</el-button
        ></template
      ></el-dialog
    >
  </main>
</template>
