using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using MongoDB.Driver;

namespace MongoDB.Embedded.CrossPlatform;

public class Server : IDisposable
{
    #region private
    private Process? _process;

    private bool _logEnabled = false;

    private readonly int _port = 0;

    private string _dbPath = "";

    private string _logPath = "";

    private readonly string _path = "";

    private readonly string _name = "";

    private readonly int PROCESS_END_TIMEOUT = 10000;

    private readonly ManualResetEventSlim _gate = new ManualResetEventSlim(false);

    private OSPlatform _platform = OSPlatform.Windows;

    private Architecture _arch = Architecture.X64;

    private string PlatformExeExt() => _platform == OSPlatform.Windows ? ".exe" : "";

    private string _processFileName = "";

    private string _processArgs = "";

    private bool Is64BitSystem() =>
        _arch == Architecture.X64
        || _arch == Architecture.Arm64
        || Environment.Is64BitOperatingSystem;

    private Action<string>? _logAction = null;
    #endregion

    public bool Active { get; private set; } = false;

    public Server(
        string? executablePath = null,
        string? logPath = null,
        string dbPath = "db",
        bool logEnabled = false,
        bool initOnly = false,
        Action<string>? logAction = null
    )
    {
        logAction ??= (string message) => Console.WriteLine(message);
        _logAction = logAction;

        _logEnabled = logEnabled;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _platform = OSPlatform.Linux;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _platform = OSPlatform.OSX;
        }
        _arch = RuntimeInformation.OSArchitecture;

        var workingDir = $"{Directory.GetCurrentDirectory()}";

        _port = GetRandomUnusedPort();
        KillMongodProcesses(PROCESS_END_TIMEOUT);
        _name = RandomFileName(7);
        _path = Path.Combine(workingDir, RandomFileName(12));
        _dbPath = Path.Combine(_path, dbPath);
        if (logPath == null)
        {
            _logPath = Path.Combine(_path, "logs");
        }

        Directory.CreateDirectory(_path);
        Directory.CreateDirectory(_logPath);
        Directory.CreateDirectory(_dbPath);
        _logAction($"working path created: {_path}");
        _logAction($"logging path created: {_logPath}");
        _logAction($"database path created: {_dbPath}");

        var format = Is64BitSystem() switch
        {
            false
                => "--dbpath {0} --smallfiles --bind_ip 127.0.0.1 --storageEngine=mmapv1 --port {1}",
            true => "--dbpath {0} --bind_ip 127.0.0.1 --port {1}"
        };

        if (logPath != null)
        {
            format += " --journal --logpath {2}.log";
        }

        if (executablePath != null)
        {
            executablePath = Path.Combine(
                Path.Combine(Directory.GetCurrentDirectory(), "mongod"),
                "mongod"
            );
            CopyFilesystemFiles(executablePath, _name);
        }
        else
        {
            CopyEmbededFiles("mongod.mongod", _name);
        }

        _processFileName = Path.Combine(_path, _name + PlatformExeExt());
        _logAction($"embedded data copied at: {_processFileName}");

        if (_platform != OSPlatform.Windows)
        {
#pragma warning disable CA1416
            if (!Chmod(_processFileName))
            {
                throw new Exception($"could not edit file mode for process file: {_processFileName}");
            }
            // File.SetUnixFileMode(
            //     _processFileName,
            //     UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite
            // );
#pragma warning restore CA1416
        }

        _processArgs = string.Format(format, _dbPath, _port, _logPath);
        if (!initOnly)
        {
            Start();
        }
    }

    public void Start() => StartMongodProcess(_processFileName, _processArgs);

    public void Kill() => KillMongodProcesses(PROCESS_END_TIMEOUT);

    ~Server() => Dispose();

#pragma warning disable CS8602
    private void CopyFilesystemFiles(string srcPath, string dstPath)
    {
        var dstStreamPath = Path.Combine(_path, dstPath + PlatformExeExt());
        using (var resourceStream = new FileStream(srcPath, FileMode.Open, FileAccess.Read))
        using (var fileStream = new FileStream(dstStreamPath, FileMode.Create, FileAccess.Write))
        {
            resourceStream.CopyTo(fileStream);
        }
    }

    private void CopyEmbededFiles(string FName, string SName)
    {
        var assembly = typeof(Server).Assembly;

        // Get all embedded resource names
        var resourceNames = assembly.GetManifestResourceNames();
        Console.WriteLine("Available embedded resources:");
        foreach (var resourceName in resourceNames)
        {
            Console.WriteLine(resourceName);
        }

        var dstStreamPath = Path.Combine(_path, SName + PlatformExeExt());
        using (
            var resourceStream = typeof(Server).Assembly.GetManifestResourceStream(
                $"MongoDB.Embedded.CrossPlatform.{FName}"
            )
        )
        using (var fileStream = new FileStream(dstStreamPath, FileMode.Create, FileAccess.Write))
        {
            resourceStream.CopyTo(fileStream);
        }
    }
