using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Devkit.Server.Application.Navigation;
using Devkit.Server.Application.Workspace;
using Devkit.Server.Domain.Identity;
using Devkit.Server.Infrastructure.Persistence;
using Devkit.Server.Domain.Navigation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Devkit.Server.Api.IntegrationTests.WorkspaceTestSupport;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class WebMenuEndpointTests
{
    private const string Root = "/api/v1/web/menu-management";
    private static async Task<MenuTreeView> Tree(HttpClient c) => await Data<MenuTreeView>(await c.GetAsync(Root + "/menus"));
    private static MenuDeclaration[] Manifest(MenuTreeView tree) => tree.Menus.Select(x => x.LastDeclaration!).ToArray();
    private static MenuDeclaration Module(string code, string? parent = "knowledge") => new(code,"module",parent,"新页面",code,"book",50,["study.search"]);
    private static MenuDeclaration Directory(string code, string? parent = "knowledge") => new(code,"directory",parent,"新目录",null,"book",49,[]);
    private static Task<MenuComparison> Compare(HttpClient c, MenuDeclaration[] manifest) => CompareCore(c, manifest);
    private static async Task<MenuComparison> CompareCore(HttpClient c, MenuDeclaration[] manifest) => await Data<MenuComparison>(await c.PostAsJsonAsync(Root + "/compare",new MenuManifestRequest(manifest)));
    private static async Task<MenuTreeView> Sync(HttpClient c,int version,MenuDeclaration[] manifest,params MenuSyncItem[] items) => await Data<MenuTreeView>(await c.PostAsJsonAsync(Root + "/sync",new MenuSyncRequest(version,manifest,items)));

    [Fact]
    public async Task Initial_tree_has_requested_three_level_hierarchy_and_stable_codes()
    {
        using var factory=new DevkitApiFactory(builtInAdmin:true);using var admin=factory.CreateClient();await Login(admin);
        var tree=await Tree(admin);
        Assert.Equal("knowledge",tree.Menus.Single(x=>x.MenuCode=="knowledge.projects").ParentCode);
        Assert.Equal("knowledge.practice",tree.Menus.Single(x=>x.MenuCode=="knowledge.practice.questions").ParentCode);
        Assert.Equal("system.identity",tree.Menus.Single(x=>x.MenuCode=="system.identity.users").ParentCode);
        Assert.Contains(tree.Menus,x=>x.MenuCode=="system.menus" && x.Type=="module" && x.RouteKey=="system-menus");
        Assert.Equal("study-projects",tree.Menus.Single(x=>x.MenuCode=="knowledge.search").RouteKey);
        var comparison=await Compare(admin,Manifest(tree));
        Assert.All(comparison.Items,x=>Assert.Equal("synced",x.Status));
        Assert.Equal(tree.Version,(await Tree(admin)).Version); // comparison is read-only
        using (var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<DevkitDbContext>();
            var saved=await db.Set<WebMenu>().SingleAsync(x=>x.MenuCode=="knowledge.search");
            saved.DeclarationJson=JsonSerializer.Serialize(JsonData.Read<MenuDeclaration>(saved.DeclarationJson!), new JsonSerializerOptions(JsonData.Options){WriteIndented=true});
            await db.SaveChangesAsync();
        }
        Assert.Equal("synced",(await Compare(admin,Manifest(tree))).Items.Single(x=>x.MenuCode=="knowledge.search").Status); // SQL JSON formatting is not a declaration change
        using var anonymous=factory.CreateClient();
        var visible=await Data<JsonElement>(await anonymous.GetAsync("/api/v1/web/navigation/menus"));
        Assert.Contains("home",visible.ToString());Assert.DoesNotContain("knowledge",visible.ToString());
        var doc=await admin.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.True(doc.GetProperty("paths").TryGetProperty(Root+"/sync",out _));
        Assert.True(doc.GetProperty("paths").TryGetProperty("/api/v1/identity/users/{id}/menus",out _));
    }

    [Fact]
    public async Task Sync_creates_missing_parents_is_idempotent_and_preserves_custom_configuration()
    {
        using var factory=new DevkitApiFactory(builtInAdmin:true);using var admin=factory.CreateClient();await Login(admin);
        var tree=await Tree(admin);var manifest=Manifest(tree).Concat([Directory("knowledge.extra"),Module("new.page","knowledge.extra")]).ToArray();
        var diff=await Compare(admin,manifest);Assert.Equal("new",diff.Items.Single(x=>x.MenuCode=="new.page").Status);
        tree=await Sync(admin,diff.Version,manifest,new MenuSyncItem("new.page",[]));
        Assert.Contains(tree.Menus,x=>x.MenuCode=="knowledge.extra");
        Assert.False(tree.Menus.Single(x=>x.MenuCode=="new.page").IsPublic);
        var count=tree.Menus.Count;
        tree=await Sync(admin,tree.Version,manifest,new MenuSyncItem("new.page",[]));Assert.Equal(count,tree.Menus.Count);
        tree=await Data<MenuTreeView>(await admin.PutAsJsonAsync(Root+"/menus/new.page",new MenuEditRequest(tree.Version,"后台名称","system.monitor","settings",77,false)));
        manifest=manifest.Select(x=>x.MenuCode=="new.page"?x with{Title="新版默认名称",Order=90}:x).ToArray();
        diff=await Compare(admin,manifest);var change=diff.Items.Single(x=>x.MenuCode=="new.page");
        Assert.Equal("changed",change.Status);Assert.Equal("后台名称",change.Current!.Title);Assert.Equal("新页面",change.Current.LastDeclaration!.Title);
        tree=await Sync(admin,diff.Version,manifest,new MenuSyncItem("new.page",[]));var menu=tree.Menus.Single(x=>x.MenuCode=="new.page");
        Assert.Equal("后台名称",menu.Title);Assert.Equal("system.monitor",menu.ParentCode);Assert.Equal(77,menu.Order);Assert.False(menu.Enabled);
        Assert.Equal("synced",(await Compare(admin,manifest)).Items.Single(x=>x.MenuCode=="new.page").Status);
        tree=await Sync(admin,tree.Version,manifest,new MenuSyncItem("new.page",["title"]));menu=tree.Menus.Single(x=>x.MenuCode=="new.page");
        Assert.Equal("新版默认名称",menu.Title);Assert.Equal(77,menu.Order);Assert.False(menu.Enabled);
        Assert.Equal("missing",(await Compare(admin,Manifest(await Tree(admin)).Where(x=>x.MenuCode!="new.page").ToArray())).Items.Single(x=>x.MenuCode=="new.page").Status);
    }

    [Fact]
    public async Task Stale_sync_and_invalid_batch_never_partially_apply()
    {
        using var factory=new DevkitApiFactory(builtInAdmin:true);using var admin=factory.CreateClient();await Login(admin);
        var tree=await Tree(admin);var manifest=Manifest(tree).Append(Module("new.page")).ToArray();var old=tree.Version;
        tree=await Data<MenuTreeView>(await admin.PostAsJsonAsync(Root+"/menus",new DirectoryCreateRequest(tree.Version,"manual.dir","人工目录",null,null,80)));
        await Problem(await admin.PostAsJsonAsync(Root+"/sync",new MenuSyncRequest(old,manifest,[new("new.page",[])])),HttpStatusCode.Conflict,"menu_conflict");
        Assert.DoesNotContain((await Tree(admin)).Menus,x=>x.MenuCode=="new.page");
        var invalid=manifest.Select(x=>x.MenuCode=="knowledge.search"?x with{RouteKey="replacement-page"}:x).ToArray();
        await Problem(await admin.PostAsJsonAsync(Root+"/sync",new MenuSyncRequest(tree.Version,invalid,[new("new.page",[]),new("knowledge.search",[])])),HttpStatusCode.BadRequest,"invalid_menu");
        var after=await Tree(admin);Assert.Equal(tree.Version,after.Version);Assert.DoesNotContain(after.Menus,x=>x.MenuCode=="new.page");
        Assert.Equal("manual",(await Compare(admin,Manifest(tree).Where(x=>x is not null).ToArray())).Items.Single(x=>x.MenuCode=="manual.dir").Status);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("cycle")]
    [InlineData("module-parent")]
    [InlineData("directory-route")]
    [InlineData("unknown-permission")]
    [InlineData("null-permissions")]
    [InlineData("bad-code")]
    public async Task Invalid_manifests_are_rejected(string kind)
    {
        using var factory=new DevkitApiFactory(builtInAdmin:true);using var admin=factory.CreateClient();await Login(admin);
        var tree=await Tree(admin);var manifest=Manifest(tree).ToList();
        switch(kind)
        {
            case "duplicate":manifest.Add(manifest[0]);break;
            case "cycle":manifest.Add(Directory("cycle.a","cycle.b"));manifest.Add(Directory("cycle.b","cycle.a"));break;
            case "module-parent":manifest.Add(Module("new.page","knowledge.search"));break;
            case "directory-route":manifest.Add(Directory("new.dir") with{RouteKey="new.dir"});break;
            case "unknown-permission":manifest.Add(Module("new.page") with{RequiredPermissions=["arbitrary"]});break;
            case "null-permissions":manifest.Add(Module("new.page") with{RequiredPermissions=null!});break;
            default:manifest.Add(Module("../unsafe"));break;
        }
        await Problem(await admin.PostAsJsonAsync(Root+"/compare",new MenuManifestRequest(manifest.ToArray())),HttpStatusCode.BadRequest,"invalid_menu");
        Assert.Equal(tree.Version,(await Tree(admin)).Version);
    }

    [Fact]
    public async Task Directory_edits_validate_cycles_protected_paths_and_subtree_visibility()
    {
        using var factory=new DevkitApiFactory(builtInAdmin:true);using var admin=factory.CreateClient();await Login(admin);
        var tree=await Tree(admin);
        await Problem(await admin.PutAsJsonAsync(Root+"/menus/system.management",new MenuEditRequest(tree.Version,"系统管理",null,null,90,false)),HttpStatusCode.BadRequest,"invalid_menu");
        await Problem(await admin.PutAsJsonAsync(Root+"/menus/knowledge",new MenuEditRequest(tree.Version,"知识库","knowledge.practice",null,20,true)),HttpStatusCode.BadRequest,"invalid_menu");
        await Problem(await admin.DeleteAsync(Root+$"/menus/knowledge?version={tree.Version}"),HttpStatusCode.BadRequest,"invalid_menu");
        await Problem(await admin.DeleteAsync(Root+$"/menus/system.menus?version={tree.Version}"),HttpStatusCode.BadRequest,"invalid_menu");
        await Problem(await admin.PutAsJsonAsync(Root+"/menus/not-found",new MenuEditRequest(tree.Version,"不存在",null,null,1,true)),HttpStatusCode.NotFound,"not_found");
        tree=await Data<MenuTreeView>(await admin.PutAsJsonAsync(Root+"/menus/knowledge",new MenuEditRequest(tree.Version,"知识库",null,"book",20,false)));
        Assert.DoesNotContain("knowledge.search",(await Data<JsonElement>(await admin.GetAsync("/api/v1/web/navigation/menus"))).ToString());
        tree=await Data<MenuTreeView>(await admin.PostAsJsonAsync(Root+"/menus",new DirectoryCreateRequest(tree.Version,"empty.dir","空目录",null,null,90)));
        tree=await Data<MenuTreeView>(await admin.DeleteAsync(Root+$"/menus/empty.dir?version={tree.Version}"));
        Assert.DoesNotContain(tree.Menus,x=>x.MenuCode=="empty.dir");
    }

    [Fact]
    public async Task User_and_role_menu_grants_are_independent_from_business_permissions()
    {
        using var factory=new DevkitApiFactory(builtInAdmin:true);using var admin=factory.CreateClient();await Login(admin);
        var role=await Data<Guid>(await admin.PostAsJsonAsync("/api/v1/identity/roles",new RoleRequest("菜单读者",[])));
        var id=await Data<Guid>(await admin.PostAsJsonAsync("/api/v1/identity/users",new AccountRequest("reader","reader@example.test","StrongPassword123",[role])));
        using var user=factory.CreateClient();await Login(user,"reader","StrongPassword123");
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/roles/{role}/menus",new MenuGrantRequest(["knowledge.search"])));
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/users/{id}/menus",new MenuGrantRequest(["knowledge.search","knowledge.practice.exam"])));
        var actor=await Data<Actor>(await user.GetAsync("/api/v1/auth/permissions"));Assert.Equal(2,actor.MenuCodes!.Count);Assert.Empty(actor.Permissions);
        Assert.Contains("knowledge.search",(await Data<JsonElement>(await user.GetAsync("/api/v1/web/navigation/menus"))).ToString());
        await Problem(await user.GetAsync("/api/v1/exam-study/projects"),HttpStatusCode.Forbidden,"forbidden");
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/users/{id}/menus",new MenuGrantRequest([])));
        actor=await Data<Actor>(await user.GetAsync("/api/v1/auth/permissions"));Assert.Equal(["knowledge.search"],actor.MenuCodes);
        await Data<Guid>(await admin.PutAsJsonAsync($"/api/v1/identity/roles/{role}",new RoleRequest("菜单读者",["study.search"])));
        Assert.Contains("knowledge.search",(await Data<Actor>(await user.GetAsync("/api/v1/auth/permissions"))).MenuCodes!); // business edits preserve menu claims
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/roles/{role}/menus",new MenuGrantRequest([])));
        Assert.DoesNotContain("knowledge.search",(await Data<JsonElement>(await user.GetAsync("/api/v1/web/navigation/menus"))).ToString());
        await Data<Devkit.Server.Domain.Workspace.StudyProject[]>(await user.GetAsync("/api/v1/exam-study/projects")); // revoking menus does not revoke APIs
        var view=(await Data<AccountView[]>(await admin.GetAsync("/api/v1/identity/users"))).Single(x=>x.Id==id);Assert.Empty(view.MenuCodes);Assert.Empty(view.EffectiveMenuCodes);
    }

    [Fact]
    public async Task Management_and_grant_endpoints_enforce_authentication_validation_and_delegation_limits()
    {
        using var factory=new DevkitApiFactory(builtInAdmin:true);using var admin=factory.CreateClient();var adminPair=await Login(admin);
        using var user=factory.CreateClient();
        await Problem(await user.GetAsync(Root+"/menus"),HttpStatusCode.Unauthorized,"unauthorized");
        await Problem(await user.PostAsJsonAsync(Root+"/compare",new MenuManifestRequest([])),HttpStatusCode.Unauthorized,"unauthorized");
        await Problem(await user.PutAsJsonAsync($"/api/v1/identity/users/{adminPair.User.Id}/menus",new MenuGrantRequest([])),HttpStatusCode.Unauthorized,"unauthorized");
        var role=await Data<Guid>(await admin.PostAsJsonAsync("/api/v1/identity/roles",new RoleRequest("授权员",["system.permissions.manage"])));
        var id=await Data<Guid>(await admin.PostAsJsonAsync("/api/v1/identity/users",new AccountRequest("grantor","grantor@example.test","StrongPassword123",[role])));
        await Login(user,"grantor","StrongPassword123");
        await Problem(await user.PostAsJsonAsync(Root+"/compare",new MenuManifestRequest([])),HttpStatusCode.Forbidden,"forbidden");
        await Problem(await user.PostAsJsonAsync(Root+"/sync",new MenuSyncRequest(1,[],[])),HttpStatusCode.Forbidden,"forbidden");
        await Problem(await user.PutAsJsonAsync($"/api/v1/identity/users/{id}/menus",new MenuGrantRequest(["knowledge.search"])),HttpStatusCode.Forbidden,"forbidden");
        await Problem(await user.PutAsJsonAsync($"/api/v1/identity/roles/{role}/menus",new MenuGrantRequest(["knowledge.search"])),HttpStatusCode.Forbidden,"forbidden");
        await Problem(await admin.PutAsJsonAsync($"/api/v1/identity/users/{id}/menus",new MenuGrantRequest(["knowledge"])),HttpStatusCode.BadRequest,"invalid_menu");
        await Problem(await admin.PutAsJsonAsync($"/api/v1/identity/users/{id}/menus",new MenuGrantRequest(null!)),HttpStatusCode.BadRequest,"invalid_menu");
        await Problem(await admin.PutAsJsonAsync($"/api/v1/identity/users/{Guid.NewGuid()}/menus",new MenuGrantRequest([])),HttpStatusCode.NotFound,"not_found");
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/roles/{role}/menus",new MenuGrantRequest(["knowledge.search"])));
        await Data<SuccessView>(await user.PutAsJsonAsync($"/api/v1/identity/users/{id}/menus",new MenuGrantRequest(["knowledge.search"])));
        await Problem(await user.PutAsJsonAsync($"/api/v1/identity/users/{adminPair.User.Id}/menus",new MenuGrantRequest([])),HttpStatusCode.Forbidden,"forbidden");
    }

    [Fact]
    public async Task Initial_migration_backfills_legacy_grants_once_and_never_regrants_revoked_menus()
    {
        var legacyRole=Guid.NewGuid();var legacyUser=Guid.NewGuid();
        using var factory=new DevkitApiFactory(builtInAdmin:true,prepareDatabase:db=>{
            db.Roles.Add(new Role { Id=legacyRole,Name="历史角色",NormalizedName="LEGACY" });
            db.Users.Add(new User { Id=legacyUser,UserName="legacy",NormalizedUserName="LEGACY",Email="legacy@example.test",NormalizedEmail="LEGACY@EXAMPLE.TEST",PasswordHash="unused-test-account" });
            db.RoleClaims.Add(new RoleClaim { RoleId=legacyRole,ClaimType="permission",ClaimValue="exam-study.access" });
            db.UserClaims.Add(new UserClaim { UserId=legacyUser,ClaimType="permission",ClaimValue="system.files.manage" });db.SaveChanges();
        });
        using var admin=factory.CreateClient();await Login(admin);
        var role=(await Data<RoleView[]>(await admin.GetAsync("/api/v1/identity/roles"))).Single(x=>x.Id==legacyRole);
        Assert.Contains("knowledge.search",role.MenuCodes);Assert.Contains("knowledge.practice.progress",role.MenuCodes);Assert.DoesNotContain("system.menus",role.MenuCodes);
        var user=(await Data<AccountView[]>(await admin.GetAsync("/api/v1/identity/users"))).Single(x=>x.Id==legacyUser);Assert.Equal(["system.files"],user.MenuCodes);
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/roles/{legacyRole}/menus",new MenuGrantRequest([])));
        using (var scope=factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<WebMenuService>().SnapshotAsync(default);
        role=(await Data<RoleView[]>(await admin.GetAsync("/api/v1/identity/roles"))).Single(x=>x.Id==legacyRole);Assert.Empty(role.MenuCodes);
        var fresh=await Data<Guid>(await admin.PostAsJsonAsync("/api/v1/identity/roles",new RoleRequest("新角色",["exam-study.access"])));
        Assert.Empty((await Data<RoleView[]>(await admin.GetAsync("/api/v1/identity/roles"))).Single(x=>x.Id==fresh).MenuCodes);
    }

    [Theory]
    [InlineData("new-page")]
    [InlineData("new_page")]
    [InlineData("new..page")]
    [InlineData("new.1page")]
    [InlineData("New.page")]
    [InlineData("new page")]
    [InlineData("new\n")]
    [InlineData(".new")]
    [InlineData("new.")]
    public async Task Menu_codes_require_lowercase_business_segments(string code)
    {
        using var factory=new DevkitApiFactory(builtInAdmin:true);using var admin=factory.CreateClient();await Login(admin);
        var tree=await Tree(admin);
        await Problem(await admin.PostAsJsonAsync(Root+"/compare",new MenuManifestRequest(Manifest(tree).Append(Module(code)).ToArray())),HttpStatusCode.BadRequest,"invalid_menu");
        await Problem(await admin.PostAsJsonAsync(Root+"/sync",new MenuSyncRequest(tree.Version,Manifest(tree).Append(Module(code)).ToArray(),[new(code,[])])),HttpStatusCode.BadRequest,"invalid_menu");
        await Problem(await admin.PostAsJsonAsync(Root+"/menus",new DirectoryCreateRequest(tree.Version,code,"非法编码",null,null,1)),HttpStatusCode.BadRequest,"invalid_menu");
        Assert.Equal(tree.Version,(await Tree(admin)).Version);
    }
}
