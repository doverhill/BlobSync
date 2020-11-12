using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
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

        public static async Task Sync(string connectionString, string connectionUrl, string containerName, string localPath, SyncSettings settings)
        {
            var contentTypeMappings = new Dictionary<string, string>
            {
                { ".html", "text/html" },
                { ".css", "text/css" },
                { ".js", "text/javascript" },
                { ".wasm", "application/wasm" }
            };

            var sync = new BlobSyncCore(connectionString, connectionUrl, containerName, localPath);
            var syncInfo = await sync.GetSyncInfoAsync();

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

            if (settings.HasFlag(SyncSettings.Delete))
            {
                foreach (var onlyRemote in syncInfo.OnlyRemote)
                {
                    // this file exists only as a blob, delete the blob
                    Console.WriteLine($"Deleting blob {onlyRemote.Name}...");
                    var blob = container.GetBlobClient(onlyRemote.Name);
                    await blob.DeleteIfExistsAsync();
                }
            }

            foreach (var differs in syncInfo.Differs)
            {
                // this file exists also as a blob but differs, upload and overwrite
                await PushFile("Updating", localPath, contentTypeMappings, differs.Blob);
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
                    await PushFile("Force updating", localPath, contentTypeMappings, identical.Blob);
                }
            }
        }

        private static async Task PushFile(string verb, string localPath, Dictionary<string, string> contentTypeMappings, BlobClient blob)
        {
            //var blob = container.GetBlockBlobReference(obj.Name);
            var contentType = await ApplyProperties(blob, contentTypeMappings);
            Console.WriteLine($"{verb} blob {blob.Name} [{contentType}]...");
            var localFile = File.OpenRead(Path.Combine(localPath, blob.Name));
            await blob.UploadAsync(localFile);
        }

        private static async Task PushFile(string verb, string localPath, Dictionary<string, string> contentTypeMappings, BlobContainerClient container, FileData file)
        {
            var blob = container.GetBlobClient(file.Name);
            var contentType = await ApplyProperties(blob, contentTypeMappings);
            Console.WriteLine($"{verb} blob {file.Name} [{contentType}]...");
            var localFile = File.OpenRead(Path.Combine(localPath, file.Name));
            await blob.UploadAsync(localFile);
        }

        private static async Task<string> ApplyProperties(BlobClient blob, Dictionary<string, string> contentTypeMappings)
        {
            var suffix = Path.GetExtension(blob.Name);
            string contentType = null;
            if (contentTypeMappings.ContainsKey(suffix))
            {
                contentType = contentTypeMappings[suffix];
            }

            // Get the existing properties
            BlobProperties properties = await blob.GetPropertiesAsync();

            BlobHttpHeaders headers = new BlobHttpHeaders
            {
                ContentType = contentType ?? properties.ContentType,
                ContentLanguage = properties.ContentLanguage,
                CacheControl = properties.CacheControl,
                ContentDisposition = properties.ContentDisposition,
                ContentEncoding = properties.ContentEncoding,
                ContentHash = properties.ContentHash
            };

            // Set the blob's properties.
            await blob.SetHttpHeadersAsync(headers);

            return contentType ?? "";
        }
    }
}
