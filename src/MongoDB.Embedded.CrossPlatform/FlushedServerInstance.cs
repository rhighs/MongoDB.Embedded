using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace MongoDB.Embedded.CrossPlatform;

public class FlushedServerInstance : IDisposable
{
    private static Server? _server = null;

    private static ILogger? _logger = null;

    private static string _memberName = "";

    private static string _filePath = "";

    private static int _lineNumber = 0;

    public FlushedServerInstance(
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
                initOnly: true,
                logAction: (string message) =>
                    _logger.LogInformation(
                        $"[FlushedServerInstance -> {_filePath}:{_memberName}:{_lineNumber}]: {message}"
                    )
            );
            _server.Start();
        }
    }

#pragma warning disable CS8602, CS8604
    public void Dispose()
    {
        _logger.LogInformation(
            $"[FlushedServerInstance -> {_filePath}:{_memberName}:{_lineNumber}]: flushing server instance"
        );
        _server.FlushDB();
        _server.FlushLogs();
        _server.Kill();
    }

    public void TeardownServer()
    {
        _server.Dispose();
    }
#pragma warning restore CS8602, CS8604
}
