# AzureFileCopier

Copy vast, complex and deep file directory structures to Azure Files, when AzCopy doesn't get the job done.  
This tool was written as a POC to determine if there are faster ways to copy particular folder structures and their contents to Azure than the current tooling allows for. 

Current Version is written in dotnet core 3.1, and as such is **cross platform**, if you run into any problems, feel free to open an issue.  
Azure Storage can suffer from contention of the subscribers to the Azure Storage Queue when there are a high number of subscribers (workers), this is resolved and can be tuned by modifying the batched work item retrieval from the queues to reduce the number of calls each subscriber (queue client) makes to the work queue.

### NOTE: Use of this code is at your own risk, and no warranties are given. Alternatives such as [AzCopy](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-azcopy-v10), [Azure File Sync](https://docs.microsoft.com/en-us/azure/storage/files/storage-sync-files-deployment-guide), and [Azure DataBox](https://docs.microsoft.com/en-us/azure/databox-family/) exist to help migrate data to Azure, and may be a better choice for your scenario.  

After copy job is finished, please remember to destroy the supporting control services,  [Azure Storage Account](https://azure.microsoft.com/en-us/services/storage/) and [Azure Cache for Redis](https://azure.microsoft.com/en-us/services/cache/).

For more information on these cloud services, please see:

[Azure Storage Documentation](https://docs.microsoft.com/en-us/azure/storage/)
[Azure Cache for Redis Documentation](https://docs.microsoft.com/en-us/azure/azure-cache-for-redis/)

## Requirements / Infrastructure

1. Create an extra Azure Storage Account to hold the Azure Storage queues used to copy files and folders to the target Azure Files Share.
2. Create an Azure Redis Cache, for most purposes a Basic C2 cache with 2,5 GB should be sufficient.
3. You will need the connection strings for the target storage account, the name of the target share, the connection string for the control storage account, and the redis cache, and will need to place these in the ```appsettings.json```. 

## Compilation

**Use Visual Studio 2019!**

If you want to build with VS Code, you will need to Compile and install MSBuild, which needs Visual Studio...

https://docs.microsoft.com/dotnet/core/tutorials/with-visual-studio-code

Build Task for Visual Studio Code has not been added to this project, nor has a ```launch.json``` been configured.

## Architecture

The copier logic is based on "at least once" queue message processing, and redis SET data structures.

As it uses a pattern of competing consumers for work placed in Azure Storage Queues, the individual copiers will jitter their backoff on queue message retrieval, in the case of collisions, there is still however the chance that they will occasionally do the same work.

Using Azure Storage and Redis services to maintain our copy job state allows us to parallelize the work, and restart in the case of errors and failures, and buffer in the case slow performance.

By being able to parallelize both analysis and copying of files, we can greatly increase the copy performance, of very large and complex folder structures.  

The current implementation does not use MD5 hashing to check the validity of files copied, this could be implemented in a future version.

## Configuration

Configuration settings are stored in the ```appsettings.json```, and are as follows:

```json
  "RedisCacheConnectionString": "",
```
Connection string for Azure Redis Cache used to store copy job data structures and progress

```json
  "ControlStorageConnectionString": "",
```
Connection string for storage account used to control copy jobs

```json
  "LARGE_FILE_SIZE_BYTES": 10000000,
```
Size we assume for large files which will slow down copy jobs. Approx 10 MB for large file bytes to keep main copy jobs fast.

```json
  "LARGE_FILE_COPY_TIMEOUT": 300,
```
Timesout a message in the queue after 5 minutes, making it reappear for another copy job to try.  
If you are copying very large files to Azure Files, then this may need to be adjusted.

```json
  "TargetStorageConnectionString": "",
```
Connection string for target storage account where files should be copied to

```json
  "TargetAzureFilesShareName": "",
```
This is the target share name in Azure Files where the files should be copied to

```json
  "InstrumentationKey" :  "",
```
If you want telemetry to be collected in Azure App Insights

```json
  "TargetStorageAccountName" : "",
```
The name of the target storage account

```json
  "TargetStorageKey" : ""
```
The access key for the target storage account.
Access keys should be rolled over after they have been used in this fashion.

## Usage

**GENERAL OPTIONS**

```--quietmode``` or ```-q```  
Reduces messages sent to standard output.

**COPY MODE:** 

Analyzes the folder structure under the path given, and creates file and folder creation tasks in an Azure Storage queues.   
Once all folders assigned to a particular have been created, worker jobs start to copy files from the file queues.

Example Usage:
```script
aafccore.exe copy -p E:\testing\testcontent4 -w 16 --pathtoremove testing --excludefolders E:\testing\testcontent4\2,E:\testing\testcontent4\1 --excludefiles office.jpg
```

### Arguments

```--path``` or ```-p``` followed by a path to analyze  
Denotes path containing directory and files to be analyzed and copied to Azure.

```--excludefolders``` or ```-x``` Exclude a comma separated list of folder paths.

```--excludefiles``` Exclude a comma separated list of files.

```--workercount``` or ```-w```
Determines how many jobs will be used for folder analysis.  
Splits the top level folder list into this number of batches.  
Each is assigned a batch number, which is used to name the worker queue in Azure.


```--batchclient```
Determines the client number for batch processing, i.e: Which batch will be processed by this job.

```--batchmode```
Will only start one of a subset of batches, used to start individual copy processes

```destinationsubfolder```
Sets the destination subfolder in the target share.

```pathtoremove```
Will try to remove this prefix string from the path when copying to target share.

Example usage: 
```script
aafccore.exe copy --destinationsubfolder mysub --pathtoremove "\Code\AzureAsyncFileCopier\testing\"
```

To target subfolders and remove prefix path:

```destinationsubfolder```
Sets the destination subfolder in the target share.

```pathtoremove```
Will try to remove this prefix string from the path when copying to target share.

Exmaple usage:  
```script
aafccore.exe copy --destinationsubfolder mysub --pathtoremove "\Code\AzureAsyncFileCopier\testing\"
```

```--largefiles``` or ```-l```  
Sets this job to copy large files from the large files queue.  

Copying large files takes much longer than small files, so we use a separate Azure storage queue for these files.  
On this queue we have a 5 minute timeout for the copying of large files.
After 5 minutes, the copy message will reappear in the queue. If your copy jobs take longer than 5 minutes, you can increase this timeout in the ```app.config``` .
There are 2 settings for large files in the ```app.config```:
- LARGE_FILE_COPY_TIMEOUT  
- LARGE_FILE_SIZE_BYTES  

Use these to tune your copier performance / throughput.
Donot reduce the large file copy timeout, otherwise the copier will just get stuck on large files and keep retrying them!  

**RESETTING**

If you have made a mistake, or think you need to restart, feel free to reach out to me with issues / questions on github. The copier will overwrite files by default, and jobs can be started / batched. It maintains a set of copied folders, so as not to copy them again, and is not designed to sync changing directory structures, which can be done with Azure File Sync.   
If you need to reset, use reset mode to just wipe out the Azure Storage Queues and clear the redis cache.

