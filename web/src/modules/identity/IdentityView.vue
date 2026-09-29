<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { ElMessage, ElMessageBox } from "element-plus";
import { identityApi, type Account, type Role, type ModulePermission } from "../../api/examStudy";
import { hasPermission, loadPermissions } from "../../api/session";
import { useNavigationStore } from "../../stores/navigation";
import { useOperation } from "../exam-study/shared";
import MenuGrantDialog from "./MenuGrantDialog.vue";
const props = defineProps<{ routeKey: string }>();
const page = computed(() => props.routeKey === "system-users" ? "users" : props.routeKey === "system-roles" ? "roles" : "permissions");
const title = computed(() => ({ users: "用户管理", roles: "角色管理", permissions: "权限管理" })[page.value]);
const { run, busy, error } = useOperation();
const navigation = useNavigationStore();
const users = ref<Account[]>([]);
const roles = ref<Role[]>([]);
const catalog = ref<ModulePermission[]>([]);
const groups = computed(() => [...new Set(catalog.value.map(x => x.group))]);
const permissionTab = ref("user-roles");
const moduleKey = ref("");
const userDialog = ref(false);
const editingUser = ref<Account>();
const userForm = ref({ userName: "", email: "", password: "", roleIds: [] as string[] });
const roleDialog = ref(false);
const editingRole = ref<Role>();
const roleForm = ref({ name: "", permissions: [] as string[] });
const assignDialog = ref(false);
const selected = ref<Account>();
const roleIds = ref<string[]>([]);
const permissionDialog = ref(false);
const directPermissions = ref<string[]>([]);
const menuTarget = ref<{ id: string; name: string; role: boolean; menuCodes: string[]; inheritedMenuCodes: string[]; permissions: string[] }>();
const permissionNames = computed(() => Object.fromEntries(catalog.value.map(p => [p.key, p.name])));
function editUserMenus(user: Account) {
  menuTarget.value = { id: user.id, name: user.userName, role: false, menuCodes: [...user.menuCodes], permissions: user.effectivePermissions,
    inheritedMenuCodes: [...new Set(roles.value.filter(r => user.roles.includes(r.name)).flatMap(r => r.menuCodes))] };
}
function editRoleMenus(role: Role) {
  menuTarget.value = { id: role.id, name: role.name, role: true, menuCodes: [...role.menuCodes], inheritedMenuCodes: [], permissions: expand(role.permissions) };
}
async function menusSaved() { menuTarget.value = undefined; await run(updated); }
const canAssign = computed(() => hasPermission("system.permissions.manage"));
function expand(values: string[]) {
  const result = new Set(values.filter(x => x !== "exam-study.access" && x !== "system.identity.manage"));
  if (values.includes("exam-study.access")) catalog.value.filter(x => x.key.startsWith("study.")).forEach(x => result.add(x.key));
  if (values.includes("system.identity.manage")) ["system.users.manage", "system.roles.manage", "system.permissions.manage"].forEach(x => result.add(x));
  return [...result];
}
const nameOf = (key: string) => catalog.value.find(x => x.key === key)?.name ?? key;
async function load() {
  catalog.value = await identityApi.catalog();
  if (!moduleKey.value) moduleKey.value = catalog.value[0]?.key ?? "";
  roles.value = await identityApi.roles();
  if (page.value !== "roles") users.value = await identityApi.users();
}
async function updated() {
  await loadPermissions(true);
  await navigation.load(true);
  if (hasPermission(page.value === "users" ? "system.users.manage" : page.value === "roles" ? "system.roles.manage" : "system.permissions.manage")) await load();
  ElMessage.success("已保存");
}
function editUser(user?: Account) {
  editingUser.value = user;
  userForm.value = { userName: user?.userName ?? "", email: user?.email ?? "", password: "", roleIds: [] };
  userDialog.value = true;
}
async function saveUser() {
  await run(async () => {
    if (editingUser.value) await identityApi.update(editingUser.value.id, userForm.value);
    else await identityApi.create(userForm.value);
    userDialog.value = false;
    await updated();
  });
}
function editRole(role?: Role) {
  editingRole.value = role;
  roleForm.value = { name: role?.name ?? "", permissions: expand(role?.permissions ?? []) };
  roleDialog.value = true;
}
async function saveRole() {
  await run(async () => {
    await identityApi.saveRole(roleForm.value, editingRole.value?.id);
    roleDialog.value = false;
    await updated();
  });
}
function assign(user: Account) {
  selected.value = user;
  roleIds.value = roles.value.filter(r => user.roles.includes(r.name)).map(r => r.id);
  assignDialog.value = true;
}
async function saveRoles() {
  await run(async () => {
    await identityApi.assign(selected.value!.id, roleIds.value);
    assignDialog.value = false;
    await updated();
  });
}
function editPermissions(user: Account) {
  selected.value = user;
  directPermissions.value = expand(user.permissions);
  permissionDialog.value = true;
}
async function savePermissions() {
  await run(async () => {
    await identityApi.permissions(selected.value!.id, directPermissions.value);
    permissionDialog.value = false;
    await updated();
  });
}
async function toggleModule(target: Account | Role, isRole: boolean, value: boolean) {
  await run(async () => {
    const next = expand(target.permissions).filter(p => p !== moduleKey.value);
    if (value) next.push(moduleKey.value);
    if (isRole) await identityApi.saveRole({ name: (target as Role).name, permissions: next }, target.id);
    else await identityApi.permissions(target.id, next);
    await updated();
  });
}
async function disable(user: Account) {
  try { await ElMessageBox.confirm(`停用账号 ${user.userName}？该账号将无法继续访问系统。`, "停用账号", { type: "warning" }); }
  catch { return; }
  await run(async () => { await identityApi.disable(user.id); await load(); });
}
onMounted(() => { void run(load); });
</script>
<template>
  <main class="study-page">
    <header class="study-heading">
      <div><h2>{{ title }}</h2><p>{{ page === 'users' ? '维护用户账号、邮箱及角色信息。' : page === 'roles' ? '维护角色信息及角色拥有的模块权限。' : '管理用户角色、用户直接权限、角色权限及独立菜单授权。' }}</p></div>
      <el-button :loading="busy" @click="run(load)">刷新</el-button>
    </header>
    <el-alert v-if="error" :title="error" type="error" class="study-error" :closable="false" />
    <el-alert v-if="page === 'permissions'" title="有效权限 = 用户直接授权 + 所有角色授权。取消直接授权不会移除角色继承权限；Administrator 始终拥有全部权限。" type="info" :closable="false" show-icon class="study-error" />
    <el-tabs v-if="page === 'permissions'" v-model="permissionTab">
      <el-tab-pane label="用户角色管理" name="user-roles" />
      <el-tab-pane label="用户权限管理" name="user-permissions" />
      <el-tab-pane label="角色权限管理" name="role-permissions" />
      <el-tab-pane label="业务权限" name="modules" />
      <el-tab-pane label="菜单访问权限" name="menus" />
    </el-tabs>
    <div v-if="page === 'users' || page === 'permissions' && ['user-roles', 'user-permissions'].includes(permissionTab)" class="study-card" v-loading="busy">
      <div v-if="page === 'users'" class="study-toolbar"><el-button type="primary" @click="editUser()">创建用户</el-button></div>
      <el-table :data="users">
        <el-table-column prop="userName" label="账号" min-width="120" />
        <el-table-column prop="email" label="邮箱" min-width="180" />
        <el-table-column label="角色" min-width="160"><template #default="{ row }"><el-tag v-for="r in row.roles" :key="r" class="permission-tag">{{ r }}</el-tag></template></el-table-column>
        <el-table-column v-if="page === 'permissions' && permissionTab === 'user-permissions'" label="有效业务权限" min-width="260"><template #default="{ row }"><el-tag v-for="key in expand(row.effectivePermissions)" :key="key" class="permission-tag" :type="expand(row.permissions).includes(key) ? 'primary' : 'info'">{{ nameOf(key) }} · {{ expand(row.permissions).includes(key) ? '直接' : '角色' }}</el-tag></template></el-table-column>
        <el-table-column label="操作" min-width="210"><template #default="{ row }">
          <template v-if="page === 'users'"><el-button link type="primary" @click="editUser(row)">编辑信息</el-button><el-button link type="danger" @click="disable(row)">停用</el-button></template>
          <el-button v-if="canAssign && page === 'users' && !row.roles.includes('Administrator')" link type="primary" @click="editUserMenus(row)">菜单授权</el-button>
          <el-button v-if="canAssign && (page === 'users' || permissionTab === 'user-roles')" link type="primary" @click="assign(row)">分配角色</el-button>
          <el-button v-if="canAssign && permissionTab === 'user-permissions' && page === 'permissions'" link type="primary" @click="editPermissions(row)">直接授权</el-button>
        </template></el-table-column>
      </el-table>
    </div>
    <div v-if="page === 'roles' || page === 'permissions' && permissionTab === 'role-permissions'" class="study-card" v-loading="busy">
      <div class="study-toolbar"><el-button v-if="page === 'roles'" type="primary" @click="editRole()">新建角色</el-button></div>
      <el-table :data="roles">
        <el-table-column prop="name" label="角色名称" min-width="160" />
        <el-table-column label="业务权限" min-width="300"><template #default="{ row }"><span v-if="row.name === 'Administrator'">全部权限（含未来新增模块）</span><template v-else><el-tag v-for="key in expand(row.permissions)" :key="key" class="permission-tag">{{ nameOf(key) }}</el-tag></template></template></el-table-column>
        <el-table-column label="操作" width="120"><template #default="{ row }"><el-button v-if="row.name !== 'Administrator'" link type="primary" @click="editRole(row)">{{ page === 'roles' ? '编辑角色' : '配置权限' }}</el-button><el-button v-if="row.name !== 'Administrator'" link type="primary" @click="editRoleMenus(row)">菜单授权</el-button></template></el-table-column>
      </el-table>
    </div>
    <div v-if="page === 'permissions' && permissionTab === 'modules'" class="study-card" v-loading="busy">
      <div class="study-toolbar"><span>选择业务权限</span><el-select v-model="moduleKey" style="width: 330px"><el-option-group v-for="group in groups" :key="group" :label="group"><el-option v-for="item in catalog.filter(x => x.group === group)" :key="item.key" :label="item.name" :value="item.key" /></el-option-group></el-select></div>
      <h3>按用户授权</h3>
      <el-table :data="users"><el-table-column prop="userName" label="用户" /><el-table-column label="直接授权"><template #default="{ row }"><el-switch :model-value="expand(row.permissions).includes(moduleKey)" :disabled="busy || row.roles.includes('Administrator') || !hasPermission(moduleKey)" @change="(v: string | number | boolean) => toggleModule(row, false, Boolean(v))" /></template></el-table-column><el-table-column label="最终访问权限"><template #default="{ row }"><el-tag :type="row.effectivePermissions.includes(moduleKey) ? 'success' : 'info'">{{ row.effectivePermissions.includes(moduleKey) ? '可访问' : '未授权' }}</el-tag></template></el-table-column></el-table>
      <h3>按角色授权</h3>
      <el-table :data="roles"><el-table-column prop="name" label="角色" /><el-table-column label="授予业务权限"><template #default="{ row }"><el-switch :model-value="row.name === 'Administrator' || expand(row.permissions).includes(moduleKey)" :disabled="busy || row.name === 'Administrator' || !hasPermission(moduleKey)" @change="(v: string | number | boolean) => toggleModule(row, true, Boolean(v))" /></template></el-table-column></el-table>
    </div>
    <div v-if="page === 'permissions' && permissionTab === 'menus'" class="study-card" v-loading="busy">
      <el-alert title="菜单访问权与业务权限分别配置；授予菜单不会自动授予接口权限。" type="info" :closable="false" />
      <h3>用户直接菜单授权</h3>
      <el-table :data="users"><el-table-column prop="userName" label="用户" /><el-table-column label="直接 / 最终授权数量"><template #default="{ row }">{{ row.menuCodes.length }} / {{ row.effectiveMenuCodes.length }}</template></el-table-column><el-table-column label="操作"><template #default="{ row }"><el-button v-if="!row.roles.includes('Administrator')" link type="primary" @click="editUserMenus(row)">配置菜单</el-button><span v-else>全部菜单</span></template></el-table-column></el-table>
      <h3>角色菜单授权</h3>
      <el-table :data="roles"><el-table-column prop="name" label="角色" /><el-table-column label="授权数量"><template #default="{ row }">{{ row.menuCodes.length }}</template></el-table-column><el-table-column label="操作"><template #default="{ row }"><el-button v-if="row.name !== 'Administrator'" link type="primary" @click="editRoleMenus(row)">配置菜单</el-button><span v-else>全部菜单</span></template></el-table-column></el-table>
    </div>
    <MenuGrantDialog v-if="menuTarget" :target="menuTarget" :permission-names="permissionNames" @close="menuTarget = undefined" @saved="menusSaved" />
    <el-dialog v-model="userDialog" :title="editingUser ? '编辑用户信息' : '创建用户'" width="min(560px,94vw)" append-to-body>
      <el-form label-position="top"><el-form-item label="账号"><el-input v-model="userForm.userName" maxlength="100" /></el-form-item><el-form-item label="邮箱"><el-input v-model="userForm.email" /></el-form-item><template v-if="!editingUser"><el-form-item label="初始密码"><el-input v-model="userForm.password" type="password" show-password autocomplete="new-password" placeholder="至少12位，包含大写、小写和数字" /></el-form-item><el-form-item v-if="canAssign" label="初始角色"><el-select v-model="userForm.roleIds" multiple style="width: 100%"><el-option v-for="r in roles" :key="r.id" :label="r.name" :value="r.id" /></el-select></el-form-item></template></el-form>
      <template #footer><el-button @click="userDialog = false">取消</el-button><el-button type="primary" :loading="busy" @click="saveUser">保存</el-button></template>
    </el-dialog>
    <el-dialog v-model="roleDialog" :title="editingRole ? '编辑角色与权限' : '新建角色'" width="min(680px,94vw)" append-to-body>
      <el-form label-position="top"><el-form-item label="角色名称"><el-input v-model="roleForm.name" maxlength="100" /></el-form-item></el-form>
      <el-checkbox-group v-model="roleForm.permissions"><section v-for="group in groups" :key="group" class="permission-group"><h4>{{ group }}</h4><el-checkbox v-for="item in catalog.filter(x => x.group === group)" :key="item.key" :value="item.key" :disabled="!hasPermission(item.key)">{{ item.name }}</el-checkbox></section></el-checkbox-group>
      <template #footer><el-button @click="roleDialog = false">取消</el-button><el-button type="primary" :loading="busy" @click="saveRole">保存</el-button></template>
    </el-dialog>
    <el-dialog v-model="assignDialog" :title="'用户角色 · ' + selected?.userName" width="min(540px,94vw)" append-to-body><el-select v-model="roleIds" multiple placeholder="选择角色" style="width: 100%"><el-option v-for="r in roles" :key="r.id" :label="r.name" :value="r.id" /></el-select><template #footer><el-button @click="assignDialog = false">取消</el-button><el-button type="primary" :loading="busy" @click="saveRoles">保存</el-button></template></el-dialog>
    <el-dialog v-model="permissionDialog" :title="'用户直接权限 · ' + selected?.userName" width="min(680px,94vw)" append-to-body>
      <p class="study-muted">此处仅修改直接授权，角色继承的权限请在角色授权中调整。</p>
      <el-checkbox-group v-model="directPermissions"><section v-for="group in groups" :key="group" class="permission-group"><h4>{{ group }}</h4><el-checkbox v-for="item in catalog.filter(x => x.group === group)" :key="item.key" :value="item.key" :disabled="!hasPermission(item.key)">{{ item.name }}</el-checkbox></section></el-checkbox-group>
      <template #footer><el-button @click="permissionDialog = false">取消</el-button><el-button type="primary" :loading="busy" @click="savePermissions">保存</el-button></template>
    </el-dialog>
  </main>
</template>
<style scoped>
.permission-tag { margin: 3px; }
.permission-group { padding: 6px 0 14px; border-bottom: 1px solid var(--el-border-color-lighter); }
.permission-group h4 { margin: 8px 0; }
.permission-group .el-checkbox { height: auto; min-height: 32px; white-space: normal; }
</style>
