using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BlobSync
{
    public class BlobSyncCore
    {
        private string ConnectionString;
        private string ContainerName;
        private string LocalPath;

        public BlobSyncCore(string connectionString, string containerName, string localPath)
        {
            ConnectionString = connectionString;
            ContainerName = containerName;
            LocalPath = localPath;
        }

        private async Task<List<IListBlobItem>> ListBlobsAsync(CloudBlobContainer container)
        {
            BlobContinuationToken continuationToken = null;
            List<IListBlobItem> results = new List<IListBlobItem>();
            do
            {
                var response = await container.ListBlobsSegmentedAsync(continuationToken);
                continuationToken = response.ContinuationToken;
                results.AddRange(response.Results);
            }
            while (continuationToken != null);
            return results;
        }

        public async Task<BlobSyncInfo> GetSyncInfoAsync()
        {
            var account = CloudStorageAccount.Parse(ConnectionString);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(ContainerName);

            var blobs = await ListBlobsAsync(container);
            var syncInfo = new BlobSyncInfo();
            var seenBlobNames = new HashSet<string>();

            // look at all blobs and place them in the correct category
            foreach (var blob in blobs)
            {
                if (blob is CloudBlockBlob)
                {
                    var blockBlob = (CloudBlockBlob)blob;
                    seenBlobNames.Add(blockBlob.Name);

                    // does the file exist?
                    var localPath = Path.Combine(LocalPath, blockBlob.Name);
                    if (File.Exists(localPath))
                    {
                        // is the same? look at length and md5
                        var fileInfo = GetFileData(localPath);
                        if (fileInfo.MD5 == blockBlob.Properties.ContentMD5 &&
                            fileInfo.Length == blockBlob.Properties.Length)
                        {
                            syncInfo.Identical.Add(blockBlob);
                        }
                        else
                        {
                            syncInfo.Differs.Add(blockBlob);
                        }
                    }
                    else
                    {
                        // file does not exist locally
                        syncInfo.OnlyRemote.Add(blockBlob);
                    }
                }
            }

            // look at all files, identify those that have no corresponding blob and put them in the onlyLocal category
            foreach (var filePath in Directory.EnumerateFiles(LocalPath))
            {
                var fileName = Path.GetFileName(filePath);

                if (!seenBlobNames.Contains(fileName))
                {
                    syncInfo.OnlyLocal.Add(fileName);
                }
            }

            return syncInfo;
        }

        private FileData GetFileData(string localPath)
        {
            var fileData = new FileData();

            var fileInfo = new FileInfo(localPath);
            fileData.Length = fileInfo.Length;

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
