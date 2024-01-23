using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Xunit;

namespace MongoDB.Embedded.CrossPlatform.Tests;

public class ManagedServerInstanceTests
{
    private class TestClass
    {
        public int Id { get; set; }
        public string TestValue { get; set; }
    }

    [Fact]
    public void ManagedServerInstanceStartupTest()
    {
        using (var manager = new ManagedServerInstance())
        {
            using var instance = manager.Instance;
            Assert.NotNull(instance);
            var client = instance.Client;
            Assert.NotNull(client);
        }
    }

    [Fact]
    public async void ManagedServerInstanceDatabaseCreationTest()
    {
        using (var manager = new ManagedServerInstance())
        {
            using var instance = manager.Instance;
            var client = instance.Client;
            var db = client.GetDatabase("test");
            Assert.NotNull(db);
            await db.CreateCollectionAsync("testCollection");
            var collections = await (await db.ListCollectionsAsync()).ToListAsync();
            Assert.Contains(collections, c => c["name"] == "testCollection");
        }
    }

    [Fact]
    public async void ManagedServerInstanceReadWriteTest()
    {
        using (var manager = new ManagedServerInstance())
        {
            using var instance = manager.Instance;
            var client = instance.Client;
            var db = client.GetDatabase("test");
            var collection = db.GetCollection<TestClass>("testCollection");
            await collection.InsertOneAsync(new TestClass() { Id = 1, TestValue = "Test Value" });
            var retrieved = await collection.Find(x => x.Id == 1).SingleOrDefaultAsync();

            Assert.NotNull(retrieved);
            Assert.Equal("Test Value", retrieved.TestValue);
        }
    }

    [Fact]
    public async void ManagedServerInstanceMultiDocumentReadWriteTest()
    {
        using (var manager = new ManagedServerInstance())
        {
            using var instance = manager.Instance;
            var client = instance.Client;
            var db = client.GetDatabase("test");
            var collection = db.GetCollection<TestClass>("testCollection");

            // Insert multiple documents
            await collection.InsertManyAsync(
                new[]
                {
                    new TestClass() { Id = 1, TestValue = "First Value" },
                    new TestClass() { Id = 2, TestValue = "Second Value" }
                }
            );

            // Retrieve and assert
            var retrievedFirst = await collection.Find(x => x.Id == 1).SingleOrDefaultAsync();
            var retrievedSecond = await collection.Find(x => x.Id == 2).SingleOrDefaultAsync();

            Assert.NotNull(retrievedFirst);
            Assert.Equal("First Value", retrievedFirst.TestValue);
            Assert.NotNull(retrievedSecond);
            Assert.Equal("Second Value", retrievedSecond.TestValue);
        }
    }
}
