using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Pinduri
{
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false)]
    public sealed class InjectAttribute : Attribute { }

    public sealed class Container
    {
        private readonly Dictionary<Type, Func<object>> factories = new();

        public Container() => RegisterSingleton(this);

        public Container RegisterSingleton<T>(T instance) => RegisterType<T, T>(() => instance).Map(x => this);
        public Container RegisterType<T, U>(Func<U> factory) where U : T => factory.Map(x => x ?? throw new ArgumentNullException(nameof(factory))).Tap(x => factories[typeof(T)] = () => factory()).Map(x => this);
        public Container RegisterType<T>() => RegisterType<T, T>().Map(x => this);
        public Container RegisterType<T, U>() => RegisterType(typeof(T), typeof(U)).Map(x => this);

        public Container RegisterType(Type key, Type type) =>
            key.Map(key => key ?? throw new ArgumentNullException(nameof(key)))
                .Map(key =>
                    type.Map(type => type ?? throw new ArgumentNullException(nameof(type)))
                        .Map(type => !key.IsAssignableFrom(type) ? throw new ArgumentException($"Type '{key.FullName}' is not assignable from '{type.FullName}'.", nameof(type)) : type)
                        .Map(type => type.IsAbstract ? throw new ArgumentException($"Type '{type.FullName}' is abstract.", nameof(type)) : type))
                        .Map(type => type == typeof(string) ? new Func<object>(() => (string)null) :
                            FindConstructor(type)
                                .Map(x => x ?? throw new ArgumentException($"Cannot find constructor of class '{type.FullName}'.", nameof(type)))
                                .Map(x => new Func<object>(() => Instantiate(x))))
                                .Tap(x => factories[key] = x)
                                .Map(x => this);

        public T Create<T>() => (T)Create(typeof(T));

        public object Create(Type type) =>
            type.Map(x => x ?? throw new ArgumentNullException(nameof(type)))
                .Map(x => factories.ContainsKey(x) ? factories[type].Invoke() :
                x.IsValueType ? Activator.CreateInstance(type) :
                x.Tap(y => RegisterType(y, y)).Map(x => Create(x)));

        private ConstructorInfo FindConstructor(Type type) =>
            type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Map(ctors => new Func<IEnumerable<ConstructorInfo>>[]
                    {
                        () => ctors.Length == 1 ? ctors : null,
                        () => ctors.Where(x => Attribute.IsDefined(x, typeof(InjectAttribute))),
                        () => ctors.Where(x => x.GetParameters().Length == 0)
                    }
                    .Select(x => x()).FirstOrDefault(x => x != null && x.Count() > 0)
                    .Map(x => x.Count() > 1 ? throw new ArgumentException($"Type '{type.FullName}' has more injectable constructors.", nameof(type)) : x)
                    .Map(x => x.Count() == 0 ? throw new ArgumentException($"Type '{type.FullName}' has no injectable constructors.", nameof(type)) : x)
                    .First());

        private object Instantiate(ConstructorInfo ctor) => ctor.GetParameters().Select(x => Create(x.ParameterType)).ToArray().Map(x => ctor.Invoke(x));
    }
} // line #58
