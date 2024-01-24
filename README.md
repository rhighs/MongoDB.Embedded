## MongoDB.Embedded.CrossPlatform

A .NET package that provides an easy way to integrate and manage a MongoDB server within your .NET applications. It abstracts the complexities of setting up and running a MongoDB instance, allowing the embedded MongoDB executable to be packaged directly within your application's DLLs. This package supports Windows, Linux, and OSX platforms, ensuring a seamless MongoDB experience across different environments. Such a package is particularly useful for testing purposes, in particular, intergration testing.

### Getting Started

#### 1. Installation

Nuget pkg manager Installation:
```
Install-Package rhighs.MongoDB.Embedded.CrossPlatform
```

dotnet-cli:
```
$ dotnet add package rhighs.MongoDB.Embedded.CrossPlatform
```

### Example case scenarios

#### Basic Operations

```csharp
using MongoDB.Embedded.Crossplatform;
using MongoDB.Driver;

// Start the embedded MongoDB server
using (var embeddedServer = new Server())
{
    var client = embeddedServer.Client;
    var database = client.GetDatabase("test");
    var collection = database.GetCollection<YourDataType>("yourCollection");
    {
        // perform some operations...
    }
} // <-- server teardown, all data is deleted
```

#### Dual Server Operations

Simultaneously operate two MongoDB servers, showcasing the package's ability to handle multiple instances:
```csharp
using MongoDB.Embedded.Crossplatform;
using MongoDB.Driver;

using (var server1 = new Server())
using (var server2 = new Server())
{
    var client1 = server1.Client;
    var db1 = client1.GetDatabase("test");
    var collection1 = db1.GetCollection<YourDataType>("yourCollection");

    var client2 = server2.Client;
    var db2 = client2.GetDatabase("test");
    var collection2 = db2.GetCollection<YourDataType>("yourCollection");

    {
        // perform operations using both servers...
    }
}
```
You could really do as much as you want, given you got enough memory in your system and you don't run out of ports to be used.
In fact, every `new Server()` will allocate a new mongod binary in a temporary directory.
TODO: this will be optimised away in next releases

#### Managed Server Instance

An additional layer of abstraction providing more control over the server's lifecycle:
```csharp
using MongoDB.Embedded.Crossplatform;
using MongoDB.Driver;

using (var manager = new ManagedServerInstance())
{
    var instance = manager.Instance;
    var client = instance.Client;
    var db = client.GetDatabase("test");
    var collection = db.GetCollection<YourDataType>("yourCollection");
    {
        // perform operations on the collection...
    }
    manager.TeardownServer();
}
```
the `ManagedServerInstance` allows for access on a mutably shared server instance, this is of help when we need
to preserve the database state across code paths and we don't necessarily need to create a fresh binary right over again.

The `Server` instance takes care of setting up and tearing down the MongoDB server automatically, along with it's data (everything will be
stored under a temporary directory that lives as long as the Server instance).

### Testing with Server

This package comes really handy for integration testing, allowing you to run tests against a real MongoDB instance with minimal setup. Here's an example of how you might write tests:
```csharp
using MongoDB.Driver;
using MongoDB.Embedded;
using Xunit;

public class MongoDBTests
{
    [Fact]
    public void BasicStartupTest()
    {
        using (var embedded = new Server())
        {
            var client = embedded.Client;
        }
    }

    [Fact]
    public async Task ReadWriteTest()
    {
        using (var embedded = new Server())
        {
            var client = embedded.Client;
            var db = client.GetDatabase("test");
            var collection = db.GetCollection<TestClass>("col");

            await collection.InsertOneAsync(new TestClass { Id = 12345, TestValue = "Hello world." });
            var retrieved = await collection.Find(x => x.Id == 12345).SingleOrDefaultAsync();

            Assert.NotNull(retrieved);
            Assert.Equal("Hello world.", retrieved.TestValue);
        }
    }
}
```

### Support and Contributions

For support, questions, or contributions, please consider the following:

- **Issues**: If you encounter any issues or bugs, please report them in the issues section of the GitHub repository.
- **Contributions**: Contributions are welcome! If you'd like to improve or add new features, feel free to fork the repository and submit a pull request.

### [MIT License](LICENSE).
