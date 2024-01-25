using System.IO.Compression;
using System.Runtime.InteropServices;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.Build.Framework;

public class InstallMongodTask : Microsoft.Build.Utilities.Task
{
    private static readonly HttpClient client = new HttpClient();

    public override bool Execute()
    {
        try
        {
            var path = DownloadMongoDBPackageAsync().GetAwaiter().GetResult();
            Log.LogMessage(MessageImportance.High, $"Installing package {path}...");
            InstallMongoDB(path, "mongod");
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, true);
            return false;
        }
        return true;
    }

    public async Task<string> DownloadMongoDBPackageAsync()
    {
        string baseUrl = "https://fastdl.mongodb.org/";
        string fileUrl = baseUrl + GetPackagePath();

        Console.WriteLine($"Downloading MongoDB package from: {fileUrl}");
        byte[] fileBytes = await client.GetByteArrayAsync(fileUrl);
        string localFileName = GetLocalFileName(fileUrl);
        await System.IO.File.WriteAllBytesAsync(localFileName, fileBytes);
        return localFileName;
    }

    private string GetPackagePath()
    {
        string os = GetOSPlatform();
        string architecture = RuntimeInformation.OSArchitecture.ToString().ToLower();

        string version = "6.0.13";
        string baseFileName = $"mongodb-{os}-{architecture}-{version}";
        if (os == "linux")
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string distro = "ubuntu2004";
                baseFileName = $"mongodb-linux-{architecture}-{distro}-{version}";
            }
        }
        else if (os == "windows")
        {
            baseFileName = $"mongodb-windows-x86_64-{version}";
        }
        else if (os == "osx")
        {
            baseFileName = $"mongodb-macos-{architecture}-{version}";
        }

        return $"{os}/{baseFileName}.tgz";
    }

    private string GetOSPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "osx";
        throw new PlatformNotSupportedException("Operating system not supported");
    }

    private string GetLocalFileName(string fileUrl)
    {
        Uri uri = new Uri(fileUrl);
        return uri.Segments[^1];
    }

    public void InstallMongoDB(string packagePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);
        string unpackedDirectory = UnpackFile(packagePath);

        // this is needed as the dir name inside the tar/zip archive is pretty much random
        var directories = Directory.GetDirectories(unpackedDirectory);
        if (directories.Length == 0)
        {
            throw new DirectoryNotFoundException(
                $"No directories found in the unpacked directory: {unpackedDirectory}"
            );
        }
        string rootDirectory = directories[0];

        string executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "mongod.exe"
            : "mongod";
        string executablePath = Path.Combine(rootDirectory, "bin", executableName);

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                $"The executable {executableName} was not found in the unpacked files."
            );
        }

        string destinationExecutablePath = Path.Combine(destinationPath, executableName);
        File.Move(executablePath, destinationExecutablePath, true);

        Console.WriteLine($"MongoDB has been installed to {destinationExecutablePath}");
    }

    private string UnpackFile(string packagePath)
    {
        string unpackedDirectory = Path.Combine(
            Path.GetDirectoryName(packagePath),
            Path.GetFileNameWithoutExtension(packagePath)
        );
        Console.WriteLine($"Creating unpack dir {unpackedDirectory} for package {packagePath}...");
        Directory.CreateDirectory(unpackedDirectory);

        if (packagePath.EndsWith(".tgz"))
        {
            ExtractTgz(packagePath, unpackedDirectory);
        }
        else if (packagePath.EndsWith(".zip"))
        {
            ZipFile.ExtractToDirectory(packagePath, unpackedDirectory);
        }
        else
        {
            throw new InvalidOperationException("Unsupported package format.");
        }

        return unpackedDirectory;
    }

    private void ExtractTgz(string gzArchiveName, string destFolder)
    {
        Log.LogMessage(
            MessageImportance.High,
            $"Extracting archive data {gzArchiveName} to {destFolder}"
        );

        using (Stream inStream = File.OpenRead(gzArchiveName))
        using (Stream gzipStream = new GZipInputStream(inStream))
        using (TarArchive tarArchive = TarArchive.CreateInputTarArchive(gzipStream))
        {
            tarArchive.ExtractContents(destFolder);
        }

        Log.LogMessage(MessageImportance.High, "Extraction complete.");
    }
}
