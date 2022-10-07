using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace BlobSync
{
    public class BlobSyncCore
    {
        private readonly string ConnectionString;
        private readonly string ConnectionUrl;
        private readonly string ContainerName;
        private readonly string LocalPath;

        public BlobSyncCore(string connectionString, string connectionUrl, string containerName, string localPath)
        {
            ConnectionString = connectionString;
            ConnectionUrl = connectionUrl;
            ContainerName = containerName;
            LocalPath = localPath;
        }

        public static async IAsyncEnumerable<BlobItem> ListBlobsAsync(BlobContainerClient container, string prefix)
        {
            await foreach (var blobItem in container.GetBlobsAsync(prefix: prefix))
            {
                yield return blobItem;
            }
        }

        public static async Task<string> DownloadBlobText(BlobClient blob)
        {
            var download = await blob.DownloadAsync();
            var reader = new StreamReader(download.Value.Content);
            return reader.ReadToEnd();
        }

        public static async Task UploadBlobText(BlobClient blob, string text)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(text);
            writer.Flush();
            stream.Position = 0;
            await blob.UploadAsync(stream, overwrite: true);
        }

        public bool Verify(BlobSyncInfo syncInfo, bool verbose)
        {
            foreach (var item in syncInfo.AllRemoteItems)
            {
                if (verbose)
                {
                    Console.WriteLine("Verifying " + item.Name);
                    Console.WriteLine("  [REMOTE: " + Convert.ToBase64String(item.Properties.ContentHash) + " " + item.Properties.ContentLength + "]");
                }

                // does the file exist?
                var localPath = Path.Combine(LocalPath, item.Name);
                if (File.Exists(localPath))
                {
                    var fileInfo = GetFileData(LocalPath, localPath);
                    if (verbose) Console.WriteLine("  [LOCAL:  " + fileInfo.MD5 + " " + fileInfo.Length + " " + fileInfo.LastModified + " " + localPath + "]");
                    if (fileInfo.MD5 != Convert.ToBase64String(item.Properties.ContentHash) || fileInfo.Length != item.Properties.ContentLength)
                    {
                        if (verbose) Console.WriteLine("  STILL DIFFERS!");
                        return false;
                    }
                }
                else
                {
                    if (verbose) Console.WriteLine("  NOT FOUND!");
                    return false;
                }
            }
            return true;
        }

        public async Task<BlobSyncInfo> GetSyncInfoAsync(bool verbose, bool recursive)
        {
            BlobServiceClient client;

            if (ConnectionUrl != null)
            {
                client = new BlobServiceClient(new Uri(ConnectionUrl));
            }
            else
            {
                client = new BlobServiceClient(ConnectionString);
            }
            var container = client.GetBlobContainerClient(ContainerName);

            var blobs = ListBlobsAsync(container, "");
            var syncInfo = new BlobSyncInfo();
            var seenBlobNames = new HashSet<string>();

            // look at all blobs and place them in the correct category
            await foreach (var item in blobs)
            {
                syncInfo.AllRemoteItems.Add(item);
                seenBlobNames.Add(item.Name);

                if (verbose)
                {
                    Console.WriteLine(item.Name);
                    Console.WriteLine("  [REMOTE: " + Convert.ToBase64String(item.Properties.ContentHash) + " " + item.Properties.ContentLength + " " + item.Properties.LastModified + "]");
                }

                // does the file exist?
                var localPath = Path.Combine(LocalPath, item.Name);
                if (File.Exists(localPath))
                {
                    // is the same? look at length and md5
                    var fileInfo = GetFileData(LocalPath, localPath);
                    if (verbose) Console.Write("  [LOCAL:  " + fileInfo.MD5 + " " + fileInfo.Length + " " + fileInfo.LastModified + " " + localPath + "]: ");
                    if (fileInfo.MD5 == Convert.ToBase64String(item.Properties.ContentHash) &&
                        fileInfo.Length == item.Properties.ContentLength)
                    {
                        if (verbose) Console.WriteLine("MATCH");
                        var blockBlob = container.GetBlobClient(item.Name);
                        syncInfo.Identical.Add((blockBlob, fileInfo));
                    }
                    else
                    {
                        if (verbose) Console.WriteLine("DIFFER");
                        var blockBlob = container.GetBlobClient(item.Name);
                        syncInfo.Differs.Add((blockBlob, fileInfo));
                    }
                }
                else
                {
                    if (verbose) Console.WriteLine("  [LOCAL:  NOT FOUND]");
                    // file does not exist locally
                    var blockBlob = container.GetBlobClient(item.Name);
                    syncInfo.OnlyRemote.Add(blockBlob);
                }
            }

            // look at all files, identify those that have no corresponding blob and put them in the onlyLocal category
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = recursive
            };
            foreach (var filePath in Directory.EnumerateFiles(LocalPath, "*", options))
            {
                var fileName = Path.GetRelativePath(LocalPath, filePath).Replace("\\", "/");

                if (!seenBlobNames.Contains(fileName))
                {
                    var fileInfo = GetFileData(LocalPath, Path.Combine(LocalPath, fileName));
                    syncInfo.OnlyLocal.Add(fileInfo);
                }
            }

            return syncInfo;
        }

        private static FileData GetFileData(string basePath, string localPath)
        {
            var fileData = new FileData();

            var fileInfo = new FileInfo(localPath);
            fileData.Name = Path.GetRelativePath(basePath, localPath);
            fileData.Length = fileInfo.Length;
            fileData.LastModified = fileInfo.LastWriteTimeUtc;

            using (var md5 = MD5.Create())
            {
                using (var stream = fileInfo.OpenRead())
                {
                    fileData.MD5 = Convert.ToBase64String(md5.ComputeHash(stream));
                }
            }

            return fileData;
        }
    }
}
