using Microsoft.WindowsAzure.Storage.Blob;
using System.Collections.Generic;

namespace BlobSync
{
    public class BlobSyncInfo
    {
        public List<CloudBlockBlob> OnlyRemote = new List<CloudBlockBlob>();
        public List<CloudBlockBlob> Differs = new List<CloudBlockBlob>();
        public List<CloudBlockBlob> Identical = new List<CloudBlockBlob>();
        public List<string> OnlyLocal = new List<string>();
    }
}