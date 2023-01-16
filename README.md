# S3DropBox

Amazon S3 Content Synchronization with .NET.

S3DropBox synchronize a directory to a S3 Bucket.

## Where to start

Create AWS account.
Create new S3 Bucket.
Create IAM Account and give it full writes to bucket created in previous step (remember user keys, you will need them bit later).
Pull .Net code.

In app.config file set: 
For LocalFolder set your folder whitch you want to sync.
For AWSBucketName set name of bucket whitch you created yearlier
for AWSAccessKey and AWSSecretKey set IAM account keys that you got after creating account.

## Installation

Build project. 
To install service you will need InstallUtil.exe. It is located in Microsoft.NET\Framework\V**. 
Run command prompt (recomended using administrator rights), open folder containing InstallUtil, and run comand:

```bash
InstallUtil.exe {Folder that contains builded application}\S3DropBox.exe
```

Although Service has "Start Automatically" option, it may require to start it manually. 
In press `Windows + R` and run `services.msc`, find S3SyncService in the list, and start it.

## Usage

On start, if there is content in the bucket, that is absent in local folder, service will download it. 
Now folders are synced, and if you remove content from local folder, it will remove content and on S3 too. If you'll add something to folder, it will upload to bucket.

S3DropBox service writes logs to system events, so if something strange happens, you can debug it using `Event Viewer`

## Uninstall

If you want to stop using it temporary, just stop it from `services.msc`. 

If you want to remove it completely, from command prompt run:

```bash
InstallUtil.exe -u {Folder that contains builded application}\S3DropBox.exe
```