using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// Attach this script to an empty game object.

public class Main : MonoBehaviour
{
    List<string> keys;

    void Start()
    {
        // Will fail here. Will succeed at end of script.
        // Addressables.LoadScene("Scenes/Many Trees Scene.unity");

        keys = new List<string>();

        // keys.Add("Scenes/New Wood Scene.unity");
        // keys.Add("Scenes/Tree And Rock Scene.unity");
        keys.Add("Scenes/Many Trees Scene.unity");
        // keys.Add("Scenes/Rock Scene.unity");

        // keys.Add("Wood Bundle/Wood.jpg");
        // keys.Add("Ground Bundle/Cracked Ground.jpg");
        // keys.Add("NatureManufacture Assets/Forest Environment Dynamic Nature/Stumps Roots and Branches/Models/Textures/T_Beech_ground_roots_01_N.tga");
        // keys.Add("NatureManufacture Assets/Forest Environment Dynamic Nature/Stumps Roots and Branches/Models/Textures/T_Beech_ground_roots_01_MaskMap.tga");
        // keys.Add("NatureManufacture Assets/Forest Environment Dynamic Nature/Stumps Roots and Branches/Models/Textures/T_Beech_ground_roots_01_MASKA.png");
        // keys.Add("NatureManufacture Assets/Forest Environment Dynamic Nature/Stumps Roots and Branches/Models/Textures/T_Beech_ground_roots_01_BC.tga");
        // keys.Add("NatureManufacture Assets/Forest Environment Dynamic Nature/Stumps Roots and Branches/Models/Textures/T_beech_forest_scarps_01_N.png");
        // keys.Add("NatureManufacture Assets/Forest Environment Dynamic Nature/Stumps Roots and Branches/Models/Textures/T_beech_forest_scarps_01_MT_AO_SM.tga");

        ///// VALIDATE KEYS /////

        var ok = AssetLoader.Instance.ValidateKeys(keys, ValidateKeysCompleted);

        if (!ok) {
            Debug.Log("Asset loader already running");
            return;
        }
    }

    void ValidateKeysCompleted(bool success, HashSet<string> badKeys)
    {
        if (!success) {
            Debug.Log("Validate keys failed");
            return;
        }

        if (badKeys.Count != 0) {
            foreach (var badKey in badKeys) {
                Debug.Log("Bad key found: " + badKey);
            }
            return;
        }

        Debug.Log("Check keys call succeeded");

        ///// GET DOWNLOAD SIZE /////

        var ok = AssetLoader.Instance.GetDownloadSize(keys, CheckSizeCompleted);
        
        if (!ok) {
            Debug.Log("Asset loader already running");
            return;
        }
    }

    void CheckSizeCompleted(bool success, long size)
    {
        if (!success) {
            Debug.Log("Check size failed");
            return;
        }

        Debug.Log("Check size succeeded with: " + String.Format("{0:n0}", size));

        ///// START DOWNLOAD /////

        var ok = AssetLoader.Instance.DownloadAssets(keys, DownloadProgress, DownloadCompleted);
        
        if (!ok) {
            Debug.Log("Asset loader already running");
            return;
        }
    }

    void DownloadProgress(float progress, string message)
    {
        Debug.Log(message);
    }

    void DownloadCompleted(bool success, string message)
    {
        if (!success) {
            Debug.Log("Download failed: " + message);
            return;
        }

        Debug.Log("Download succeeded!");

        Addressables.LoadScene("Scenes/Many Trees Scene.unity");
    }
}
