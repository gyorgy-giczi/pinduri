namespace Pinduri
{
    class Program
    {
        static void Main(string[] args)
        {
            Pinduri.Tests.PUnitTests.Go();
            Pinduri.Tests.ContainerTests.Go();
            Pinduri.Tests.OrmTests.Go();
            Pinduri.Tests.JsonTests.Go();
            Pinduri.Tests.DiffMergeTests.Go();
        }
    }
}