#pragma warning restore CS8602

    public MongoClientSettings Settings
    {
        get
        {
            return new MongoClientSettings { Server = new MongoServerAddress("127.0.0.1", _port) };
        }
    }

    public MongoClient Client
    {
        get { return new MongoClient(Settings); }
    }

    private static string RandomFileName(int length)
    {
        var chars = "abcdefghijklmnopqrstuvwxyz1234567890".ToCharArray();
        var data = RandomNumberGenerator.GetBytes(length);
        var result = new StringBuilder(length);
        foreach (byte b in data)
        {
            result.Append(chars[b % chars.Length]);
        }
        return result.ToString();
    }

    private static int GetRandomUnusedPort()
    {
        var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        if (_process != null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(PROCESS_END_TIMEOUT);
                }

                _process.Dispose();
                Active = false;
            }
            catch (Exception exception)
            {
                Trace.TraceWarning(
                    string.Format(
                        "Got exception when disposing the mongod server process msg = {0}",
                        exception.Message
                    )
                );
            }

            _process = null;
        }

        if (Directory.Exists(_path))
        {
#pragma warning disable CS8602, CS8604
            _logAction($"deleting working path at {_path}");
#pragma warning restore CS8602, CS8604
            Directory.Delete(_path, true);
        }
    }

    #region process_procs
#pragma warning disable CS8602, CS8604
    private void StartMongodProcess(string processFileName, string args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = processFileName,
            WorkingDirectory = _path,
            Arguments = args,
            UseShellExecute = false,
            ErrorDialog = false,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            RedirectStandardOutput = true,
        };

        if (_platform == OSPlatform.Windows)
        {
#pragma warning disable CA1416
            startInfo.LoadUserProfile = false;
#pragma warning restore CA1416
        }

        _process = new Process { StartInfo = startInfo };
        _process.OutputDataReceived += ProcessOutputDataReceived;
        _process.ErrorDataReceived += ProcessErrorDataReceived;
        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        _gate.Wait(8000);

        Active = true;
    }

    private void KillMongodProcesses(int millisTimeout)
    {
        foreach (var procname in new string[] { "mongod.exe", "mongod" })
        {
            var processesByName = Process.GetProcessesByName(procname);
            foreach (var process in processesByName)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        _logAction($"killing mongod process {procname}");
                        process.Kill();
                        process.WaitForExit(millisTimeout);
                    }
                }
                catch (Exception exception)
                {
                    var message =
                        $"Got exception when killing {procname} msg = {exception.Message}";
                    Trace.TraceWarning(message);
                    LogInCurrentStdout(message);
                    _logAction(message);
                }
            }
        }
    }

    private void ProcessErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            Trace.WriteLine(string.Format("Err - {0}", e.Data));
            var logMe = string.Format("Err - {0}", e.Data);
            Trace.WriteLine(logMe);
            LogInCurrentStdout(logMe);
            _logAction(logMe);
        }
    }

    private void ProcessOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            if (e.Data.Contains("waiting for connections on port " + _port))
                _gate.Set();

            var logMe = string.Format("Output - {0}", e.Data);
            Trace.WriteLine(logMe);
            LogInCurrentStdout(logMe);
            _logAction(logMe);
        }
    }

    internal void FlushDB()
    {
        _logAction($"flushing database at {_dbPath}");
        if (Directory.Exists(_dbPath))
        {
            Directory.Delete(_dbPath, true);
        }
    }

    internal void FlushLogs()
    {
        _logAction($"flushing logs at {_dbPath}");
        if (Directory.Exists(_logPath))
        {
            Directory.Delete(_logPath, true);
        }
    }
#pragma warning restore CS8602, CS8604
    #endregion

    private void LogInCurrentStdout(string message)
    {
        if (_logEnabled)
        {
            Console.WriteLine($"[Rhighs.MongoDB.Embedded | INFO]: {message}");
        }
    }

    // starting a process is sorta needed, net6.0 does not provide useful APIs for this yet
    private bool Chmod(string filePath, string permissions = "700")
    {
        try
        {
            using (Process proc = Process.Start("/bin/bash", $"-c \"chmod {permissions} {filePath}\""))
            {
                proc.WaitForExit();
                return proc.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }
}
