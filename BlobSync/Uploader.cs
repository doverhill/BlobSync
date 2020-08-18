using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BlobSync
{
    public class Uploader
    {
        [Flags]
        public enum SyncSettings
        {
            Default = 0,
            Delete = 1
        }

        public static async Task Sync(string connectionString, string containerName, string localPath, SyncSettings settings)
        {
            var sync = new BlobSyncCore(connectionString, containerName, localPath);
            var syncInfo = await sync.GetSyncInfoAsync();

            var account = CloudStorageAccount.Parse(connectionString);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(containerName);

            if ((settings & SyncSettings.Delete) != 0)
            {
                foreach (var onlyRemote in syncInfo.OnlyRemote)
                {
                    // this file exists only as a blob, delete the blob
                    Console.WriteLine($"Deleting blob {onlyRemote.Name}...");
                    var blob = container.GetBlobReference(onlyRemote.Name);
                    await blob.DeleteIfExistsAsync();
                }
            }

            foreach (var differs in syncInfo.Differs)
            {
                // this file exists also as a blob but differs, upload and overwrite
                Console.WriteLine($"Updating blob {differs.Name}...");
                var blob = container.GetBlockBlobReference(differs.Name);
                await blob.UploadFromFileAsync(Path.Combine(localPath, differs.Name));
            }

            foreach (var identical in syncInfo.Identical)
            {
                // this file exists also as a blob and is identical, don't do anything
            }

            foreach (var onlyLocal in syncInfo.OnlyLocal)
            {
                // this file exists only locally so upload it as a blob
                Console.WriteLine($"Uploading blob {onlyLocal}...");
                var blob = container.GetBlockBlobReference(onlyLocal);
                await blob.UploadFromFileAsync(Path.Combine(localPath, onlyLocal));
            }
        }
    }
}
