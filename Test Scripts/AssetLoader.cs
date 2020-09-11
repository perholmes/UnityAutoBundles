using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.AsyncOperations;
using System;

public class AssetLoader : Singleton<AssetLoader>
{
    public delegate void CheckKeysCallback(bool success, HashSet<string> badKeys);
    public delegate void CheckSizeCallback(bool success, long size); 
    public delegate void ProgressCallback(float progress, string message);
    public delegate void DownloadCompleteCallback(bool success, string message);

    CheckKeysCallback checkKeysCallback = null;
    CheckSizeCallback checkSizeCallback = null;
    ProgressCallback dlProgressCallback = null;
    DownloadCompleteCallback dlCompleteCallback = null;

    AsyncOperationHandle<long> sizeCheckHandle;
    AsyncOperationHandle<IList<IResourceLocation>> keyCheckHandle;
    AsyncOperationHandle downloadHandle;

    float progressBase = 1f;
    List<long> bytesAtTime = new List<long>();
    List<float> bytesTime = new List<float>();

    HashSet<string> keysSet;

    bool running = false;

	protected virtual void Start ()
	{
		// Don't put anything important here, it's called late.
	}

    //
    // Check key validity
    //

    public bool ValidateKeys(List<string> keys, CheckKeysCallback cb)
    {
        // Check whether keys are valid.

        if (running) {
            return false;
        }
        running = true;

        checkKeysCallback = cb;

        keysSet = new HashSet<string>(keys); // Save for next function
        keyCheckHandle = Addressables.LoadResourceLocationsAsync(keys.ToArray(), Addressables.MergeMode.Union);
        keyCheckHandle.Completed += LocationsComplete;

        return true;
    }

	void LocationsComplete(AsyncOperationHandle<IList<IResourceLocation>> handle)
	{   
        // Return all bad keys to the callback.

        if (handle.Status == AsyncOperationStatus.Succeeded) {
            foreach (var loc in handle.Result) {
                keysSet.Remove(loc.PrimaryKey);
            }
            HashSet<string> badKeys = new HashSet<string>(keysSet);
            keysSet.Clear();
            running = false;
            checkKeysCallback(true, badKeys);
        } else {
            keysSet.Clear();
            running = false;
            checkKeysCallback(false, null);
        }
	}

    //
    // Size Checking
    //

    public bool GetDownloadSize(List<string> keys, CheckSizeCallback cb)
    {
        // Get the download size of the list of keys, including their dependencies.

        if (running) {
            return false;
        }
        running = true;
        
        checkSizeCallback = cb;

        sizeCheckHandle = Addressables.GetDownloadSizeAsync(keys.ToArray());
        sizeCheckHandle.Completed += DownloadSizeComplete;

        return true;
    }

	void DownloadSizeComplete(AsyncOperationHandle<long> handle)
	{
        // Return download size

        var status = handle.Status;
        running = false;

        if (status == AsyncOperationStatus.Succeeded) {
            checkSizeCallback(true, handle.Result);
        } else {
            checkSizeCallback(false, 0);
        }
	}

    //
    // Download
    //

    public bool DownloadAssets(List<string> keys, ProgressCallback pg, DownloadCompleteCallback dl)
    {
        // Download the assets in the list and all their dependencies.

        if (running) {
            return false;
        }
        running = true;
        
        dlProgressCallback = pg;
        dlCompleteCallback = dl;
        progressBase = -1f;

        downloadHandle = Addressables.DownloadDependenciesAsync(keys.ToArray(), Addressables.MergeMode.Union);
        downloadHandle.Completed += DownloadComplete;

        // Start coroutine for progress. Don't use coroutines for anything that can throw
        // an exception (which includes downloading unchecked keys), because it can't be trapped.
        // See https://www.jacksondunstan.com/articles/3718

        StartCoroutine(DownloadProgress());

        return true;
    }

	IEnumerator DownloadProgress()
    {
        // Calculate progress and download speed.

        while (!downloadHandle.IsDone) {
            var status = downloadHandle.GetDownloadStatus();

            // Progress

            float progress = status.TotalBytes != 0 ? (float) status.DownloadedBytes / (float) status.TotalBytes : 0f;

            // Speed

            if (bytesAtTime.Count > 5) {
                bytesAtTime.RemoveAt(0);
                bytesTime.RemoveAt(0);
            }

            bytesAtTime.Add(status.DownloadedBytes);
            bytesTime.Add(Time.time);

            float timeSpan = bytesTime[bytesTime.Count - 1] - bytesTime[0];
            float byteSpan = bytesAtTime[bytesAtTime.Count - 1] - bytesAtTime[0];
            long bytesPerSecond = !Mathf.Approximately(timeSpan, 0) ? (long) (byteSpan / timeSpan) : 0;

            dlProgressCallback(progress, "Progress: " + (progress * 100).ToString("F0") + "%, Speed: " + FormatSize(bytesPerSecond) + "/sec");
            yield return new WaitForSeconds(.2f);
        }
    }

    static readonly string[] suffixes = { "Bytes", "KB", "MB", "GB", "TB", "PB" }; 

    public static string FormatSize(long bytes)  
    {  
        int counter = 0;  
        decimal number = (decimal)bytes; 

        while (Math.Round(number / 1024) >= 1)  
        {  
            number = number / 1024;  
            counter++;  
        }  
        return string.Format("{0:n1}{1}", number, suffixes[counter]);  
    }

	void DownloadComplete(AsyncOperationHandle handle)
	{
        StopCoroutine(DownloadProgress());

        var status = handle.Status;
        running = false;

        if (status == AsyncOperationStatus.Succeeded) {
            dlCompleteCallback(true, "Download completed with success!");
        } else {
            dlCompleteCallback(false, "Download failed with reason: " + handle.OperationException.Message);
        }
	}
}
