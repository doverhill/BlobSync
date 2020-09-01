using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using System;
using System.Collections.Generic;
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
            Delete = 1,
            Force = 2
        }

        public static async Task Sync(string connectionString, string containerName, string localPath, SyncSettings settings)
        {
            var contentTypeMappings = new Dictionary<string, string>
            {
                { ".html", "text/html" },
                { ".css", "text/css" },
                { ".js", "text/javascript" },
                { ".wasm", "application/wasm" }
            };

            var sync = new BlobSyncCore(connectionString, containerName, localPath);
            var syncInfo = await sync.GetSyncInfoAsync();

            var account = CloudStorageAccount.Parse(connectionString);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(containerName);

            if (settings.HasFlag(SyncSettings.Delete))
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
                await PushFile("Updating", localPath, contentTypeMappings, container, differs.Blob, differs.File);
            }

            foreach (var onlyLocal in syncInfo.OnlyLocal)
            {
                // this file exists only locally so upload it as a blob
                await PushFile("Uploading", localPath, contentTypeMappings, container, onlyLocal);
            }

            if (settings.HasFlag(SyncSettings.Force))
            {
                foreach (var identical in syncInfo.Identical)
                {
                    // this file exists also as a blob and is identical, don't do anything
                    await PushFile("Force updating", localPath, contentTypeMappings, container, identical.Blob, identical.File);
                }
            }
        }

        private static async Task PushFile(string verb, string localPath, Dictionary<string, string> contentTypeMappings, CloudBlobContainer container, CloudBlockBlob obj, FileData file)
        {
            var blob = container.GetBlockBlobReference(obj.Name);
            ApplyContentType(blob, contentTypeMappings);
            ApplyCaching(blob, file);
            Console.WriteLine($"{verb} blob {obj.Name} [{blob.Properties.ContentType}]...");
            await blob.UploadFromFileAsync(Path.Combine(localPath, obj.Name));
            await blob.SetPropertiesAsync();
        }

        private static async Task PushFile(string verb, string localPath, Dictionary<string, string> contentTypeMappings, CloudBlobContainer container, FileData file)
        {
            var blob = container.GetBlockBlobReference(file.Name);
            ApplyContentType(blob, contentTypeMappings);
            ApplyCaching(blob, file);
            Console.WriteLine($"{verb} blob {file.Name} [{blob.Properties.ContentType}]...");
            await blob.UploadFromFileAsync(Path.Combine(localPath, file.Name));
            await blob.SetPropertiesAsync();
        }

        private static void ApplyCaching(CloudBlockBlob blob, FileData file)
        {
            blob.Properties.CacheControl = "max-age=600";
        }

        private static void ApplyContentType(CloudBlockBlob blob, Dictionary<string, string> contentTypeMappings)
        {
            var suffix = Path.GetExtension(blob.Name);
            if (contentTypeMappings.ContainsKey(suffix))
            {
                blob.Properties.ContentType = contentTypeMappings[suffix];
            }
        }
    }
}
