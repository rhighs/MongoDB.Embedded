using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace MongoDB.Embedded.CrossPlatform;

public class ManagedServerInstance : IDisposable
{
    private static Server? _server = null;

    private static ILogger? _logger = null;

    private static string _memberName = "";

    private static string _filePath = "";

    private static int _lineNumber = 0;

    public Server Instance
    {
        get
        {
            return _server != null
                ? _server
                : throw new Exception("Server instance is not initialized");
        }
    }

    public ManagedServerInstance(
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0
    )
    {
        _memberName = memberName;
        _filePath = filePath;
        _lineNumber = lineNumber;
        if (_server == null || !_server.Active)
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });
            _logger = loggerFactory.CreateLogger<ManagedServerInstance>();
            _server = new Server(
                logEnabled: true,
                logAction: (string message) =>
                    _logger.LogInformation(
                        $"[ManagedServerIstance -> {_filePath}:{_memberName}:{_lineNumber}]: {message}"
                    )
            );
        }
    }

#pragma warning disable CS8602, CS8604
    public void Dispose()
    {
        _logger.LogInformation(
            $"[ManagedServerIstance -> {_filePath}:{_memberName}:{_lineNumber}]: disposing server manager"
        );
    }

    public void TeardownServer()
    {
        _server.Dispose();
    }
#pragma warning restore CS8602, CS8604

    ~ManagedServerInstance() => Dispose();
}
