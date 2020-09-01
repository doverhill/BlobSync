using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
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

        public static async Task Sync(string connectionString, string containerName, string localPath, SyncSettings settings)
        {
            var sync = new BlobSyncCore(connectionString, containerName, localPath);
            var syncInfo = await sync.GetSyncInfoAsync();

            var account = CloudStorageAccount.Parse(connectionString);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(containerName);

            foreach (var onlyRemote in syncInfo.OnlyRemote)
            {
                Console.WriteLine($"Downloading {onlyRemote.Name}...");
                var blob = container.GetBlockBlobReference(onlyRemote.Name);
                var path = Path.Combine(localPath, onlyRemote.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                await blob.DownloadToFileAsync(path, FileMode.Create);
            }

            foreach (var differs in syncInfo.Differs)
            {
                Console.WriteLine($"Updating {differs.Blob.Name}...");
                var blob = container.GetBlockBlobReference(differs.Blob.Name);
                await blob.DownloadToFileAsync(Path.Combine(localPath, differs.Blob.Name), FileMode.Create);
            }

            if ((settings & SyncSettings.Delete) != 0)
            {
                foreach (var onlyLocal in syncInfo.OnlyLocal)
                {
                    Console.WriteLine($"Deleting {onlyLocal}...");
                    File.Delete(Path.Combine(localPath, onlyLocal.Name));
                }
            }

            foreach (var identical in syncInfo.Identical)
            {
            }
        }
    }
}
