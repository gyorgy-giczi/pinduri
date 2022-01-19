﻿namespace Pinduri
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "scm")
            {
                args = args[1..];
                new Pinduri.Scm() { RootPath = ".", DiffContent = DiffMerge.Diff, MergeContent = DiffMerge.Merge }.Cli(args.Length == 0 ? new string[] { "" } : args);
            }
            else
            {
                Pinduri.Tests.PUnitTests.Go();
                Pinduri.Tests.ContainerTests.Go();
                Pinduri.Tests.OrmTests.Go();
                Pinduri.Tests.JsonTests.Go();
                Pinduri.Tests.DiffMergeTests.Go();
                Pinduri.Tests.ScmTests.Go();
            }
        }
    }
}
