using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace Pinduri
{
    internal static class OrmExtensions
    {
        public static TResult Using<T, TResult>(Func<T> factory, Func<T, TResult> fn) where T : IDisposable { using (var x = factory()) { return fn(x); } }
        public static IEnumerable<TResult> While<T, TResult>(this T source, Predicate<T> pred, Func<T, TResult> fn) { while (pred(source)) { yield return fn(source); } }
    }

    public sealed class Orm
    {
        private static readonly Dictionary<Type, string> _sqlTypes = new()
        {
            { typeof(string), "nvarchar(400)" },
            { typeof(byte[]), "varbinary(MAX)" },
            { typeof(int), "int" },
            { typeof(double), "float" },
            { typeof(bool), "bit" },
            { typeof(DateTime), "datetime" },
            { typeof(Guid), "uniqueidentifier" },
        };

        private Dictionary<Type, List<System.Reflection.PropertyInfo>> Entities { get; init; } = new();
        private List<(Type From, Type To, string KeyField, string PropertyName)> Relations { get; init; } = new();
        private List<(Type Entity, string[] Fields, bool IsUnique)> Indexes { get; init; } = new();
        public string ConnectionString { get; init; }

        private static string SqlType(Type type) => (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) ? _sqlTypes[type.GetGenericArguments()[0]] + " null" : _sqlTypes[type] + " not null";
        private bool IsSupported(Type x) => (x.IsGenericType && x.GetGenericTypeDefinition() == typeof(Nullable<>)) ? IsSupported(x.GetGenericArguments()[0]) : new[] { typeof(string), typeof(byte[]), typeof(int), typeof(double), typeof(bool), typeof(DateTime), typeof(Guid) }.Contains(x);
        public Orm Entity<T>() => typeof(T).GetProperties().Where(x => x.CanWrite && x.CanRead && IsSupported(x.PropertyType)).ToList().Tap(x => Entities[typeof(T)] = x).Map(x => this);
        public Orm HasOne<TSource, TTarget>(string keyField, string propertyName = null) => (From: typeof(TSource), To: typeof(TTarget), KeyField: keyField, PropertyName: propertyName).Tap(x => Relations.Add(x)).Map(x => this);
        public Orm Index<T>(string[] fields, bool isUnique = false) => (Entity: typeof(T), Fields: fields, IsUnique: isUnique).Tap(x => Indexes.Add(x)).Map(x => this);

        public string Schema() =>
            new[] {
                Entities.Select(entry => $"CREATE TABLE [{entry.Key.Name}] (\n\t[Id] int not null identity (1, 1) primary key,\n {string.Join(",\n", entry.Value.Where(x => x.Name != "Id").Select(x => "\t[" + x.Name + "] " + x.PropertyType.Map(SqlType)))}\n);"),
                Relations.Select((relation) => $"ALTER TABLE [{relation.From.Name}] ADD CONSTRAINT fk_{relation.From.Name}_{relation.KeyField} FOREIGN KEY([{relation.KeyField}]) REFERENCES [{relation.To.Name}]([Id]);"),
                Indexes.Select((index) => $"CREATE {(index.IsUnique ? "UNIQUE " : "")}INDEX ix_{index.Entity.Name}_{string.Join("_", index.Fields)} ON [{index.Entity.Name}]({string.Join(", ", index.Fields.Select(f => "[" + f + "]"))});"),
            }.SelectMany(x => x).Map(x => string.Join("\n\n", x));

        public (SqlCommand Command, Func<T, SqlDataReader, T> Reader) SelectCommand<T>(string whereClause = null, object queryParameters = null, string orderBy = null) =>
            Entities[typeof(T)].Map(fields => (Command: $"SELECT {string.Join(", ", fields.Select(x => "[" + x.Name + "]"))} FROM [{typeof(T).Name}] WHERE {whereClause ?? "1 = 1"} ORDER BY {orderBy ?? "1"}".Map(x => new SqlCommand(x))
                    .Tap(cmd => { queryParameters?.GetType().GetProperties().ToList().ForEach(x => cmd.Parameters.AddWithValue("@" + x.Name, x.GetValue(queryParameters) ?? DBNull.Value)); })
                , Reader: new Func<T, SqlDataReader, T>((row, reader) => { fields.Select((x, i) => { x.SetValue(row, reader.GetValue(i).Map(y => y == DBNull.Value ? null : y)); return 0; }).ToList(); return row; })));

        public SqlCommand InsertCommand<T>(T value) =>
            Entities[typeof(T)].Where(x => x.Name != "Id").ToList().Map(fields => $"INSERT INTO [{typeof(T).Name}] ({string.Join(", ", fields.Select(x => "[" + x.Name + "]"))}) VALUES({string.Join(", ", fields.Select(x => "@" + x.Name))}); SELECT CAST(SCOPE_IDENTITY() AS int)".Map(x => new SqlCommand(x))
                .Tap(cmd => fields.ForEach(x => cmd.Parameters.AddWithValue("@" + x.Name, x.GetValue(value) ?? DBNull.Value))));

        public SqlCommand UpdateCommand<T>(T value) =>
            Entities[typeof(T)].Map(fields => $"UPDATE [{typeof(T).Name}] SET {string.Join(", ", fields.Where(x => x.Name != "Id").Select(x => "[" + x.Name + "] = @" + x.Name))} WHERE [Id] = @Id".Map(x => new SqlCommand(x))
                .Tap(cmd => fields.ForEach(x => { cmd.Parameters.AddWithValue("@" + x.Name, x.GetValue(value) ?? DBNull.Value); })));

        public SqlCommand DeleteCommand<T>(T value) =>
            Entities[typeof(T)].First(x => x.Name == "Id").Map(idField => $"DELETE FROM [{typeof(T).Name}] WHERE [Id] = @Id".Map(x => new SqlCommand(x))
                .Tap(cmd => cmd.Parameters.AddWithValue("@" + idField.Name, idField.GetValue(value) ?? DBNull.Value)));

        public int ExecuteNonQuery(SqlCommand cmd) => OrmExtensions.Using(() => new SqlConnection(ConnectionString), x => x.Tap(x => cmd.Connection = x).Tap(x => x.Open()).Map(x => cmd.ExecuteNonQuery()));
        public object ExecuteScalar(SqlCommand cmd) => OrmExtensions.Using(() => new SqlConnection(ConnectionString), x => x.Tap(x => cmd.Connection = x).Tap(x => x.Open()).Map(x => cmd.ExecuteScalar()));
        public SqlDataReader ExecuteReader(SqlCommand cmd) => new SqlConnection(ConnectionString).Map(x => x.Tap(x => cmd.Connection = x).Tap(x => x.Open()).Map(x => cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection)));

        private T SelectRelations<T>(T target, string[] relations) =>
            (relations ?? new string[0])
                .Where(x => x != null)
                .Select(x => new { Segments = x.Split('.', 2) }.Map(y => new { Key = y.Segments[0], Rest = y.Segments.Length > 1 ? y.Segments[1] : null }))
                .GroupBy(x => x.Key, x => x.Rest)
                .Select(g =>
                    new Func<(Type EntityType, string KeyField, string KeyFieldValue)>[] {
                        () => Relations.Where(x => x.To == typeof(T) && x.PropertyName == g.Key).Select(x => (EntityType: x.From, KeyField: x.KeyField, KeyFieldValue: "Id")).FirstOrDefault(),
                        () => Relations.Where(x => x.From == typeof(T) && x.KeyField == g.Key + "Id").Select(x => (EntityType: x.To, KeyField: "Id", KeyFieldValue: x.KeyField)).FirstOrDefault()
                    }.Select(x => x()).SkipWhile(x => x == default).First().Tap(x =>
                        typeof(Orm).GetMethod("Select").MakeGenericMethod(x.EntityType).Invoke(this, new object[] { $"{x.KeyField} = @IdParam", new { IdParam = typeof(T).GetProperty(x.KeyFieldValue).GetValue(target) }, null, g.ToArray() })
                            .Map(x => (IEnumerable<object>)x)
                            .Map(values => typeof(T).GetProperty(g.Key).Tap(targetProperty => targetProperty.SetValue(target, targetProperty.PropertyType == x.EntityType ? values.FirstOrDefault() : values)))
                    ))
                .ToList().Map(x => target);

        public IEnumerable<T> Select<T>(string whereClause = null, object queryParameters = null, string orderBy = null, string[] include = null) where T : new() =>
            SelectCommand<T>(whereClause, queryParameters, orderBy).Map(selectCommandAndReader =>
                OrmExtensions.Using(() => selectCommandAndReader.Command, cmd =>
                OrmExtensions.Using(() => ExecuteReader(cmd), reader =>
               reader.While(x => x.Read(), x => x.Map(x => selectCommandAndReader.Reader(new T(), x))).ToList().Select(x => SelectRelations(x, include)).ToList()
            )));

        public T Insert<T>(T value) => OrmExtensions.Using(() => InsertCommand(value), cmd => ExecuteScalar(cmd).Tap(x => typeof(T).GetProperty("Id").SetValue(value, x)).Map(x => value));
        public int Update<T>(T value) => OrmExtensions.Using(() => UpdateCommand(value), cmd => ExecuteNonQuery(cmd));
        public int Delete<T>(T value) => OrmExtensions.Using(() => DeleteCommand(value), cmd => ExecuteNonQuery(cmd));
    }
} // line #93
