using Devkit.Services.Interfaces;
using LibGit2Sharp;

namespace Devkit.Services;

public class GitService : IGitService
{
    #region IGitService Members
    public void Clone(string url, string path)
    {
        throw new NotImplementedException();
    }
    public void Pull(string path)
    {
        throw new NotImplementedException();
    }
    public void Commit(string path, string message)
    {
        throw new NotImplementedException();
    }
    public void Push(string path)
    {
        throw new NotImplementedException();
    }
    public bool HasRemoteUpdates(string repoPath, string branchName = "main", string remoteName = "origin")
    {
        using var repo = new Repository(repoPath);

        // 1. fetch
        var remote = repo.Network.Remotes[remoteName];

        Commands.Fetch(
            repo,
            remote.Name,
            remote.FetchRefSpecs.Select(x => x.Specification),
            new FetchOptions
            {
                CredentialsProvider = (_, _, _) =>
                    new UsernamePasswordCredentials
                    {
                        Username = "your_token",
                        Password = ""
                    }
            },
            null
        );

        // 2. 本地 & 远端分支
        var localBranch = repo.Branches[branchName];
        var remoteBranch = repo.Branches[$"{remoteName}/{branchName}"];

        if (localBranch == null || remoteBranch == null)
            return false;

        // 3. 对比 commit
        var divergence = repo.ObjectDatabase
            .CalculateHistoryDivergence(localBranch.Tip, remoteBranch.Tip);

        // AheadBy: 本地领先
        // BehindBy: 本地落后（远端有新提交）
        return divergence?.BehindBy > 0;
    }
    #endregion
}
