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
    private Process? _process;

    private bool _logEnabled = false;
    private readonly int _port = 0;
    private readonly string _path = "";
    private string _db_path = "";
    private readonly string _name = "";
    private readonly int PROCESS_END_TIMEOUT = 10000;
    private readonly ManualResetEventSlim _gate = new ManualResetEventSlim(false);
    private OSPlatform _platform = OSPlatform.Windows;
    private Architecture _arch = Architecture.X64;

    private string PlatformExeExt() => _platform == OSPlatform.Windows ? ".exe" : "";

    private bool Is64BitSystem() =>
        _arch == Architecture.X64
        || _arch == Architecture.Arm64
        || Environment.Is64BitOperatingSystem;

    private string CheckLinuxVersion() => "linux.mongod_6_x86-64";

    private string CheckOSXVersion() =>
        _arch == Architecture.Arm64 || _arch == Architecture.Arm64
            ? "osx.mongod_6_arm64"
            : "osx.mongod_6_x86-64";

    public Server(
        string? logPath = null,
        string db_path = "db",
        bool logEnabled = false,
        Action<string> logAction = null
    ){
        logAction ??= (string message) => Console.WriteLine(message);

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

        var workingDir = $"{Directory.GetCurrentDirectory()}\\{RandomFileName(7)}";
        if (_platform == OSPlatform.Windows)
        {
            _db_path = $"{workingDir}\\{db_path}";
            if (logPath == null)
            {
                logPath = $"{workingDir}\\logs";
            }
        }
        else
        {
            _db_path = $"{workingDir}/{db_path}";
            if (logPath == null)
            {
                logPath = $"{workingDir}/logs";
            }
        }

        _port = GetRandomUnusedPort();
        Directory.CreateDirectory(_db_path);
        KillMongoDbProcesses(PROCESS_END_TIMEOUT);
        _name = RandomFileName(7);
        _path = Path.Combine(Directory.GetCurrentDirectory(), RandomFileName(12));
        Directory.CreateDirectory(_path);

        string format = Is64BitSystem() switch
        {
            false
                => "--dbpath {0} --smallfiles --bind_ip 127.0.0.1 --storageEngine=mmapv1 --port {1}",
            true => "--dbpath {0} --bind_ip 127.0.0.1 --port {1}"
        };

        if (logPath != null)
        {
            format += " --journal --logpath {2}.log";
        }

        string executablePath = ResolveExecutablePath();
        CopyEmbededFiles(executablePath, _name);
        var processFileName = Path.Combine(_path, _name + PlatformExeExt());

        if (_platform != OSPlatform.Windows)
        {
#pragma warning disable CA1416
            File.SetUnixFileMode(
                processFileName,
                UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite
            );
#pragma warning restore CA1416
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = processFileName,
            WorkingDirectory = _path,
            Arguments = string.Format(format, _db_path, _port, logPath),
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
    }

    ~Server() => Dispose();

    private string CheckWindowsVersion()
    {
        var windowsBuildNumber = GetWindowsBuildNumber();
        if (windowsBuildNumber < 7600)
        {
            throw new Exception("Windows 7 is not supported yet!");
        }

        string result = "";
        if (windowsBuildNumber >= 10000)
        {
            result = Is64BitSystem() ? "win.mongod_5_x64.exe" : "win.mongod_3_x32.exe";
        }
        else if (
            windowsBuildNumber >= 7600
            || windowsBuildNumber >= 9600 && windowsBuildNumber < 10000
        )
        {
            result = Is64BitSystem() ? "win.mongod_4_2_x64.exe" : "win.mongod_3_x32.exe";
        }
        return result;
    }

    private void CopyEmbededFiles(string FName, string SName)
    {
#pragma warning disable CS8602
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
#pragma warning restore CS8602
    }

    private string ResolveExecutablePath()
    {
        string result = "";
        if (_platform == OSPlatform.OSX)
        {
            result = CheckOSXVersion();
        }
        else if (_platform == OSPlatform.Linux)
        {
            result = CheckLinuxVersion();
        }
        else
        {
            result = CheckWindowsVersion();
        }
        return result;
    }

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
            Directory.Delete(_path, true);
        }
    }

    private void KillMongoDbProcesses(int millisTimeout)
    {
        var processesByName = Process.GetProcessesByName("mongod.exe");
        foreach (var process in processesByName)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(millisTimeout);
                }
            }
            catch (Exception exception)
            {
                var message = string.Format(
                    "Got exception when killing mongod.exe msg = {0}",
                    exception.Message
                );
                Trace.TraceWarning(message);
                LogInCurrentStdout(message);
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
        }
    }

    private void LogInCurrentStdout(string message)
    {
        if (_logEnabled)
        {
            Console.WriteLine($"[Rhighs.MongoDB.Embedded | INFO]: {message}");
        }
    }

    internal static int GetWindowsBuildNumber()
    {
#pragma warning disable CA1416
        var subKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\";
        var keyName = "CurrentBuildNumber";
        using (var rkSubKey = Registry.LocalMachine.OpenSubKey(subKey))
        {
            if (rkSubKey == null)
                throw new Exception(
                    string.Format(
                        @"Error while reading registry key: {0}\{1} does not exist!",
                        subKey,
                        keyName
                    )
                );

            try
            {
                var result = rkSubKey.GetValue(keyName);
                rkSubKey.Close();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    string.Format(
                        "Error while reading registry key: {0} param: {1}. ErrorMessage: {2}",
                        subKey,
                        keyName,
                        ex.Message
                    )
                );
            }
        }
#pragma warning restore CA1416
    }
}
