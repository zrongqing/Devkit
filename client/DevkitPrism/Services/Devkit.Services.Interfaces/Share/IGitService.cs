namespace Devkit.Services.Interfaces;

public interface IGitService
{
    /// <summary>
    /// 克隆
    /// </summary>
    void Clone(string url, string path);
    /// <summary>
    /// 拉取
    /// </summary>
    void Pull(string path);
    /// <summary>
    /// 提交
    /// </summary>
    void Commit(string path, string message);
    /// <summary>
    /// 推送
    /// </summary>
    void Push(string path);

    /// <summary>
    /// 远端是否更新
    /// </summary>
    bool HasRemoteUpdates(string repoPath, string branchName, string remoteName);
}
