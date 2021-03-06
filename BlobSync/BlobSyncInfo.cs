﻿using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Collections.Generic;

namespace BlobSync
{
    public class BlobSyncInfo
    {
        public List<BlobItem> AllRemoteItems = new List<BlobItem>();
        public List<BlobClient> OnlyRemote = new List<BlobClient>();
        public List<(BlobClient Blob, FileData File)> Differs = new List<(BlobClient Blob, FileData File)>();
        public List<(BlobClient Blob, FileData File)> Identical = new List<(BlobClient Blob, FileData File)>();
        public List<FileData> OnlyLocal = new List<FileData>();
    }
}