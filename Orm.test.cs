using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace Pinduri.Tests
{
    class Foo
    {
        public int Id { get; set; }
        public string StringProp { get; set; }
        public int IntProp { get; set; }
        public bool BoolProp { get; set; }
        public DateTime DateTimeProp { get; set; }
        public Guid GuidProp { get; set; }
        public int? NullableIntProp { get; set; }
        public double? NullableDoubleProp { get; set; }
    }

    class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public List<Order> Orders { get; set; }
    }

    class Order
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }

        public List<OrderDetail> Details { get; set; }
    }

    class OrderDetail
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }

        public Order Order { get; set; }
        public Product Product { get; set; }
    }

    class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Price { get; set; }
        public int? CategoryId { get; set; }

        public ProductCategory Category { get; set; }
    }

    class ProductCategory
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    class Employee
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? ManagerId { get; set; }
        public int? DeputyId { get; set; }

        public Employee Manager { get; set; }
        public Employee Deputy { get; set; }
        public List<Employee> ManagedEmployees { get; set; }

        public override string ToString()
        {
            return $"{Name} ({Id})";
        }
    }

    public class OrmTests
    {
        public static Orm target = new Orm() { ConnectionString = "Server=localhost;User Id=sa;password=Abcd123#;Database=pinduri-orm-test" }
            .Entity<Foo>().Index<Foo>(new[] { nameof(Foo.GuidProp), nameof(Foo.DateTimeProp) }, isUnique: true)
            .Entity<Employee>()
            .HasOne<Employee, Employee>("ManagerId", "ManagedEmployees")
            .HasOne<Employee, Employee>("DeputyId")

            .Entity<Customer>().Index<Customer>(new[] { "Name" }, isUnique: true)
            .Entity<Order>()
            .Entity<OrderDetail>()
            .Entity<Product>().Index<Product>(new[] { "Name", "CategoryId" }, isUnique: true).Index<Product>(new[] { "Price" }, isUnique: false)
            .Entity<ProductCategory>().Index<ProductCategory>(new[] { "Name" })
            .HasOne<Product, ProductCategory>("CategoryId")
            .HasOne<Order, Customer>("CustomerId", "Orders")
            .HasOne<OrderDetail, Order>("OrderId", "Details")
            .HasOne<OrderDetail, Product>("ProductId");

        public class QueryExecutorTest
        {
            public void Before()
            {
                var create = @"
DROP DATABASE IF EXISTS [pinduri-orm-test];
CREATE DATABASE [pinduri-orm-test];
";
                new Orm() { ConnectionString = "Server=localhost;User Id=sa;Password=Abcd123#;Connect Timeout=3" }
                    .ExecuteNonQuery(new SqlCommand(create));
            }

            public void BeforeEach()
            {
                var drop = @"
DROP TABLE IF EXISTS [OrderDetail];
DROP TABLE IF EXISTS [Order];
DROP TABLE IF EXISTS [Product];
DROP TABLE IF EXISTS [ProductCategory];
DROP TABLE IF EXISTS [Customer];
DROP TABLE IF EXISTS [Foo];
DROP TABLE IF EXISTS [Employee];
";
                target.ExecuteNonQuery(new SqlCommand(drop));

                var schema = target.Schema();
                target.ExecuteNonQuery(new SqlCommand(schema));
            }

            public void ShouldSelectWithRelation()
            {
                var productCategory = new ProductCategory() { Name = "Product category #1" };
                target.Insert(productCategory);

                var product = new Product() { Name = "Product #1", CategoryId = productCategory.Id, Price = 3.14 };
                target.Insert(product);

                var result = target.Select<Product>(include: new[] { "Category" }).ToList();
                Assert.AreEqual(1, result.Count);
                Assert.AreEqual(product.Id, result[0].Id);
                Assert.IsNotNull(result[0].Category);
                Assert.AreEqual(productCategory.Id, result[0].Category.Id);
                Assert.AreEqual(productCategory.Name, result[0].Category.Name);
            }

            public void ShouldSelectWithRelation2()
            {
                var productCategory1 = target.Insert(new ProductCategory() { Name = "Product category #1" });
                var productCategory2 = target.Insert(new ProductCategory() { Name = "Product category #2" });
                var product1 = target.Insert(new Product() { Name = "Product #1", CategoryId = productCategory1.Id, Price = 1.1 });
                var product2 = target.Insert(new Product() { Name = "Product #2", CategoryId = productCategory2.Id, Price = 2.2 });
                var product3 = target.Insert(new Product() { Name = "Product #3", CategoryId = productCategory2.Id, Price = 3.3 });
                var customer = target.Insert(new Customer() { Name = "Hans Regenkurt" });
                var order = target.Insert(new Order() { CustomerId = customer.Id });
                var orderDetail1 = target.Insert(new OrderDetail() { OrderId = order.Id, ProductId = product1.Id, Quantity = 1 });
                var orderDetail2 = target.Insert(new OrderDetail() { OrderId = order.Id, ProductId = product2.Id, Quantity = 2 });
                var orderDetail3 = target.Insert(new OrderDetail() { OrderId = order.Id, ProductId = product3.Id, Quantity = 3 });

                var result = target.Select<Customer>(include: new[] { "Orders", "Orders.Details", "Orders.Details.Product", "Orders.Details.Product.Category" }).ToList();
                Assert.AreEqual(1, result.Count);

                var selectedCustomer = result[0];
                Assert.AreEqual(customer.Id, selectedCustomer.Id);
                Assert.IsNotNull(selectedCustomer.Orders);
                Assert.AreEqual(1, selectedCustomer.Orders.Count);

                var selectedOrder = selectedCustomer.Orders[0];
                Assert.AreEqual(order.Id, selectedOrder.Id);
                Assert.IsNotNull(selectedOrder.Details);
                Assert.AreEqual(3, selectedOrder.Details.Count);

                Assert.IsNotNull(selectedOrder.Details[0].Product);
                Assert.AreEqual(product1.Id, selectedOrder.Details[0].Product.Id);
                Assert.IsNotNull(selectedOrder.Details[0].Product.Category);
                Assert.AreEqual(productCategory1.Id, selectedOrder.Details[0].Product.Category.Id);

                Assert.IsNotNull(selectedOrder.Details[1].Product);
                Assert.AreEqual(product2.Id, selectedOrder.Details[1].Product.Id);
                Assert.IsNotNull(selectedOrder.Details[1].Product.Category);
                Assert.AreEqual(productCategory2.Id, selectedOrder.Details[1].Product.Category.Id);

                Assert.IsNotNull(selectedOrder.Details[2].Product);
                Assert.AreEqual(product3.Id, selectedOrder.Details[2].Product.Id);
                Assert.IsNotNull(selectedOrder.Details[2].Product.Category);
                Assert.AreEqual(productCategory2.Id, selectedOrder.Details[2].Product.Category.Id);
            }

            public void ShouldSelectWithRelation3()
            {
                var managersDeputy = target.Insert(new Employee() { Name = "Deputy of Manager" });
                var manager1 = target.Insert(new Employee() { Name = "Manager", DeputyId = managersDeputy.Id });
                var manager2 = target.Insert(new Employee() { Name = "Vice Manager", ManagerId = manager1.Id });
                var deputy1 = target.Insert(new Employee() { Name = "Deputy", ManagerId = manager1.Id });
                var employee1 = target.Insert(new Employee() { Name = "Employee with deputy", ManagerId = manager2.Id, DeputyId = deputy1.Id });
                var employee2 = target.Insert(new Employee() { Name = "Employee", ManagerId = manager2.Id });

                var results = target.Select<Employee>(include: new[] { "ManagedEmployees", "ManagedEmployees.Deputy", "ManagedEmployees.Deputy.Manager", "Deputy", "Manager", "Deputy.Manager" }).ToList();

                Assert.AreEqual(6, results.Count());

                var resultManagersDeputy = results.FirstOrDefault(x => x.Id == managersDeputy.Id);
                Assert.IsNotNull(resultManagersDeputy, "resultManagersDeputy");
                Assert.AreEqual(null, resultManagersDeputy.Deputy, "resultManagersDeputy.Deputy");
                Assert.AreEqual(null, resultManagersDeputy.Manager, "resultManagersDeputy.Manager");
                Assert.AreEqual(0, resultManagersDeputy.ManagedEmployees.Count(), "resultManagersDeputy.ManagedEmployees");

                var resultManager1 = results.FirstOrDefault(x => x.Id == manager1.Id);
                Assert.IsNotNull(resultManager1, "resultManager1");
                Assert.IsNotNull(resultManager1.Deputy, "resultManager1.Deputy");
                Assert.AreEqual(null, resultManager1.Manager, "resultManager1.Manager");
                Assert.AreEqual("3,4", string.Join(",", resultManager1.ManagedEmployees.Select(x => x.Id.ToString())), "resultManager1.ManagedEmployees");

                var resultManager2 = results.FirstOrDefault(x => x.Id == manager2.Id);
                Assert.IsNotNull(resultManager2, "resultManager2");
                Assert.AreEqual(null, resultManager2.Deputy, "resultManager2.Deputy");
                Assert.IsNotNull(resultManager2.Manager, "resultManager2.Manager");
                Assert.AreEqual("5,6", string.Join(",", resultManager2.ManagedEmployees.Select(x => x.Id.ToString())), "resultManager2.ManagedEmployees");

                var resultDeputy1 = results.FirstOrDefault(x => x.Id == deputy1.Id);
                Assert.IsNotNull(resultDeputy1, "resultDeputy1");
                Assert.AreEqual(null, resultDeputy1.Deputy, "resultDeputy1.Deputy");
                Assert.IsNotNull(resultDeputy1.Manager, "resultDeputy1.Manager");
                Assert.AreEqual(0, resultDeputy1.ManagedEmployees.Count(), "resultDeputy1.ManagedEmployees");

                var resultEmployee1 = results.FirstOrDefault(x => x.Id == employee1.Id);
                Assert.IsNotNull(resultEmployee1, "resultEmployee1");
                Assert.IsNotNull(resultEmployee1.Deputy, "resultEmployee1.Deputy");
                Assert.IsNotNull(resultEmployee1.Manager, "resultEmployee1.Manager");
                Assert.AreEqual(0, resultEmployee1.ManagedEmployees.Count(), "resultEmployee1.ManagedEmployees");

                var resultEmployee2 = results.FirstOrDefault(x => x.Id == employee2.Id);
                Assert.IsNotNull(resultEmployee2, "resultEmployee2");
                Assert.AreEqual(null, resultEmployee2.Deputy, "resultEmployee2.Deputy");
                Assert.IsNotNull(resultEmployee2.Manager, "resultEmployee2.Manager");
                Assert.AreEqual(0, resultEmployee2.ManagedEmployees.Count(), "resultEmployee2.ManagedEmployees");
            }

            public void ShouldDoCrudLogic()
            {
                var selectResult1 = target.Select<Foo>();
                Assert.AreEqual(0, selectResult1.Count());

                var value = new Foo() { Id = 111111, NullableDoubleProp = 2.5, BoolProp = true, DateTimeProp = DateTime.Now, GuidProp = Guid.NewGuid(), IntProp = 42, StringProp = "ROARR" };
                var insertResult = target.Insert(value);
                Assert.AreEqual(value, insertResult);
                Assert.AreEqual(1, value.Id);

                var selectResult2 = target.Select<Foo>().ToList();
                Assert.AreEqual(1, selectResult2.Count());
                Assert.AreEqual(1, selectResult2[0].Id);

                var newValue = new Foo() { Id = 1, BoolProp = false, DateTimeProp = DateTime.Now, GuidProp = Guid.NewGuid(), IntProp = 4422, StringProp = "ROARR 2" };
                var updateResult = target.Update(newValue);
                Assert.AreEqual(1, updateResult);

                var selectResult3 = target.Select<Foo>().ToList();
                Assert.AreEqual(1, selectResult3.Count());
                Assert.AreEqual(newValue.BoolProp, selectResult3[0].BoolProp);
                Assert.AreEqual(newValue.DateTimeProp.ToString(), selectResult3[0].DateTimeProp.ToString());
                Assert.AreEqual(newValue.GuidProp, selectResult3[0].GuidProp);
                Assert.AreEqual(newValue.IntProp, selectResult3[0].IntProp);
                Assert.AreEqual(newValue.StringProp, selectResult3[0].StringProp);

                var deleteResult = target.Delete<Foo>(new Foo() { Id = 1 });
                Assert.AreEqual(1, deleteResult);

                var selectResult4 = target.Select<Foo>();
                Assert.AreEqual(0, selectResult4.Count());
            }
        }

        class QueryBuilderTest
        {
            public void ShouldGenerateSelectCommand()
            {
                var result = target.SelectCommand<Product>().Command;
                Assert.AreEqual("SELECT [Id], [Name], [Price], [CategoryId] FROM [Product] WHERE 1 = 1 ORDER BY 1", result.CommandText);
                Assert.AreEqual("", string.Join(", ", result.Parameters.Cast<SqlParameter>().Select(x => $"{x.ParameterName}={x.Value}")));
            }

            public void ShouldGenerateSelectCommandWithOrderBy()
            {
                var result = target.SelectCommand<Product>(orderBy: "Name DESC").Command;
                Assert.AreEqual("SELECT [Id], [Name], [Price], [CategoryId] FROM [Product] WHERE 1 = 1 ORDER BY Name DESC", result.CommandText);
                Assert.AreEqual("", string.Join(", ", result.Parameters.Cast<SqlParameter>().Select(x => $"{x.ParameterName}={x.Value}")));
            }

            public void ShouldGenerateSelectCommandWithWhereClause()
            {
                var result = target.SelectCommand<Product>(whereClause: "Name like @Name", new { Name = "Roarr" }, orderBy: "Name DESC").Command;
                Assert.AreEqual("SELECT [Id], [Name], [Price], [CategoryId] FROM [Product] WHERE Name like @Name ORDER BY Name DESC", result.CommandText);
                Assert.AreEqual("@Name=Roarr", string.Join(", ", result.Parameters.Cast<SqlParameter>().Select(x => $"{x.ParameterName}={x.Value}")));
            }

            public void ShouldGenerateInsertCommand()
            {
                var result = target.InsertCommand(new Product() { Id = 42, Name = "Power Roarr", Price = 3.14 });
                Assert.AreEqual("INSERT INTO [Product] ([Name], [Price], [CategoryId]) VALUES(@Name, @Price, @CategoryId); SELECT CAST(SCOPE_IDENTITY() AS int)", result.CommandText);
                Assert.AreEqual($"@Name=Power Roarr, @Price={3.14}, @CategoryId=", string.Join(", ", result.Parameters.Cast<SqlParameter>().Select(x => $"{x.ParameterName}={x.Value}")));
            }

            public void ShouldGenerateUpdateCommand()
            {
                var result = target.UpdateCommand(new Product() { Id = 42, Name = "Power Roarr", Price = 3.14 });
                Assert.AreEqual("UPDATE [Product] SET [Name] = @Name, [Price] = @Price, [CategoryId] = @CategoryId WHERE [Id] = @Id", result.CommandText);
                Assert.AreEqual($"@Id=42, @Name=Power Roarr, @Price={3.14}, @CategoryId=", string.Join(", ", result.Parameters.Cast<SqlParameter>().Select(x => $"{x.ParameterName}={x.Value}")));
            }

            public void ShouldGenerateDeleteCommand()
            {
                var result = target.DeleteCommand(new Product() { Id = 42, Name = "Power Roarr", Price = 3.14 });
                Assert.AreEqual("DELETE FROM [Product] WHERE [Id] = @Id", result.CommandText);
                Assert.AreEqual("@Id=42", string.Join(", ", result.Parameters.Cast<SqlParameter>().Select(x => $"{x.ParameterName}={x.Value}")));
            }
        }

        public class SchemaGeneratorTest
        {
            public static void ShouldGenerateSchema()
            {
                string expected = @"
CREATE TABLE [Foo] (
	[Id] int not null identity (1, 1) primary key,
 	[StringProp] nvarchar(400) not null,
	[IntProp] int not null,
	[BoolProp] bit not null,
	[DateTimeProp] datetime not null,
	[GuidProp] uniqueidentifier not null,
	[NullableIntProp] int null,
	[NullableDoubleProp] float null
);

CREATE TABLE [Employee] (
	[Id] int not null identity (1, 1) primary key,
 	[Name] nvarchar(400) not null,
	[ManagerId] int null,
	[DeputyId] int null
);

CREATE TABLE [Customer] (
	[Id] int not null identity (1, 1) primary key,
 	[Name] nvarchar(400) not null
);

CREATE TABLE [Order] (
	[Id] int not null identity (1, 1) primary key,
 	[CustomerId] int not null
);

CREATE TABLE [OrderDetail] (
	[Id] int not null identity (1, 1) primary key,
 	[Quantity] int not null,
	[OrderId] int not null,
	[ProductId] int not null
);

CREATE TABLE [Product] (
	[Id] int not null identity (1, 1) primary key,
 	[Name] nvarchar(400) not null,
	[Price] float not null,
	[CategoryId] int null
);

CREATE TABLE [ProductCategory] (
	[Id] int not null identity (1, 1) primary key,
 	[Name] nvarchar(400) not null
);

ALTER TABLE [Employee] ADD CONSTRAINT fk_Employee_ManagerId FOREIGN KEY([ManagerId]) REFERENCES [Employee]([Id]);

ALTER TABLE [Employee] ADD CONSTRAINT fk_Employee_DeputyId FOREIGN KEY([DeputyId]) REFERENCES [Employee]([Id]);

ALTER TABLE [Product] ADD CONSTRAINT fk_Product_CategoryId FOREIGN KEY([CategoryId]) REFERENCES [ProductCategory]([Id]);

ALTER TABLE [Order] ADD CONSTRAINT fk_Order_CustomerId FOREIGN KEY([CustomerId]) REFERENCES [Customer]([Id]);

ALTER TABLE [OrderDetail] ADD CONSTRAINT fk_OrderDetail_OrderId FOREIGN KEY([OrderId]) REFERENCES [Order]([Id]);

ALTER TABLE [OrderDetail] ADD CONSTRAINT fk_OrderDetail_ProductId FOREIGN KEY([ProductId]) REFERENCES [Product]([Id]);

CREATE UNIQUE INDEX ix_Foo_GuidProp_DateTimeProp ON [Foo]([GuidProp], [DateTimeProp]);

CREATE UNIQUE INDEX ix_Customer_Name ON [Customer]([Name]);

CREATE UNIQUE INDEX ix_Product_Name_CategoryId ON [Product]([Name], [CategoryId]);

CREATE INDEX ix_Product_Price ON [Product]([Price]);

CREATE INDEX ix_ProductCategory_Name ON [ProductCategory]([Name]);
".Trim().Replace("\r\n", "\n");

                string schema = target.Schema().Trim().Replace("\r\n", "\n");

                Assert.AreEqual(expected, schema);
            }
        }

        public static void Go()
        {
            new PUnit()
                .Test<SchemaGeneratorTest>()
                .Test<QueryBuilderTest>()
                .Test<QueryExecutorTest>()
                .RunToConsole();
        }
    }
}
