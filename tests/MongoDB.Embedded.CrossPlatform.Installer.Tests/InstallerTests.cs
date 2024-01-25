using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using MongoDB.Embedded.CrossPlatform.Installer;
using Moq;
using Moq.Protected;
using Xunit;

public class MongoDBDownloaderTests
{
    [Fact]
    public async Task DownloadMongoDBPackageAsync_DownloadsFileSuccessfully()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        var response = new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new ByteArrayContent(new byte[100]),
        };

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handlerMock.Object);
        MongoDBDownloader.Client = httpClient;

        var filePath = await MongoDBDownloader.DownloadMongoDBPackageAsync();

        Assert.True(File.Exists(filePath));
        File.Delete(filePath);
    }
}
