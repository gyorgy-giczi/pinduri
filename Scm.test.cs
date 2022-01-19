using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pinduri.Tests
{
    public class ScmTests
    {
        static string WorkDirectory = Path.GetFullPath("./scmtest-tmp");

        static Scm target = new Scm() { RootPath = WorkDirectory };

        static void ResetWorkDirectory()
        {
            if (Directory.Exists(WorkDirectory))
            {
                Directory.Delete(WorkDirectory, true);
            }

            Directory.CreateDirectory(WorkDirectory);
            Directory.SetCurrentDirectory(WorkDirectory);
        }

        static string Serialize(IEnumerable<(string, string)> items) => string.Join(",", items.OrderBy(x => x.Item1).ThenBy(x => x.Item2).Select(x => x.ToString()));
        static string CommitChanges(params (string File, string Content)[] files) { return CommitChanges("", files); }
        static string CommitChanges(string commitMessage, params (string File, string Content)[] files)
        {
            foreach (var file in files)
            {
                if (file.Content == null)
                {
                    File.Delete(file.File);
                }
                else
                {
                    File.WriteAllText(file.File, file.Content);
                }
                target.Stage(file.File);
            }

            return target.Commit(commitMessage);
        }

        class BranchTest
        {
            public void BeforeEach() => ResetWorkDirectory();

            public void ShouldGetCurrentBranch_When_NameIsNull()
            {
                Assert.AreEqual((Name: "default", Position: "67d211dde2932b6f", MergeSource: null), target.GetBranch(null));

                var commitId1 = target.Commit("an empty commit #1");
                Assert.AreEqual((Name: "default", Position: commitId1, MergeSource: null), target.GetBranch(null));

                target.CreateBranch("roarr");
                Assert.AreEqual((Name: "roarr", Position: commitId1, MergeSource: null), target.GetBranch(null));

                var commitId2 = target.Commit("an empty commit on branch roarr");
                Assert.AreEqual((Name: "roarr", Position: commitId2, MergeSource: null), target.GetBranch(null));
            }

            public void ShouldGetBranch_When_NameIsEmptyOrNotExist()
            {
                Assert.AreEqual((Name: "", Position: "67d211dde2932b6f", MergeSource: null), target.GetBranch(""));
                Assert.AreEqual((Name: "not-exists-1", Position: "67d211dde2932b6f", MergeSource: null), target.GetBranch("not-exists-1"));

                var commitId1 = target.Commit("an empty commit #1");
                Assert.AreEqual((Name: "", Position: "67d211dde2932b6f", MergeSource: null), target.GetBranch(""));
                Assert.AreEqual((Name: "not-exists-1", Position: "67d211dde2932b6f", MergeSource: null), target.GetBranch("not-exists-1"));

                target.CreateBranch("roarr");
                Assert.AreEqual((Name: "", Position: "67d211dde2932b6f", MergeSource: null), target.GetBranch(""));
                Assert.AreEqual((Name: "not-exists-1", Position: "67d211dde2932b6f", MergeSource: null), target.GetBranch("not-exists-1"));

                var commitId2 = target.Commit("an empty commit on branch roarr");
                Assert.AreEqual((Name: "", Position: "67d211dde2932b6f", MergeSource: null), target.GetBranch(""));
                Assert.AreEqual((Name: "not-exists-1", Position: "67d211dde2932b6f", MergeSource: null), target.GetBranch("not-exists-1"));
            }

            public void ShouldGetBranch_When_NameExists()
            {
                var commitId1 = target.Commit("an empty commit #1");

                target.CreateBranch("roarr");
                Assert.AreEqual((Name: "roarr", Position: commitId1, MergeSource: null), target.GetBranch("roarr"));

                var commitId2 = target.Commit("an empty commit on branch roarr");
                Assert.AreEqual((Name: "roarr", Position: commitId2, MergeSource: null), target.GetBranch("roarr"));
            }

            public void ShouldCreateBranchFail_When_NameIsNullOrEmpty()
            {
                Assert.Throws<UnauthorizedAccessException>(() => target.CreateBranch(null));
                Assert.Throws<UnauthorizedAccessException>(() => target.CreateBranch(""));
            }

            public void ShouldCreateBranch_When_NameIsValid()
            {
                var commitId1 = target.Commit("an empty commit #1");

                target.CreateBranch("roarr");
                Assert.AreEqual((Name: "roarr", Position: commitId1, MergeSource: null), target.GetBranch("roarr"));
            }

            public void ShouldCreateBranch_When_NameIsWhitespace()
            {
                var commitId1 = target.Commit("an empty commit #1");

                target.CreateBranch("\t \t");
                Assert.AreEqual((Name: "\t \t", Position: commitId1, MergeSource: null), target.GetBranch("\t \t"));
            }

            public void ShouldCreateBranchOverwitePosition_When_NameAlreadyExists()
            {
                var commitId1 = target.Commit("an empty commit #1");

                target.CreateBranch("roarr");
                Assert.AreEqual((Name: "roarr", Position: commitId1, MergeSource: null), target.GetBranch("roarr"));

                target.CreateBranch("roarr");
                Assert.AreEqual((Name: "roarr", Position: commitId1, MergeSource: null), target.GetBranch("roarr"));

                var commitId2 = target.Commit("an empty commit #2 on branch default");
                target.CreateBranch("roarr");
                Assert.AreEqual((Name: "roarr", Position: commitId2, MergeSource: null), target.GetBranch("roarr"));

                target.Checkout("roarr");

                target.CreateBranch("roarr");
                Assert.AreEqual((Name: "roarr", Position: commitId2, MergeSource: null), target.GetBranch("roarr"));

                var commitId3 = target.Commit("an empty commit #3 on branch roarr");

                target.CreateBranch("roarr");
                Assert.AreEqual((Name: "roarr", Position: commitId3, MergeSource: null), target.GetBranch("roarr"));
            }

            public void ShouldCreateBranch_When_PositionIsEmpty()
            {
                target.CreateBranch("roarr", "");
                Assert.AreEqual((Name: "roarr", Position: "", MergeSource: null), target.GetBranch("roarr"));

                target.Checkout("roarr");
            }

            public void ShouldCreateBranch_When_PositionIsInvalid()
            {
                target.CreateBranch("roarr", "not-exists");
                Assert.AreEqual((Name: "roarr", Position: "not-exists", MergeSource: null), target.GetBranch("roarr"));
            }
        }

        class CheckoutTest
        {
            public void BeforeEach() => ResetWorkDirectory();

            public void ShouldCheckoutCurrentBranch_When_BranchNameIsNull()
            {
                Assert.AreEqual("", Serialize(target.Checkout(null)));
                Assert.AreEqual((Name: "default", Position: "67d211dde2932b6f", MergeSource: null), target.Status().Branch);

                target.CreateBranch("branch-1");
                var commitId = target.Commit("");

                Assert.AreEqual("", Serialize(target.Checkout(null)));
                Assert.AreEqual((Name: "branch-1", Position: commitId, MergeSource: null), target.Status().Branch);
            }

            public void ShouldCheckout_When_BranchNameDoesNotExist()
            {
                Assert.AreEqual("", Serialize(target.Checkout("not-exists")));
                Assert.AreEqual((Name: "not-exists", Position: "67d211dde2932b6f", MergeSource: null), target.Status().Branch);

                target.CreateBranch("branch-1");
                var commitId = target.Commit("");

                Assert.AreEqual("", Serialize(target.Checkout("not-exists")));
                Assert.AreEqual((Name: "not-exists", Position: "67d211dde2932b6f", MergeSource: null), target.Status().Branch);
            }

            public void ShouldCheckoutUpdateCurrentBranch()
            {
                target.CreateBranch("branch-1");
                target.CreateBranch("branch-2");

                target.Checkout("branch-1");
                var commitId1 = target.Commit("");

                target.Checkout("branch-2");
                var commitId2 = target.Commit("");

                target.Checkout("branch-1");
                Assert.AreEqual((Name: "branch-1", Position: commitId1, MergeSource: null), target.Status().Branch);

                target.Checkout("branch-2");
                Assert.AreEqual((Name: "branch-2", Position: commitId2, MergeSource: null), target.Status().Branch);
            }

            public void ShouldCheckoutClearStage()
            {
                target.Checkout(null);
                Assert.AreEqual("", Serialize(target.Status().Stage));

                File.WriteAllText("file-1", "roarr");
                target.Stage("file-1");
                target.Stage("file-2");

                target.Checkout(null);
                Assert.AreEqual("", Serialize(target.Status().Stage));
            }

            public void ShouldCheckoutKeepNewFiles()
            {
                target.CreateBranch("branch-1");
                target.CreateBranch("branch-2");

                Assert.AreEqual("", Serialize(target.Checkout("branch-1")));
                CommitChanges("", ("file-1", "roarr"));

                File.WriteAllText("file-new", "new");
                Assert.AreEqual("(file-new, new)", Serialize(target.Status().Workspace));
                Assert.AreEqual("new", File.ReadAllText("file-new"));

                Assert.AreEqual("(file-1, removed)", Serialize(target.Checkout("branch-2")));
                Assert.AreEqual("(file-new, new)", Serialize(target.Status().Workspace));
                Assert.AreEqual(true, File.Exists("file-new"));
                Assert.AreEqual("new", File.ReadAllText("file-new"));
            }

            public void ShouldCheckoutDeleteFiles_When_DoNotExistInTargetBranch()
            {
                target.CreateBranch("branch-1");
                target.CreateBranch("branch-2");

                Assert.AreEqual("", Serialize(target.Checkout("branch-1")));
                CommitChanges("", ("file-1", "roarr-file-1"), ("file-2", "roarr-file-2"));

                File.WriteAllText("file-2", "roarr-changed");

                Assert.AreEqual("(file-1, removed),(file-2, removed)", Serialize(target.Checkout("branch-2")));
                Assert.AreEqual("", Serialize(target.Status().Workspace));
                Assert.AreEqual(false, File.Exists("file-1"));
                Assert.AreEqual(false, File.Exists("file-2"));
            }

            public void ShouldCheckoutUpdateFiles_When_ExistInTargetBranch()
            {
                target.CreateBranch("branch-1");
                target.CreateBranch("branch-2");

                Assert.AreEqual("", Serialize(target.Checkout("branch-1")));
                CommitChanges("", ("file-1", "roarr-branch-1"), ("file-2", "roarr-branch-1"));

                Assert.AreEqual("(file-1, removed),(file-2, removed)", Serialize(target.Checkout("branch-2")));
                CommitChanges("", ("file-1", "roarr-branch-2"), ("file-2", "roarr-branch-2"));

                Assert.AreEqual("(file-1, updated),(file-2, updated)", Serialize(target.Checkout("branch-1")));
                Assert.AreEqual("", Serialize(target.Status().Workspace));
                Assert.AreEqual(true, File.Exists("file-1"));
                Assert.AreEqual(true, File.Exists("file-2"));
                Assert.AreEqual("roarr-branch-1", File.ReadAllText("file-1"));
                Assert.AreEqual("roarr-branch-1", File.ReadAllText("file-2"));
            }

            public void ShouldCheckoutRestoreDeletedFiles()
            {
                CommitChanges("", ("file-1", "roarr-file-1"), ("file-2", "roarr-file-2"));

                File.Delete("file-1");
                Assert.AreEqual("(file-1, deleted)", Serialize(target.Status().Workspace));

                Assert.AreEqual("(file-1, updated),(file-2, updated)", Serialize(target.Checkout(null)));
                Assert.AreEqual("", Serialize(target.Status().Workspace));
                Assert.AreEqual(true, File.Exists("file-1"));
                Assert.AreEqual(true, File.Exists("file-2"));
                Assert.AreEqual("roarr-file-1", File.ReadAllText("file-1"));
                Assert.AreEqual("roarr-file-2", File.ReadAllText("file-2"));
            }

            public void ShouldCheckout()
            {
                // prepare common ancestor
                var commitIdCommon = CommitChanges("common commit",
                    ("changed-in-both", "roarr-initial"),
                    ("changed-in-branch-1", "roarr-initial"),
                    ("changed-in-branch-2", "roarr-initial"),
                    ("deleted-in-branch-1", "roarr-initial"),
                    ("deleted-in-branch-2", "roarr-initial"),
                    ("unchanged-in-both", "roarr-initial")
                );

                target.CreateBranch("branch-1");
                target.CreateBranch("branch-2");

                // prepare branch-1
                Assert.AreEqual("(changed-in-both, updated),(changed-in-branch-1, updated),(changed-in-branch-2, updated),(deleted-in-branch-1, updated),(deleted-in-branch-2, updated),(unchanged-in-both, updated)", Serialize(target.Checkout("branch-1")));
                var commitIdBranch1 = CommitChanges("commit on branch-1",
                    ("added-in-branch-1", "roarr-branch-1"),
                    ("changed-in-both", "roarr-branch-1"),
                    ("changed-in-branch-1", "roarr-branch-1"),
                    ("deleted-in-branch-1", null)
                );

                // prepare branch-2
                Assert.AreEqual("(added-in-branch-1, removed),(changed-in-both, updated),(changed-in-branch-1, updated),(changed-in-branch-2, updated),(deleted-in-branch-1, updated),(deleted-in-branch-2, updated),(unchanged-in-both, updated)", Serialize(target.Checkout("branch-2")));
                var commitIdBranch2 = CommitChanges("commit on branch-2",
                    ("added-in-branch-2", "roarr-branch-2"),
                    ("changed-in-both", "roarr-branch-2"),
                    ("changed-in-branch-2", "roarr-branch-2"),
                    ("deleted-in-branch-2", null)
                );

                // test checkout branch-1
                Assert.AreEqual("(added-in-branch-1, updated),(added-in-branch-2, removed),(changed-in-both, updated),(changed-in-branch-1, updated),(changed-in-branch-2, updated),(deleted-in-branch-1, removed),(deleted-in-branch-2, updated),(unchanged-in-both, updated)", Serialize(target.Checkout("branch-1")));
                Assert.AreEqual("", Serialize(target.Status().Workspace));
                Assert.AreEqual(6, Directory.GetFiles(".").Count());
                new string[] { "changed-in-branch-1", "deleted-in-branch-2", "changed-in-both", "added-in-branch-1", "unchanged-in-both", "changed-in-branch-2" }
                    .ToList().ForEach(x => Assert.AreEqual(true, File.Exists(x), x));

                new string[] { "changed-in-branch-1", "changed-in-both", "added-in-branch-1", }
                    .ToList().ForEach(x => Assert.AreEqual("roarr-branch-1", File.ReadAllText(x), x));

                new string[] { "deleted-in-branch-2", "unchanged-in-both", "changed-in-branch-2" }
                    .ToList().ForEach(x => Assert.AreEqual("roarr-initial", File.ReadAllText(x), x));

                // test checkout branch-2
                Assert.AreEqual("(added-in-branch-1, removed),(added-in-branch-2, updated),(changed-in-both, updated),(changed-in-branch-1, updated),(changed-in-branch-2, updated),(deleted-in-branch-1, updated),(deleted-in-branch-2, removed),(unchanged-in-both, updated)", Serialize(target.Checkout("branch-2")));
                Assert.AreEqual("", Serialize(target.Status().Workspace));
                Assert.AreEqual(6, Directory.GetFiles(".").Count());
                new string[] { "added-in-branch-2", "changed-in-both", "changed-in-branch-1", "changed-in-branch-2", "deleted-in-branch-1", "unchanged-in-both", }
                    .ToList().ForEach(x => Assert.AreEqual(true, File.Exists(x), x));

                new string[] { "added-in-branch-2", "changed-in-both", "changed-in-branch-2" }
                    .ToList().ForEach(x => Assert.AreEqual("roarr-branch-2", File.ReadAllText(x), x));

                new string[] { "changed-in-branch-1", "deleted-in-branch-1", "unchanged-in-both", }
                    .ToList().ForEach(x => Assert.AreEqual("roarr-initial", File.ReadAllText(x), x));

                // test checkout default (common ancestor)
                Assert.AreEqual("(added-in-branch-2, removed),(changed-in-both, updated),(changed-in-branch-1, updated),(changed-in-branch-2, updated),(deleted-in-branch-1, updated),(deleted-in-branch-2, updated),(unchanged-in-both, updated)", Serialize(target.Checkout("default")));
                Assert.AreEqual("", Serialize(target.Status().Workspace));
                Assert.AreEqual(6, Directory.GetFiles(".").Count());
                new string[] { "changed-in-both", "changed-in-branch-1", "changed-in-branch-2", "deleted-in-branch-1", "unchanged-in-both", }
                    .ToList().ForEach(x => Assert.AreEqual(true, File.Exists(x), x));

                new string[] { "changed-in-both", "changed-in-branch-1", "changed-in-branch-2", "deleted-in-branch-1", "deleted-in-branch-2", "unchanged-in-both", }
                    .ToList().ForEach(x => Assert.AreEqual("roarr-initial", File.ReadAllText(x), x));
            }
        }

        class CommitTest
        {
            public void BeforeEach() => ResetWorkDirectory();

            public void ShouldCommitFail_When_MessageIsNull()
            {
                Assert.Throws<ArgumentNullException>(() => target.Commit(null));
            }

            public void ShouldCommit_When_MessageIsEmpty()
            {
                var commitId = target.Commit("");
                Assert.IsNotNull(commitId);
                Assert.AreEqual(16, commitId.Length);
            }

            public void ShouldCommit_When_StageIsEmpty()
            {
                var commitId = target.Commit("commit #1");
                Assert.IsNotNull(commitId);
                Assert.AreEqual(16, commitId.Length);
            }

            public void ShouldCommitClearStage()
            {
                File.WriteAllText("file1", "roarr");
                target.Stage("file1");
                Assert.AreEqual("(file1, new)", Serialize(target.Status().Stage));

                target.Commit("");
                Assert.AreEqual("", Serialize(target.Status().Stage));
            }

            public void ShouldCommitOnlyStagedFiles()
            {
                File.WriteAllText("file1", "roarr");
                File.WriteAllText("file2", "roarr");
                target.Stage("file1");
                // target.Stage("file2"); // Do not stage! 

                target.Commit("");
                Assert.AreEqual("(file2, new)", Serialize(target.Status().Workspace));
            }
        }

        class DiffTest
        {
            // These tests are for the default silly diff algorithm

            public void BeforeEach() => ResetWorkDirectory();

            public void ShouldDiffWithCurrentBranch_When_FileDoesNotExist()
            {
                Assert.AreEqual("", string.Join(",", target.Diff("not-exists", null)));
            }

            public void ShouldDiffWithCurrentBranch_When_FileIsNew()
            {
                File.WriteAllText("file1", "1\n2\n3\n");

                Assert.AreEqual("+ 1,+ 2,+ 3", string.Join(",", target.Diff("file1", null)));
            }

            public void ShouldDiffWithCurrentBranch_When_FileIsDeleted()
            {
                File.WriteAllText("file1", "a\nb\nc\n");
                target.Stage("file1");
                target.Commit("");

                File.Delete("file1");

                Assert.AreEqual("- a,- b,- c", string.Join(",", target.Diff("file1", null)));
            }

            public void ShouldDiffWithCurrentBranch_When_FileIsChanged()
            {
                File.WriteAllText("file1", "a\nb\nc\n");
                target.Stage("file1");
                target.Commit("");

                File.WriteAllText("file1", "1\n2\n3\n");

                Assert.AreEqual("- a,- b,- c,+ 1,+ 2,+ 3", string.Join(",", target.Diff("file1", null)));
            }

            public void ShouldDiffWithCurrentBranch_When_FileIsUnchanged()
            {
                File.WriteAllText("file1", "a\nb\nc\n");
                target.Stage("file1");
                target.Commit("");

                Assert.AreEqual("- a,- b,- c,+ a,+ b,+ c", string.Join(",", target.Diff("file1", null)));
            }

            public void ShouldDiffWithCurrentBranch_When_RepositoryFileIsDeleted_And_WorkspaceFileDoesNotExist()
            {
                File.WriteAllText("file1", "a\nb\nc\n");
                target.Stage("file1");
                target.Commit("");

                File.Delete("file1");
                target.Stage("file1");
                target.Commit("");

                Assert.AreEqual("", string.Join(",", target.Diff("file1", null)));
            }

            public void ShouldDiffWithCurrentBranch_When_RepositoryFileIsDeleted_And_WorkspaceFileIsNew()
            {
                File.WriteAllText("file1", "a\nb\nc\n");
                target.Stage("file1");
                target.Commit("");

                File.Delete("file1");
                target.Stage("file1");
                target.Commit("");

                File.WriteAllText("file1", "1\n2\n3\n");

                Assert.AreEqual("+ 1,+ 2,+ 3", string.Join(",", target.Diff("file1", null)));
            }

            public void ShouldDiffWithSpecificCommit_When_CommitDoesNotExist()
            {
                File.WriteAllText("file1", "1\n2\n3\n");

                Assert.AreEqual("+ 1,+ 2,+ 3", string.Join(",", target.Diff("file1", "commit-not-exists")));
            }

            public void ShouldDiffWithSpecificCommit_When_FileDoesNotExist_And_CommitDoesNotExist()
            {
                Assert.AreEqual("", string.Join(",", target.Diff("not-exists", "commit-not-exists")));
            }

            public void ShouldDiffWithSpecificCommit_When_FileIsChanged()
            {
                target.CreateBranch("branch-1");
                target.CreateBranch("branch-2");

                target.Checkout("branch-1");
                File.WriteAllText("file1", "a\nb\nc\n");
                target.Stage("file1");
                var commitId = target.Commit("");

                File.WriteAllText("file1", "a\nb\nc\n");
                target.Stage("file1");
                target.Commit("");

                target.Checkout("branch-2");

                File.WriteAllText("file1", "1\n2\n3\n");

                Assert.AreEqual("- a,- b,- c,+ 1,+ 2,+ 3", string.Join(",", target.Diff("file1", commitId)));
            }

            public void ShouldDiffWithSpecificCommit_When_FileIsUnchanged()
            {
                target.CreateBranch("branch-1");
                target.CreateBranch("branch-2");

                target.Checkout("branch-1");
                File.WriteAllText("file1", "a\nb\nc\n");
                target.Stage("file1");
                var commitId = target.Commit("");

                File.WriteAllText("file1", "a\nb\nc\n");
                target.Stage("file1");
                target.Commit("");

                target.Checkout("branch-2");

                File.WriteAllText("file1", "a\nb\nc\n");

                Assert.AreEqual("- a,- b,- c,+ a,+ b,+ c", string.Join(",", target.Diff("file1", commitId)));
            }
        }

        class HistoryTest
        {
            public void BeforeEach() => ResetWorkDirectory();

            static string Serialize(IEnumerable<(string, string, string, string)> items) => string.Join(",", items.Select(x => (x.Item2, x.Item3, x.Item4).ToString()));

            public void ShouldGetHistoryOfCurrentBranch_When_CommitIdIsNull()
            {
                target.CreateBranch("branch-1");
                target.CreateBranch("branch-2");
                target.Checkout("branch-1");
                var commitId1 = target.Commit("commit #1");
                target.Checkout("branch-2");
                var commitId2 = target.Commit("commit #2");
                var commitId3 = target.Commit("commit #3");

                Assert.AreEqual($"({commitId2}, 67d211dde2932b6f, commit #2),({commitId3}, {commitId2}, commit #3)", Serialize(target.GetHistory(null)));

                target.Checkout("branch-1");
                Assert.AreEqual($"({commitId1}, 67d211dde2932b6f, commit #1)", Serialize(target.GetHistory(null)));
            }

            public void ShouldGetEmptyHistory_When_CommitIdIsEmptyOrNotExists()
            {
                var commitId1 = target.Commit("commit #1");
                var commitId2 = target.Commit("commit #2");

                Assert.AreEqual("", Serialize(target.GetHistory("")));
                Assert.AreEqual("", Serialize(target.GetHistory("not-exists")));
            }

            public void ShouldGetHistory_When_CommitIdExists()
            {
                var commitId1 = target.Commit("commit #1");
                var commitId2 = target.Commit("commit #2");
                var commitId3 = target.Commit("commit #3");

                Assert.AreEqual($"({commitId1}, 67d211dde2932b6f, commit #1)", Serialize(target.GetHistory(commitId1)));
                Assert.AreEqual($"({commitId1}, 67d211dde2932b6f, commit #1),({commitId2}, {commitId1}, commit #2)", Serialize(target.GetHistory(commitId2)));
                Assert.AreEqual($"({commitId1}, 67d211dde2932b6f, commit #1),({commitId2}, {commitId1}, commit #2),({commitId3}, {commitId2}, commit #3)", Serialize(target.GetHistory(commitId3)));
            }

            public void ShouldGetHistoryTraverseOnlyParents()
            {
                var commitId1 = target.Commit("commit #1");
                target.CreateBranch("branch-1");
                target.CreateBranch("branch-2");
                target.Checkout("branch-1");
                var commitId2 = target.Commit("commit #2");
                target.Checkout("branch-2");
                var commitId3 = target.Commit("commit #3");

                Assert.AreEqual($"({commitId1}, 67d211dde2932b6f, commit #1)", Serialize(target.GetHistory(commitId1)));
                Assert.AreEqual($"({commitId1}, 67d211dde2932b6f, commit #1),({commitId2}, {commitId1}, commit #2)", Serialize(target.GetHistory(commitId2)));
                Assert.AreEqual($"({commitId1}, 67d211dde2932b6f, commit #1),({commitId3}, {commitId1}, commit #3)", Serialize(target.GetHistory(commitId3))); // exclude commit #2
            }
        }

        class MergeTest
        {
            // These tests are for the default silly merge algorithm

            public void BeforeEach() => ResetWorkDirectory();

            static string Serialize(IEnumerable<(string, IEnumerable<string>)> items) => string.Join(",", items.Select(x => $"({x.Item1}, {string.Join(",", x.Item2.Select(y => y))})"));

            public void ShouldMerge_When_BranchNameIsNull()
            {
                CommitChanges("add some files", ("file-1", "roarr"));

                Assert.AreEqual("(file-1, <<<<<<<<,roarr,========,roarr,>>>>>>>>)", Serialize(target.Merge(null)));
            }

            public void ShouldMerge_When_BranchNameIsNull_And_FileHasUncommittedChange()
            {
                CommitChanges("add some files", ("file-1", "roarr"));
                File.WriteAllText("file-1", "roarr-uncommitted");

                Assert.AreEqual("(file-1, <<<<<<<<,roarr-uncommitted,========,roarr,>>>>>>>>)", Serialize(target.Merge(null)));
            }

            public void ShouldMerge_When_BranchNameIsNull_And_FileHasUncommittedDelete()
            {
                CommitChanges("add some files", ("file-1", "roarr"));
                File.Delete("file-1");

                Assert.AreEqual("(file-1, <<<<<<<<,========,roarr,>>>>>>>>)", Serialize(target.Merge(null)));
            }

            public void ShouldMerge_When_BranchNameIsEmpty()
            {
                CommitChanges("add some files", ("file-1", "roarr"));

                Assert.AreEqual("", Serialize(target.Merge("")));
            }

            public void ShouldMerge_When_BranchNameIsEmpty_And_FileHasUncommittedChange()
            {
                CommitChanges("add some files", ("file-1", "roarr"));
                File.WriteAllText("file-1", "roarr-uncommitted");

                Assert.AreEqual("", Serialize(target.Merge("")));
            }

            public void ShouldMerge_When_BranchNameIsEmpty_And_FileHasUncommittedDelete()
            {
                CommitChanges("add some files", ("file-1", "roarr"));
                File.Delete("file-1");

                Assert.AreEqual("", Serialize(target.Merge("")));
            }

            public void ShouldMerge_When_BranchDoesNotExist()
            {
                CommitChanges("add some files", ("file-1", "roarr"));

                Assert.AreEqual("", Serialize(target.Merge("not-exists")));
            }

            public void ShouldMerge_When_BranchDoesNotExist_And_FileHasUncommittedChange()
            {
                CommitChanges("add some files", ("file-1", "roarr"));
                File.WriteAllText("file-1", "roarr-uncommitted");

                Assert.AreEqual("", Serialize(target.Merge("not-exists")));
            }

            public void ShouldMerge_When_BranchDoesNotExist_And_FileHasUncommittedDelete()
            {
                CommitChanges("add some files", ("file-1", "roarr"));
                File.Delete("file-1");

                Assert.AreEqual("", Serialize(target.Merge("not-exists")));
            }

            public void ShouldMerge_When_BranchIsSelf()
            {
                CommitChanges("add some files", ("file-1", "roarr"));

                Assert.AreEqual("(file-1, <<<<<<<<,roarr,========,roarr,>>>>>>>>)", Serialize(target.Merge("default")));
            }

            public void ShouldMerge_When_BranchIsSelf_And_FileHasUncommittedChange()
            {
                CommitChanges("add some files", ("file-1", "roarr"));
                File.WriteAllText("file-1", "roarr-uncommitted");

                Assert.AreEqual("(file-1, <<<<<<<<,roarr-uncommitted,========,roarr,>>>>>>>>)", Serialize(target.Merge("default")));
            }

            public void ShouldMerge_When_BranchIsSelf_And_FileHasUncommittedDelete()
            {
                CommitChanges("add some files", ("file-1", "roarr"));
                File.Delete("file-1");

                Assert.AreEqual("(file-1, <<<<<<<<,========,roarr,>>>>>>>>)", Serialize(target.Merge("default")));
            }

            public void ShouldMerge_When_TargetFileDoesNotExist()
            {
                target.CreateBranch("target-branch");
                target.CreateBranch("source-branch");
                CommitChanges("add some files", ("file-1", "roarr"));

                target.Checkout("target-branch");

                Assert.AreEqual("(file-1, <<<<<<<<,========,roarr,>>>>>>>>)", Serialize(target.Merge("source-branch")));
            }
            public void ShouldMerge_When_FilesExistInBothBranches()
            {
                target.CreateBranch("target-branch");
                target.CreateBranch("source-branch");
                CommitChanges("add some files", ("file-1", "roarr"));

                target.Checkout("target-branch");
                CommitChanges("", ("file-1", "roarr")); // same file

                Assert.AreEqual("(file-1, <<<<<<<<,roarr,========,roarr,>>>>>>>>)", Serialize(target.Merge("source-branch")));
            }

            public void ShouldMerge_When_FileHasChanged()
            {
                target.CreateBranch("target-branch");
                target.CreateBranch("source-branch");
                CommitChanges("add some files", ("file-1", "roarr"));

                target.Checkout("target-branch");
                CommitChanges("", ("file-1", "roarr-target-branch"));

                Assert.AreEqual("(file-1, <<<<<<<<,roarr-target-branch,========,roarr,>>>>>>>>)", Serialize(target.Merge("source-branch")));
            }

            public void ShouldMerge_When_FileHasNoChange()
            {
                CommitChanges("add some files", ("file-1", "roarr"));

                target.CreateBranch("target-branch");
                target.CreateBranch("source-branch");

                target.Checkout("target-branch");

                Assert.AreEqual("(file-1, <<<<<<<<,roarr,========,roarr,>>>>>>>>)", Serialize(target.Merge("source-branch")));
            }

            public void ShouldMerge_When_FileHasUncommittedChange()
            {
                target.CreateBranch("target-branch");
                target.CreateBranch("source-branch");
                CommitChanges("add some files", ("file-1", "roarr"));

                target.Checkout("target-branch");
                CommitChanges("", ("file-1", "roarr-target-branch"));
                File.WriteAllText("file-1", "roarr-target-branch-uncommitted");

                Assert.AreEqual("(file-1, <<<<<<<<,roarr-target-branch-uncommitted,========,roarr,>>>>>>>>)", Serialize(target.Merge("source-branch")));
            }

            public void ShouldMerge_When_FileHasUncommittedDelete()
            {
                target.CreateBranch("target-branch");
                target.CreateBranch("source-branch");
                CommitChanges("add some files", ("file-1", "roarr"));

                target.Checkout("target-branch");
                CommitChanges("", ("file-1", "roarr-target-branch"));
                File.Delete("file-1");

                Assert.AreEqual("(file-1, <<<<<<<<,========,roarr,>>>>>>>>)", Serialize(target.Merge("source-branch")));
            }

            public void ShouldMergeOnlyFilesInSourceBranch()
            {
                target.CreateBranch("target-branch");
                target.CreateBranch("source-branch");
                CommitChanges("add some files", ("file-1", "roarr"));

                target.Checkout("target-branch");
                CommitChanges("", ("file-2", "roarr"));

                Assert.AreEqual("(file-1, <<<<<<<<,========,roarr,>>>>>>>>)", Serialize(target.Merge("source-branch")));
            }

            public void ShouldMergeAllFilesInSourceBranch()
            {
                target.CreateBranch("target-branch");
                target.CreateBranch("source-branch");
                CommitChanges("add some files", ("file-1", "roarr"), ("file-2", "roarr"), ("file-3", "roarr"));
                CommitChanges("delete file-3", ("file-3", null));

                target.Checkout("target-branch");

                Assert.AreEqual("(file-1, <<<<<<<<,========,roarr,>>>>>>>>),(file-2, <<<<<<<<,========,roarr,>>>>>>>>)", Serialize(target.Merge("source-branch")));
            }

            public void ShouldMergeRecoverDeletedFilesInTargetBranch()
            {
                target.CreateBranch("target-branch");
                target.CreateBranch("source-branch");
                CommitChanges("add some files", ("file-1", "roarr"), ("file-2", "roarr"));

                target.Checkout("target-branch");
                CommitChanges("delete file-2", ("file-2", null));

                Assert.AreEqual("(file-1, <<<<<<<<,========,roarr,>>>>>>>>),(file-2, <<<<<<<<,========,roarr,>>>>>>>>)", Serialize(target.Merge("source-branch")));
            }

            public void ShouldMergeSetMergeSource_And_ShouldCommitResetMergeSource()
            {
                target.CreateBranch("target-branch");
                target.CreateBranch("source-branch");
                var commitId1 = target.Commit("");

                target.Checkout("target-branch");
                var commitId2 = target.Commit("");

                Assert.AreEqual("", Serialize(target.Merge("source-branch")));
                Assert.AreEqual((Name: "target-branch", Position: commitId2, MergeSource: commitId1), target.Status().Branch);

                var commitId3 = target.Commit("");
                Assert.AreEqual((Name: "target-branch", Position: commitId3, MergeSource: null), target.Status().Branch);
            }

            public void ShouldMergeStageAffectedFiles()
            {
                target.CreateBranch("target-branch");
                target.CreateBranch("source-branch");
                CommitChanges("add some files", ("file-1", "roarr"), ("file-2", "roarr"));

                target.Checkout("target-branch");
                CommitChanges("", ("file-2", "roarr"));
                File.WriteAllText("file-3", "roarr");

                Assert.AreEqual("(file-1, <<<<<<<<,========,roarr,>>>>>>>>),(file-2, <<<<<<<<,roarr,========,roarr,>>>>>>>>)", Serialize(target.Merge("source-branch")));
                Assert.AreEqual("(file-1, new),(file-2, changed)", ScmTests.Serialize(target.Status().Stage));
                Assert.AreEqual("(file-3, new)", ScmTests.Serialize(target.Status().Workspace));
            }
        }

        class StageTest
        {
            public void BeforeEach() => ResetWorkDirectory();

            public void ShouldStageFail_When_FileIsNull()
            {
                Assert.Throws<ArgumentNullException>(() => target.Stage(null));
            }

            public void ShouldStageFail_When_FileIsEmpty()
            {
                Assert.Throws<ArgumentException>(() => target.Stage(""));
            }

            public void Skip_ShouldStageFail_When_FileIsInvalid()
            {
                // TODO: invalid characters are os dependent
                Assert.Throws<ArgumentException>(() => target.Stage("\\\\"));
            }

            public void ShouldUnstageFail_When_FileIsNull()
            {
                Assert.Throws<ArgumentNullException>(() => target.Unstage(null));
            }

            public void ShouldUnstageFail_When_FileIsEmpty()
            {
                Assert.Throws<ArgumentException>(() => target.Unstage(""));
            }

            public void Skip_UnshouldStageFail_When_FileIsInvalid()
            {
                // TODO: invalid characters are os dependent
                Assert.Throws<ArgumentException>(() => target.Unstage("\\\\"));
            }

            public void ShouldStageUnstage()
            {
                Assert.AreEqual("file1", string.Join(",", target.Stage("file1")));
                Assert.AreEqual("file1,file2", string.Join(",", target.Stage("file2")));
                Assert.AreEqual("file2", string.Join(",", target.Unstage("file1")));
                Assert.AreEqual("", string.Join(",", target.Unstage("file2")));
            }

            public void ShouldNotStageMultipleTimes()
            {
                Assert.AreEqual("file1", string.Join(",", target.Stage("file1")));
                Assert.AreEqual("file1", string.Join(",", target.Stage("file1")));
            }

            public void ShouldUnstage_When_FileIsNotStaged()
            {
                target.Stage("staged-file");
                Assert.AreEqual("staged-file", string.Join(",", target.Unstage("not-staged")));
            }

            public void ShouldUnstageMultipleTimes()
            {
                target.Stage("file1");
                Assert.AreEqual("", string.Join(",", target.Unstage("file1")));
                Assert.AreEqual("", string.Join(",", target.Unstage("file1")));
            }
        }

        class StatusTest
        {
            public void BeforeEach() => ResetWorkDirectory();

            public void ShouldStatusBeCorrect_When_InInitialState()
            {
                var result = target.Status();

                Assert.AreEqual(target.RootPath, result.Root);
                Assert.AreEqual((Name: "default", Position: "67d211dde2932b6f", MergeSource: null), result.Branch);
                Assert.AreEqual(0, result.Stage.Count());
                Assert.AreEqual(0, result.Workspace.Count());
            }

            public void ShouldStatusListStagedAndWorkspaceFiles()
            {
                string Serialize(IEnumerable<(string, string)> items) => string.Join(",", items.OrderBy(x => x.Item1).ThenBy(x => x.Item2).Select(x => x.ToString()));

                File.WriteAllText("changed-file", "roarr");
                File.WriteAllText("unchanged-file", "roarr");
                File.WriteAllText("deleted-file", "roarr");
                target.Stage("changed-file");
                target.Stage("unchanged-file");
                target.Stage("deleted-file");
                target.Commit("commit #1");

                Assert.AreEqual("", Serialize(target.Status().Stage));
                Assert.AreEqual("", Serialize(target.Status().Workspace));

                File.WriteAllText("new-file", "roarr");
                File.WriteAllText("changed-file", "roarr changed");
                File.Delete("deleted-file");

                Assert.AreEqual("", Serialize(target.Status().Stage));
                Assert.AreEqual("(changed-file, changed),(deleted-file, deleted),(new-file, new)", Serialize(target.Status().Workspace));

                target.Stage("new-file");
                target.Stage("changed-file");
                target.Stage("unchanged-file");
                target.Stage("deleted-file");

                Assert.AreEqual("(changed-file, changed),(deleted-file, deleted),(new-file, new),(unchanged-file, unchanged)", Serialize(target.Status().Stage));
                Assert.AreEqual("", Serialize(target.Status().Workspace));

                target.Unstage("new-file");
                Assert.AreEqual("(changed-file, changed),(deleted-file, deleted),(unchanged-file, unchanged)", Serialize(target.Status().Stage));
                Assert.AreEqual("(new-file, new)", Serialize(target.Status().Workspace));

                target.Unstage("changed-file");
                Assert.AreEqual("(deleted-file, deleted),(unchanged-file, unchanged)", Serialize(target.Status().Stage));
                Assert.AreEqual("(changed-file, changed),(new-file, new)", Serialize(target.Status().Workspace));

                target.Unstage("unchanged-file");
                Assert.AreEqual("(deleted-file, deleted)", Serialize(target.Status().Stage));
                Assert.AreEqual("(changed-file, changed),(new-file, new)", Serialize(target.Status().Workspace));

                target.Unstage("deleted-file");
                Assert.AreEqual("", Serialize(target.Status().Stage));
                Assert.AreEqual("(changed-file, changed),(deleted-file, deleted),(new-file, new)", Serialize(target.Status().Workspace));
            }

            // TODO: test ignore file
        }

        public static void Go()
        {
            new PUnit()
                .Test<BranchTest>()
                .Test<CheckoutTest>()
                .Test<CommitTest>()
                .Test<DiffTest>()
                .Test<HistoryTest>()
                .Test<MergeTest>()
                .Test<StageTest>()
                .Test<StatusTest>()
                .RunToConsole();
        }
    }
}
