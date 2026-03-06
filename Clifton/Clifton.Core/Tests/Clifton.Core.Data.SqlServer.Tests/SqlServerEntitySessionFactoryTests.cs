using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

using Clifton.Core.Data.Abstractions;
using Clifton.Core.Data.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Clifton.Core.Data.SqlServer.Tests
{
    [TestClass]
    public class SqlServerEntitySessionFactoryTests
    {
        private const string TableName = "dbo.SqlServerSessionEntities";

        private string connectionString;
        private SqlServerEntitySessionFactory factory;

        [TestInitialize]
        public void TestInitialize()
        {
            connectionString = GetConnectionString();

            try
            {
                EnsureDatabaseExists(connectionString);
                EnsureTableExists(connectionString);
                ResetTable(connectionString);
            }
            catch (Exception ex)
            {
                Assert.Inconclusive("SQL Server integration tests require a reachable SQL Server instance. " + ex.Message);
            }

            factory = new SqlServerEntitySessionFactory(new SqlServerEntitySessionFactoryOptions()
            {
                ConnectionString = connectionString,
                EntityTypes = new[] { typeof(SqlServerSessionEntity) }
            });
        }

        [TestMethod]
        public void InsertAndQuery_PopulatesIdentityAndTimestamps()
        {
            using IEntitySession session = factory.CreateSession();
            SqlServerSessionEntity entity = new SqlServerSessionEntity() { Name = "Alpha" };

            int id = session.Insert(entity);
            SqlServerSessionEntity saved = session.Query<SqlServerSessionEntity>().Single();

            Assert.AreEqual(id, saved.Id);
            Assert.IsNotNull(saved.CreatedOn);
            Assert.IsNotNull(saved.UpdatedOn);
            Assert.AreEqual("Alpha", saved.Name);
        }

        [TestMethod]
        public void Update_PersistsChangesAndRefreshesUpdatedOn()
        {
            using IEntitySession session = factory.CreateSession();
            SqlServerSessionEntity entity = new SqlServerSessionEntity() { Name = "Alpha" };
            session.Insert(entity);
            DateTime? createdOn = entity.CreatedOn;

            entity.Name = "Beta";
            session.Update(entity);
            SqlServerSessionEntity saved = session.Query<SqlServerSessionEntity>().Single();

            Assert.AreEqual("Beta", saved.Name);
            Assert.IsTrue(saved.UpdatedOn >= createdOn);
        }

        [TestMethod]
        public void ExistsAndCount_ReflectStoredRows()
        {
            using IEntitySession session = factory.CreateSession();
            session.Insert(new SqlServerSessionEntity() { Name = "Alpha" });
            session.Insert(new SqlServerSessionEntity() { Name = "Beta" });

            Assert.AreEqual(2, session.Count<SqlServerSessionEntity>());
            Assert.IsTrue(session.Exists<SqlServerSessionEntity>(entity => entity.Name == "Alpha"));
        }

        [TestMethod]
        public void DeleteAndDeleteByPredicate_RemoveRows()
        {
            using IEntitySession session = factory.CreateSession();
            SqlServerSessionEntity alpha = new SqlServerSessionEntity() { Name = "Alpha" };
            SqlServerSessionEntity beta = new SqlServerSessionEntity() { Name = "Beta" };
            session.Insert(alpha);
            session.Insert(beta);

            session.Delete(alpha);
            session.Delete<SqlServerSessionEntity>(entity => entity.Name == "Beta");

            Assert.AreEqual(0, session.Count<SqlServerSessionEntity>());
        }

        [TestMethod]
        public void SqlQuery_ReturnsMappedEntities()
        {
            using IEntitySession session = factory.CreateSession();
            session.Insert(new SqlServerSessionEntity() { Name = "Alpha" });

            SqlServerSessionEntity saved = session.SqlQuery<SqlServerSessionEntity>("SELECT Id, Name, CreatedOn, UpdatedOn FROM " + TableName).Single();

            Assert.AreEqual("Alpha", saved.Name);
        }

        [TestMethod]
        public void Query_UsesFreshNoTrackingContextsAcrossCalls()
        {
            using IEntitySession session = factory.CreateSession();
            SqlServerSessionEntity entity = new SqlServerSessionEntity() { Name = "Alpha" };
            session.Insert(entity);

            SqlServerSessionEntity first = session.Query<SqlServerSessionEntity>().Single();
            ExecuteNonQuery(connectionString, "UPDATE " + TableName + " SET Name = N'Beta' WHERE Id = " + first.Id);
            SqlServerSessionEntity second = session.Query<SqlServerSessionEntity>().Single();

            Assert.AreEqual("Alpha", first.Name);
            Assert.AreEqual("Beta", second.Name);
        }

        private static string GetConnectionString()
        {
            string fromEnvironment = Environment.GetEnvironmentVariable("FLOW_SHARP_SQLSERVER_TEST_CONNECTION_STRING");

            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return fromEnvironment;
            }

            return "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Initial Catalog=FlowSharpCliftonCoreSqlServerTests;TrustServerCertificate=True";
        }

        private static void EnsureDatabaseExists(string targetConnectionString)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(targetConnectionString);
            string databaseName = builder.InitialCatalog;
            SqlConnectionStringBuilder masterBuilder = new SqlConnectionStringBuilder(targetConnectionString)
            {
                InitialCatalog = "master"
            };

            using SqlConnection connection = new SqlConnection(masterBuilder.ConnectionString);
            connection.Open();
            using SqlCommand command = connection.CreateCommand();
            string quotedDatabaseName = databaseName.Replace("'", "''");
            string escapedDatabaseIdentifier = databaseName.Replace("]", "]]");
            command.CommandText = "IF DB_ID(N'" + quotedDatabaseName + "') IS NULL CREATE DATABASE [" + escapedDatabaseIdentifier + "]";
            command.ExecuteNonQuery();
        }

        private static void EnsureTableExists(string targetConnectionString)
        {
            ExecuteNonQuery(targetConnectionString,
                "IF OBJECT_ID('" + TableName + "', 'U') IS NULL " +
                "BEGIN " +
                "CREATE TABLE " + TableName + " (" +
                "Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SqlServerSessionEntities PRIMARY KEY, " +
                "Name NVARCHAR(200) NOT NULL, " +
                "CreatedOn DATETIME2 NULL, " +
                "UpdatedOn DATETIME2 NULL" +
                ")" +
                " END");
        }

        private static void ResetTable(string targetConnectionString)
        {
            ExecuteNonQuery(targetConnectionString,
                "DELETE FROM " + TableName + ";" +
                "DBCC CHECKIDENT ('" + TableName + "', RESEED, 0);");
        }

        private static void ExecuteNonQuery(string targetConnectionString, string sql)
        {
            using SqlConnection connection = new SqlConnection(targetConnectionString);
            connection.Open();
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        [Table("SqlServerSessionEntities", Schema = "dbo")]
        private sealed class SqlServerSessionEntity : IEntity, ICreateUpdate
        {
            [Key]
            [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
            [Column("Id")]
            public int? Id { get; set; }

            [Column("Name")]
            [MaxLength(200)]
            public string Name { get; set; }

            [Column("CreatedOn")]
            public DateTime? CreatedOn { get; set; }

            [Column("UpdatedOn")]
            public DateTime? UpdatedOn { get; set; }
        }
    }
}
