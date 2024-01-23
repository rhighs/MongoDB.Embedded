## MongoDB.Embedded.CrossPlatform

A .NET package that provides an easy way to integrate and manage a MongoDB server within your .NET applications. It abstracts the complexities of setting up and running a MongoDB instance, allowing the embedded MongoDB executable to be packaged directly within your application's DLLs. This package supports Windows, Linux, and OSX platforms, ensuring a seamless MongoDB experience across different environments. Such a package is particularly useful for testing purposes, in particular, intergration testing.

### Getting Started

#### 1. Installation

To install Server, you can use NuGet package manager. The following command will install the package into your project:

```
Install-Package MongoDB.Embedded.CrossPlatform
```

#### 2. Basic Usage

Here is a simple example of how to use Server to start a MongoDB instance and perform basic operations:

```csharp
using MongoDB.Embedded;
using MongoDB.Driver;

// Initialize the embedded MongoDB server
using (var embeddedServer = new Server())
{
    // Get the MongoClient
    var client = embeddedServer.Client;

    // Use the client as you would normally...
    var database = client.GetDatabase("yourDatabase");
    var collection = database.GetCollection<YourDataType>("yourCollection");
    // Perform operations (CRUD) on the collection
}
```

The `Server` instance takes care of setting up and tearing down the MongoDB server automatically.

#### 3. Advanced Configuration

Server offers several configuration options to tailor the MongoDB instance to your needs, such as setting custom database paths, enabling logging, and more.

```csharp
using MongoDB.Embedded;

var customSettings = new ServerSettings
{
    LogPath = "path/to/your/logs",
    DatabasePath = "path/to/your/database",
    LogEnabled = true
};

using (var embeddedServer = new Server(customSettings))
{
    IMongoClient client = embeddedServer.Client;
    // Your code to interact with the mongodb client, already setup and connected!
}
```

### Testing with Server

Server is ideal for integration testing, allowing you to run tests against a real MongoDB instance with minimal setup. Here's an example of how you might write tests:

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
            // Perform tests using the client...
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
