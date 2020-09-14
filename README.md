# Unity AutoBundles
Extension to Unity Engine's Addressables for making it easier to distribute large projects and keep mobile download size small.

* Just create a folder for all your assets.
* Automatically generate Addressables and keep them synced.
* Creates one .bundle file for each asset, resulting in your app only downloading or updating EXACTLY what it needs, reducing caching, bandwidth and storage dramatically.
* Heavy dependency checking. Only bundles things that are referenced, and splits out assets that are used by multiple assets.
* Creates the smallest number of bundles necessary to download only what's needed for each scene.
* Allows you to force some assets to become addressable, even if they're unreferenced.
* The goal is extreme automation. No babysitting. Just a giant bucket of assets, and the script does the right thing.

# How does it work?

You just arrange all your assets in a folder called AutoBundles:

![AutoBundles](https://github.com/perholmes/UnityAutoBundles/raw/master/Images/folders.png)

Addressables are automatically created for everything in this folder, each single item as its own Addressable.

![AutoBundles](https://github.com/perholmes/UnityAutoBundles/raw/master/Images/mapping.png)

# Workflow

* Organize your assets in folders under AutoBundles.
* Each top-level folder becomes an Addressable group.
* You only organize the AutoBundles folder. Addressables are magically created, optimized and synced.
* Each asset become an addressable unless:
  * It's not the right type or file extension.
  * It has zero or one parents.
  * It has zero or one ultimate parents (ensuring that we don't create multiple bundles for what is ultimately just pieces of one asset).
  * It's not part of any scene.
  * It's too small to warrant its own bundle file. Gets duplicated into multiple bundles instead, to reduce number of bundles.
* Any assets not bundled are still included by the Addressables framework if needed, just not in a separate bundle file.
* You can force the inclusion of an asset by adding the label "ForceAddressable", e.g. if you intend to address the asset manually, and it's currently unreferenced.
* You only push actual changes to the CDN.
* User only downloads what's needed for the scenes/assets they're intending to run.

# Why?

* Multi-file bundles are bad for users, because they result in extreme over-caching especially on mobile.
* By making every texture, prefab, material, or whatever, its own Addressable, downloading and updating is as nimble as possible.
* Manually labelling Addressables is a drag, especially if you have 10,000 assets.
* Manually optimizing Addressables is a drag. AutoBundles creates the lowest number of bundles that still give you individual download access to the assets you care about.
* You already have a folder structure. Why not just lean on that? 
* AutoBundles is two-click automation. Hopefully zero-click in the future.

# How To Use It:

Simply open the AutoBundles **Analyze** Rule:
 
![AutoBundles](https://github.com/perholmes/UnityAutoBundles/raw/master/Images/analyze.png)
 
Then press **Fix Selected Rules**, and the Addressables are updated to mirror the AutoBundles folder structure. Only groups starting with "(Auto)" are synced, and tagging of individual assets is preserved.

# Downsides

* It uses more file handles to open more bundles at runtime. But this is drastically reduced in latest version.
* It's more web requests. But with concurrent requests set to just 32, you don't feel any slowdown.

# How To Install

* Copy Editor/AutoBundles.cs into your project.
* Copy Scripts/… if you want the testing scripts.
* Create a folder in Assets called AutoBundles.
* Make sure that your "Packed Assets" settings (Assets/AddressableAssetsData/AssetGroupTemplates/Packed Assets) has the following settings:
  * Bundle Mode: Pack Separately
  * Bundle Naming: Use Hash Of AssetBundle
* Make sure your Addressable settings are 32 concurrent downloads.
* The Hash-based naming (e.g. "3c3b1761ce87715d3177d2c5ec7d27ac.bundle ") is to frustrate reverse-engineering by making all assets incomprehensible. In the future, encryption should also be added.

# Contributing

If others find this useful, maybe we can work together on designing the script better.

Cheers,

Per Holmes

Hollywood Camera Work

www.hollywoodcamerawork.com


