using System;

namespace Pinduri.Tests
{
    public class ContainerTests
    {
        interface ITest { }

        class TestImpl : ITest { }

        abstract class AbstractClass { }

        class ConcreteClass { }

        class OneCtor
        {
            public ITest Test { get; init; }
            public OneCtor(ITest test) { this.Test = test; }
        }

        class DefaultCtor { }

        class MultipleCtors
        {
            public ITest Test { get; init; }
            public string StringValue { get; init; }
            public MultipleCtors() { }
            public MultipleCtors(ITest test) { this.Test = test; }
            [Inject] public MultipleCtors(ITest test, string stringValue) { this.Test = test; this.StringValue = stringValue; }
        }

        class MultipleCtorsNoInjectableAttribute
        {
            public MultipleCtorsNoInjectableAttribute() { }
            public MultipleCtorsNoInjectableAttribute(ITest test) { }
        }

        class ContainerToCtor
        {
            public Container Container { get; init; }
            public ContainerToCtor(Container container) { this.Container = container; }
        }

        class SingletonClass { }

        class RegisterTypeTest
        {
            public void ShouldNotRegisterNullType()
            {
                var target = new Container();
                Assert.Throws<ArgumentNullException>(() => target.RegisterType(typeof(ITest), null), "Value cannot be null. (Parameter 'type')");
            }

            public void ShouldNotRegisterNullTypeKey()
            {
                var target = new Container();
                Assert.Throws<ArgumentNullException>(() => target.RegisterType(null, typeof(TestImpl)), "Value cannot be null. (Parameter 'key')");
            }

            public void ShouldNotRegisterIncompatibleTypes()
            {
                var target = new Container();
                Assert.Throws<ArgumentException>(() => target.RegisterType(typeof(TestImpl), typeof(ConcreteClass)), "Type 'Pinduri.Tests.ContainerTests+TestImpl' is not assignable from 'Pinduri.Tests.ContainerTests+ConcreteClass'. (Parameter 'type')");
            }

            public void ShouldNotRegisterAbstractType()
            {
                var target = new Container();
                Assert.Throws<ArgumentException>(() => target.RegisterType<AbstractClass>(), "Type 'Pinduri.Tests.ContainerTests+AbstractClass' is abstract. (Parameter 'type')");
            }

            public void ShouldNotRegisterNullFactory()
            {
                var target = new Container();
                Assert.Throws<ArgumentNullException>(() => target.RegisterType<ITest, TestImpl>(null), "Value cannot be null. (Parameter 'factory')");
            }

            public void ShouldRegisterSingleton()
            {
                var target = new Container();
                target.RegisterSingleton<ITest>(new TestImpl());
            }

            public void ShouldAllowToReRegisterType()
            {
                var target = new Container();
                target.RegisterSingleton<ITest>(new TestImpl());
                target.RegisterSingleton<ITest>(new TestImpl());
                target.RegisterType<ITest, TestImpl>();
            }
        }

        class CreateTest
        {
            private Container _target;

            public void BeforeEach()
            {
                _target = new Container()
                    .RegisterType<ITest, TestImpl>()
                    .RegisterType<ConcreteClass>()
                    .RegisterType<OneCtor>()
                    .RegisterType<MultipleCtors>()
                    .RegisterType<MultipleCtorsNoInjectableAttribute>()
                    .RegisterType<ContainerToCtor>()
                    .RegisterSingleton(new SingletonClass());
            }

            public void ShouldCreateString()
            {
                Assert.AreEqual((string)null, _target.Create<string>());
            }

            public void ShouldCreateValueType()
            {
                Assert.AreEqual(default(int), _target.Create<int>());
            }

            public void ShouldCreateRegisteredType()
            {
                var result = _target.Create<OneCtor>();
                Assert.IsNotNull(result);
                Assert.IsNotNull(result.Test);
            }

            public void ShouldReturnSameInstanceOfSingleton()
            {
                var result1 = _target.Create<SingletonClass>();
                var result2 = _target.Create<SingletonClass>();
                Assert.IsNotNull(result1);
                Assert.IsNotNull(result2);
                Assert.AreEqual(result1, result2);
            }

            public void ShouldReturnDifferentInstanceOfNonSingleton()
            {
                var result1 = _target.Create<OneCtor>();
                var result2 = _target.Create<OneCtor>();
                Assert.IsNotNull(result1);
                Assert.IsNotNull(result2);
                Assert.AreEqual(false, Equals(result1, result2));
            }

            public void Skip_ShouldCreateSimpleObject()
            {
                throw new NotImplementedException();
            }

            public void Skip_ShouldInjectConstructor()
            {
                throw new NotImplementedException();
            }

            public void ShouldInjectContainer()
            {
                Assert.AreEqual(_target, _target.Create<ContainerToCtor>().Container);
            }
        }

        public static void Go()
        {
            new PUnit().Test<RegisterTypeTest>().Test<CreateTest>().RunToConsole();
        }
    }
}
