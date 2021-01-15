using Azure.Storage.Blobs;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BlobSync
{
    public class Downloader
    {
        [Flags]
        public enum SyncSettings
        {
            Default = 0,
            Delete = 1,
            Force = 2
        }

        public static async Task<bool> Sync(string connectionString, string connectionUrl, string containerName, string localPath, SyncSettings settings, bool verbose)
        {
            var sync = new BlobSyncCore(connectionString, connectionUrl, containerName, localPath);
            var syncInfo = await sync.GetSyncInfoAsync(verbose);

            BlobServiceClient client;
            if (connectionUrl != null)
            {
                client = new BlobServiceClient(new Uri(connectionUrl));
            }
            else
            {
                client = new BlobServiceClient(connectionString);
            }
            var container = client.GetBlobContainerClient(containerName);

            foreach (var onlyRemote in syncInfo.OnlyRemote)
            {
                Console.WriteLine($"Downloading {onlyRemote.Name}...");
                var blob = container.GetBlobClient(onlyRemote.Name);
                var path = Path.Combine(localPath, onlyRemote.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                using (var file = File.OpenWrite(path))
                {
                    await blob.DownloadToAsync(file);
                }
            }

            foreach (var differs in syncInfo.Differs)
            {
                Console.WriteLine($"Updating local {differs.Blob.Name}...");
                var blob = container.GetBlobClient(differs.Blob.Name);
                var path = Path.Combine(localPath, differs.Blob.Name);

                File.Delete(path);
                using (var file = File.OpenWrite(path))
                {
                    await blob.DownloadToAsync(file);
                }
            }

            if ((settings & SyncSettings.Delete) != 0)
            {
                foreach (var onlyLocal in syncInfo.OnlyLocal)
                {
                    Console.WriteLine($"Deleting local {onlyLocal}...");
                    File.Delete(Path.Combine(localPath, onlyLocal.Name));
                }
            }

            return sync.Verify(syncInfo, verbose);
        }
    }
}
