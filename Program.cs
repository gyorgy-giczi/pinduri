using System;

namespace Pinduri
{
    class Program
    {
        static void Main(string[] args)
        {
            Pinduri.Tests.PUnitTests.Go();
            Pinduri.Tests.ContainerTests.Go();
            Pinduri.Tests.OrmTests.Go();
        }
    }
}
