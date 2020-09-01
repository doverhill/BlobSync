using Microsoft.Azure.Storage.Blob;
using System.Collections.Generic;

namespace BlobSync
{
    public class BlobSyncInfo
    {
        public List<CloudBlockBlob> OnlyRemote = new List<CloudBlockBlob>();
        public List<(CloudBlockBlob Blob, FileData File)> Differs = new List<(CloudBlockBlob Blob, FileData File)>();
        public List<(CloudBlockBlob Blob, FileData File)> Identical = new List<(CloudBlockBlob Blob, FileData File)>();
        public List<FileData> OnlyLocal = new List<FileData>();
    }
}