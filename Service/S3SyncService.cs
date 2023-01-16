using System.ServiceProcess;
using Amazon.S3;
using Amazon.S3.Model;
using System.IO;
using System.Configuration;
using System;
using System.Diagnostics;
using Amazon.Runtime;
using Amazon;
using System.Threading;

namespace S3DropBox.Service
{
    public class S3SyncService : ServiceBase
    {
        private FileSystemWatcher _watcher;
        private IAmazonS3 _client;
        private readonly string _localFolder = ConfigurationManager.AppSettings["LocalFolder"];
        private readonly string _bucketName = ConfigurationManager.AppSettings["AWSBucketName"];
        private readonly string _accessKey = ConfigurationManager.AppSettings["AWSAccessKey"];
        private readonly string _secretKey = ConfigurationManager.AppSettings["AWSSecretKey"];
        EventLog Lg;

        public S3SyncService()
        {
            
        }

        // what we do on service start
        protected override void OnStart(string[] args)
        {
            if (!EventLog.SourceExists("S3SyncLog", "."))
                EventLog.CreateEventSource("S3SyncLog", "Application");
            Lg = new EventLog("Application", ".", "S3SyncLog");
            Lg.WriteEntry("S3Sync started", EventLogEntryType.Information);

            try
            {
                var credentials = new BasicAWSCredentials(_accessKey, _secretKey);
                _client = new AmazonS3Client(credentials, RegionEndpoint.EUCentral1);

                DownloadAllFiles();

            } catch(Exception ex)
            {
                Lg.WriteEntry("oops, we got problem on start: " + ex, EventLogEntryType.Error);
            }

            _watcher = new FileSystemWatcher(_localFolder);
            _watcher.Created += OnFileCreated;
            _watcher.Changed += OnFileChanged;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;
            _watcher.EnableRaisingEvents = true;
        }

        // what we do on service stop
        protected override void OnStop()
        {
            _watcher.Dispose();
            _client.Dispose();
        }

        //adding file and folders
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            //we check, if its folder, we call another method
            if ((File.GetAttributes(e.FullPath) & FileAttributes.Directory) == FileAttributes.Directory)
            {                
                UploadDirectory(e.FullPath);
            }
            else // if now, lets add an file
            {                
                try
                {
                    using (var file = new FileStream(e.FullPath, FileMode.Open))
                    {
                        var request = new PutObjectRequest
                        {
                            BucketName = _bucketName,
                            Key = e.Name,
                            InputStream = file
                        };
                        PutObjectWithRetry(request);
                    }
                }
                catch (Exception ex)
                {
                    Lg.WriteEntry("oops, could not move created file: " + ex, EventLogEntryType.Error);
                }
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Update the file in S3           
            OnFileCreated(sender, e);
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            // Delete the file from S3      
            try
            {
                var request = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = e.Name
                };
                _client.DeleteObject(request);
            }
            catch (Exception ex)
            {
                Lg.WriteEntry("oops, cant delete file: " + ex, EventLogEntryType.Error);
            }           
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            // Delete the old file and upload the new file            
            OnFileDeleted(sender, new FileSystemEventArgs(WatcherChangeTypes.Deleted, e.OldFullPath, e.OldName));
            OnFileCreated(sender, e);
        }

        //we try 3 times to add, with timeout, if still can't add, return error
        private void PutObjectWithRetry(PutObjectRequest request)
        {
            int retries = 3;
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    _client.PutObject(request);
                    return;
                }
                catch (AmazonS3Exception ex)
                {
                    if (ex.ErrorCode != "Throttling" && ex.ErrorCode != "ProvisionedThroughputExceededException")
                        throw;
                    // wait for a moment before retrying
                    Thread.Sleep(3000);
                }
            }
            throw new Exception("Failed to upload object after multiple retries.");
        }

        private void UploadDirectory(string directoryPath)
        {
            var files = Directory.GetFiles(directoryPath);
            var directories = Directory.GetDirectories(directoryPath);

            foreach (var file in files)
            {
                using (var fileStream = new FileStream(file, FileMode.Open))
                {
                    var request = new PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = file.Replace(_localFolder + "\\", "").Replace(@"\","/"),
                        InputStream = fileStream
                    };
                    PutObjectWithRetry(request);
                }
            }

            var emptyStream = new MemoryStream();
            var request2 = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = directoryPath.Replace(_localFolder + "\\", "").Replace(@"\", "/") + "/",
                InputStream = emptyStream
            };
            PutObjectWithRetry(request2);

            foreach (var subDirectory in directories)
            {
                UploadDirectory(subDirectory);
            }
        }

        //check bucket and local folder, download from S3 everything that is not present in local folder. We do this only on service start
        private void DownloadAllFiles()
        {
            var request = new ListObjectsRequest
            {
                BucketName = _bucketName
            };

            var response = _client.ListObjects(request);

            foreach (var s3Object in response.S3Objects)
            {
                var downloadPath = Path.Combine(_localFolder, s3Object.Key);
                if (s3Object.Key.EndsWith("/"))
                {
                    Directory.CreateDirectory(downloadPath);
                }
                else
                {
                    if (!File.Exists(downloadPath))
                    {
                        DownloadFile(downloadPath, s3Object);
                    }
                    else
                    {
                        var localFileInfo = new FileInfo(downloadPath);
                        var s3FileInfo = _client.GetObjectMetadata(new GetObjectMetadataRequest
                        {
                            BucketName = _bucketName,
                            Key = s3Object.Key
                        });

                        if (localFileInfo.LastWriteTime < s3FileInfo.LastModified)
                        {
                            DownloadFile(downloadPath, s3Object);
                        }
                    }
                }
            }
        }

        private void DownloadFile(string downloadPath, S3Object s3Object)
        {
            var downloadRequest = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Object.Key
            };
            using (var downloadResponse = _client.GetObject(downloadRequest))
            using (var fileStream = new FileStream(downloadPath, FileMode.Create))
            {
                downloadResponse.ResponseStream.CopyTo(fileStream);
            }
        }
    }
}