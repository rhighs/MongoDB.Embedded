using MongoDB.Driver;
using Xunit;

namespace MongoDB.Embedded.Tests
{
    public class BasicTests
    {
        [Fact]
        public void BasicStartupTests()
        {
            using (var embedded = new EmbeddedMongoDbServer())
            {
                var client = embedded.Client;
            }
        }

        private class TestClass
        {
            public int Id { get; set; }
            public string TestValue { get; set; }
        }

        [Fact]
        public async void ReadWriteTest()
        {
            using (var embedded = new EmbeddedMongoDbServer())
            {
                var client = embedded.Client;
                var db = client.GetDatabase("test");
                var collection = db.GetCollection<TestClass>("col");
                await collection.InsertOneAsync(new TestClass() { Id = 12345, TestValue = "Hello world." });
                var retrieved = await collection.Find(x => x.Id == 12345).SingleOrDefaultAsync();

                Assert.NotNull(retrieved);
                Assert.Equal("Hello world.", retrieved.TestValue);
            }
        }

        [Fact]
        public async void DualServerReadWriteTest()
        {
            using (var embedded1 = new EmbeddedMongoDbServer())
            using (var embedded2 = new EmbeddedMongoDbServer())
            {
                var client1 = embedded1.Client;
                var db1 = client1.GetDatabase("test");
                var collection1 = db1.GetCollection<TestClass>("col");
                await collection1.InsertOneAsync(new TestClass() { Id = 12345, TestValue = "Hello world." });
                var retrieved1 = await collection1.Find(x => x.Id == 12345).SingleOrDefaultAsync();

                Assert.NotNull(retrieved1);

                Assert.Equal("Hello world.", retrieved1.TestValue);

                var client2 = embedded2.Client;
                var db2 = client2.GetDatabase("test");
                var collection2 = db2.GetCollection<TestClass>("col");
                await collection2.InsertOneAsync(new TestClass() { Id = 12345, TestValue = "Hello world." });
                var retrieved2 = await collection2.Find(x => x.Id == 12345).SingleOrDefaultAsync();

                Assert.NotNull(retrieved2);
                Assert.Equal("Hello world.", retrieved2.TestValue);
            }
        }
    }
}
