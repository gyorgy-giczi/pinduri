using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pinduri
{
    public class Scm
    {
        public string RootPath { get; init; }

        private string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetRelativePath(RootPath, Path.GetFullPath(path))).Replace("\\", "/");
        private string Hash(byte[] value) => value.Aggregate(3074457345618258791ul, (acc, x) => (acc + x) * 3074457345618258799ul).Map(x => BitConverter.GetBytes(x)).Map(x => Convert.ToHexString(x).ToLowerInvariant()); // https://stackoverflow.com/questions/9545619/a-fast-hash-function-for-string-in-c-sharp
        private string EnsurePath(string path) => path.Tap(x => Directory.CreateDirectory(Path.GetDirectoryName(x)));

        public (string Root, (string Name, string Position, string MergeSource) Branch, IEnumerable<(string File, string Status)> Stage, IEnumerable<(string File, string Status)> Workspace) Status() =>
            GetBranch().Map(branch => (Root: RootPath, Branch: branch, Stage: RetrieveText("stage").ToHashSet(), Files: GetFileStatuses(branch.Position).ToHashSet()))
            .Map(x => (Root: RootPath, Branch: x.Branch, Stage: x.Files.Where(y => x.Stage.Contains(y.File)), Workspace: x.Files.Where(y => y.Status != "unchanged" && !x.Stage.Contains(y.File))));

        private IEnumerable<string> GetRepositoryFiles(string commitId) => GetState(commitId).Where(x => x.Value != null).Select(x => x.Key);

        private IEnumerable<string> GetWorkspaceFiles() =>
            Directory.GetFiles(".", "*", new EnumerationOptions() { AttributesToSkip = FileAttributes.Hidden, RecurseSubdirectories = true }).Select(x => NormalizePath(x))
            .Map(files => RetrieveText("../.scm-ignore").Append(Path.Combine(RootPath, ".pinduri-scm")).Select(x => NormalizePath(x))
            .Map(ignore => files.Where(x => !ignore.Any(i => x == i || x.StartsWith(i.TrimEnd('/') + "/")))));

        private IEnumerable<(string File, string Status)> GetFileStatuses(string commitId) =>
            GetWorkspaceFiles().ToHashSet().Map(workspaceFiles =>
                GetRepositoryFiles(commitId).ToHashSet().Map(repoFiles =>
                    workspaceFiles.Intersect(repoFiles).Select(x => (File: x, Status: Hash(File.ReadAllBytes(x)) == GetState(commitId)[x].Map(x => Hash(File.ReadAllBytes(Path.Combine(RootPath, ".pinduri-scm/data", x.Substring(0, 2), x)))) ? "unchanged" : "changed"))
                    .Concat(workspaceFiles.Except(repoFiles).Select(x => (File: x, Status: "new")))
                    .Concat(repoFiles.Except(workspaceFiles).Select(x => (File: x, Status: "deleted")))
                )
            );

        private string StoreText(string file, IEnumerable<string> content) => EnsurePath(Path.Combine(RootPath, ".pinduri-scm", file)).Tap(f => File.WriteAllLines(f, content ?? new string[0]));
        private IEnumerable<string> RetrieveText(string file) => Path.Combine(RootPath, ".pinduri-scm", file).Map(f => File.Exists(f) ? File.ReadAllLines(f) : new string[0]);
        public void CreateBranch(string name, string position = null) => StoreText($"branches/{name}", new string[] { position ?? GetBranch().Position }).Tap(x => Checkout(name));
        public (string Name, string Position, string MergeSource) GetBranch(string name = null) => (name ?? RetrieveText("current-branch").FirstOrDefault() ?? "default").Map(name => RetrieveText($"branches/{name}").ToArray().Map(b => (Name: name, Position: b.FirstOrDefault() ?? Hash(Guid.Empty.ToByteArray()), MergeSource: b.Length > 1 ? b[1] : null)));
        public IEnumerable<string> Stage(string path) => RetrieveText("stage").Append(NormalizePath(path)).Distinct().Tap(x => StoreText("stage", x));
        public IEnumerable<string> Unstage(string path) => NormalizePath(path).Map(path => RetrieveText("stage").Where(x => x != path).Tap(x => StoreText("stage", x)));

        public string Commit(string message) =>
            Hash(Guid.NewGuid().ToByteArray()).Tap(commitId =>
                GetBranch().Map(branch =>
                    RetrieveText("stage")
                    .Select(x => File.Exists(x)
                        ? string.Join(" ", "add", x, File.ReadAllBytes(x).Map(fileData => fileData.Map(x => Hash(x)).Tap(hash => File.Copy(x, EnsurePath(Path.Combine(RootPath, ".pinduri-scm/data", hash.Substring(0, 2), hash)), true))))
                        : string.Join(" ", "delete", x)
                    )
                    .Tap(x => StoreText($"commits/{commitId}", x))
                    .Tap(x => StoreText("commitlog", RetrieveText("commitLog").Append($"{DateTime.UtcNow.ToString("o")} {commitId} {branch.Position}{(branch.MergeSource != null ? $",{branch.MergeSource}" : "")} {Uri.EscapeDataString(message)}")))
                    .Tap(x => StoreText($"branches/{branch.Name}", new string[] { commitId }))
                    .Tap(x => StoreText("stage", new string[0]))
                )
            );

        private IEnumerable<(string TimeStamp, string CommitId, string ParentId, string Message)> Traverse(Dictionary<string, (string TimeStamp, string CommitId, string ParentId, string Message)> commits, string id) =>
            commits.ContainsKey(id) ? Traverse(commits, commits[id].ParentId).Append(commits[id]) : new (string TimeStamp, string CommitId, string ParentId, string Message)[0];

        public IEnumerable<(string TimeStamp, string CommitId, string ParentId, string Message)> GetHistory(string commitId) =>
            RetrieveText("commitlog").Select(x => x.Split(" ").Map(y => (TimeStamp: y[0], CommitId: y[1], ParentId: y[2].Split(",").FirstOrDefault(), Message: Uri.UnescapeDataString(y[3])))).ToDictionary(x => x.CommitId).Map(x => Traverse(x, commitId ?? GetBranch().Position));

        public Dictionary<string, string> GetState(string branchNameOrCommitId) =>
            GetHistory(GetBranch(branchNameOrCommitId).Position.Map(x => x == Hash(Guid.Empty.ToByteArray()) ? branchNameOrCommitId : x))
            .Aggregate(new Dictionary<string, string>(), (acc, commit) => acc.Tap(acc => RetrieveText($"commits/{commit.CommitId}").Select(x => x.Split(" ").Map(x => x[0] == "add" ? acc[x[1]] = x[2] : x[0] == "delete" ? acc[x[1]] = null : default)).ToList()));

        private IEnumerable<(string File, string Action)> SyncWorkspace(Dictionary<string, string> newState) =>
            GetRepositoryFiles(null).Except(newState.Keys).Select(x => (File: x, Value: (string)null)).Concat(newState.Select(x => (File: x.Key, Value: x.Value)))
            .Select(x => x.Value != null ? x.Tap(x => File.Copy(EnsurePath(Path.Combine(RootPath, ".pinduri-scm/data", x.Value.Substring(0, 2), x.Value)), x.File, true)) : x.Tap(x => File.Delete(x.File)))
            .Select(x => (File: x.File, Action: x.Value != null ? "updated" : "removed")).ToList();

        public IEnumerable<(string File, string Action)> Checkout(string branchName) => GetBranch(branchName).Map(branch => branch.Map(x => SyncWorkspace(GetState(x.Position))).Tap(x => StoreText($"current-branch", new string[] { branch.Name })).Tap(x => StoreText("stage", new string[0])));
        public Func<IEnumerable<string>, IEnumerable<string>, IEnumerable<string>> DiffContent { get; init; } = new Func<IEnumerable<string>, IEnumerable<string>, IEnumerable<string>>((a, b) => a.Select(x => "- " + x).Concat(b.Select(x => "+ " + x)));
        public IEnumerable<string> Diff(string file, string commitId = null) => DiffContent(GetState(commitId).Map(x => x.ContainsKey(file) && x[file] != null ? File.ReadAllLines(Path.Combine(RootPath, ".pinduri-scm/data", x[file].Substring(0, 2), x[file])) : new string[0]), File.Exists(file) ? File.ReadLines(file) : new string[0]);

        private string FindCommonAncestor(string commitIdA, string commitIdB) =>
            GetHistory(commitIdA).Select(x => x.CommitId).ToHashSet()
            .Map(commits => GetHistory(commitIdB).LastOrDefault(x => commits.Contains(x.CommitId)).CommitId ?? Hash(Guid.Empty.ToByteArray()));

        public IEnumerable<(string Name, IEnumerable<string> Merged)> Merge(string branchName) =>
            (BranchA: GetBranch(null), BranchB: GetBranch(branchName)).Tap(x => StoreText($"branches/{x.BranchA.Name}", new string[] { x.BranchA.Position, x.BranchB.Position }))
            .Map(b =>
                b.Map(x => (StateB: GetState(b.BranchB.Position), StateO: GetState(FindCommonAncestor(b.BranchA.Position, b.BranchB.Position))))
                .Map(x => x.StateB.Where(x => x.Value != null).Select(b => (Name: b.Key, DataIdB: b.Value, DataIdO: x.StateO.FirstOrDefault(x => x.Key == b.Key).Value)))
                .Select(x =>
                    new Func<string, string[]>(f => f != null ? Path.Combine(RootPath, ".pinduri-scm/data", f.Substring(0, 2), f).Map(x => File.Exists(x) ? File.ReadAllLines(x) : new string[0]) : new string[0])
                    .Map(readFileData => (Name: x.Name, ContentA: File.Exists(x.Name) ? File.ReadAllLines(x.Name) : new string[0], ContentB: readFileData(x.DataIdB), ContentO: readFileData(x.DataIdO))))
                .Select(x => (Name: x.Name, Merged: MergeContent(x.ContentO, x.ContentA, x.ContentB).Tap(y => File.WriteAllLines(x.Name, y)))).ToList()
            ).Select(x => x.Tap(x => Stage(x.Name))).ToList();

        public Func<IEnumerable<string>, IEnumerable<string>, IEnumerable<string>, IEnumerable<string>> MergeContent = new Func<IEnumerable<string>, IEnumerable<string>, IEnumerable<string>, IEnumerable<string>>((o, a, b) => a.Prepend("<<<<<<<<").Append("========").Concat(b).Append(">>>>>>>>"));

        public void Cli(string[] args) =>
            new Func<string, string[], bool>[]
            {
                (c, a) => c.Map(x => x == "branch" && a.Length >= 1 && a.Length <= 2 ? true.Tap(x => CreateBranch(a[0], a.Length > 1 ? a[1]: null)) : false),
                (c, a) => c.Map(x => x == "checkout" && a.Length <= 1 ? true.Tap(x => Checkout(a.Length == 1 ? a[0] : GetBranch().Name).Select(x => x.Tap(x => Console.WriteLine($"{x.Action} {x.File}")))) : false),
                (c, a) => c.Map(x => x == "commit" && a.Length == 1 ? true.Tap(x => Commit(a[0]).Tap(x => Console.WriteLine(x))) : false),
                (c, a) => c.Map(x => x == "diff" && a.Length <= 2 ? true.Tap(x => a.Append(null).ToArray().Map(a => Diff(a[0], a[1]).Select(x => x.Tap(x => Console.WriteLine(x))).ToList())) : false),
                (c, a) => c.Map(x => x == "history" && a.Length <= 1 ? true.Tap(x => GetHistory(GetBranch(a.Length == 1 ? a[0] : null).Position).Select(x => RetrieveText($"commits/{x.CommitId}").Select(x => "    " + x).Prepend($"{x.CommitId} {x.TimeStamp} {x.Message}")).SelectMany(x => x).Select(x => x.Tap(x => Console.WriteLine(x))).ToList()) : false),
                (c, a) => c.Map(x => x == "merge" && a.Length == 1 ? true.Tap(x => Merge(a[0]).Select(x=> x.Tap(x=> Console.WriteLine($"merged {x.Name}"))).ToList()) : false),
                (c, a) => c.Map(x => x == "stage" && a.Length == 1 ? true.Tap(x => Stage(a[0])) : false),
                (c, a) => c.Map(x => x == "status" && a.Length == 0 ? true.Tap(x => Status().Tap(x => Console.WriteLine($"Root: {x.Root}\nBranch: {x.Branch}\n\n    staged files:\n{string.Join("\n", x.Stage.Select(y => $"        {y.Status} {y.File}"))}\n\n    unstaged files:\n{string.Join("\n", x.Workspace.Select(y=> $"        {y.Status} {y.File}"))}"))) : false),
                (c, a) => c.Map(x => x == "unstage" && a.Length == 1 ? true.Tap(x => Unstage(a[0])) : false),
                (c, a) => c.Map(x => true).Tap(x => Console.WriteLine("usage: pinduri-scm <command> [parameters]\n\ncommands:\n\nbranch <branchname> [commit]\ncheckout [branchname]\ncommit <message>\ndiff <file> [branchname]\nhelp\nhistory [commit]\nmerge <branchname>\nstage <file>\nstatus\nunstage <file>")),
            }.FirstOrDefault(x => x.Invoke(args[0].ToLowerInvariant(), args[1..]));
    }
} // line #109
