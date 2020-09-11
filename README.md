# Unity AutoBundles
Extension to Unity Engine's Addressables for making it easier to distribute large projects and keep mobile download size small.

* Just create a folder for all your assets.
* Automatically generate Addressables and keep them synced.
* Creates one .bundle file for each individual asset, resulting in your app only downloading or updating EXACTLY what it needs.
* Dramatically reduce storage and bandwidth usage, which is critical on mobile and great on desktop. Don't use a 5 GB cache on mobile when only 500 MB is needed.
* Reduce your CDN storage with much smaller updates.
* A hands-off approach to Addressables. No manual labeling or maintenance, just a giant bucket of assets, and the app downloads what it needs.

# How does it work?

You just arrange all your assets in a folder called AutoBundles:

![AutoBundles](https://github.com/perholmes/UnityAutoBundles/raw/master/Images/folders.png)

Addressables are automatically created for everything in this folder, each single item as its own Addressable.

![AutoBundles](https://github.com/perholmes/UnityAutoBundles/raw/master/Images/mapping.png)

# Workflow

* Under the AutoBundles folder, you arrange all your assets in appropriate folders, e.g. a "Trees" folder, a "Rocks" folder, a "Scenes" folder.
* You ONLY arrange this folder. You NEVER manually organize Addressables.
* A script scrapes this folder and creates a unique Addressable for every single asset. One addressable per asset.
* It also creates an Addressable Group for each top level folder under AutoBundles. So you'll get a "(Auto) Rocks" group, with every prefab, material or texture as its own, unique Addressable.
* Each Addressable is packed into a unique bundle file.
* When a scene (or any selected assets) need to be loaded, Addressables downloads only EXACTLY what is needed. If your "Rocks" folder has 100 MB of rocks, but you only use one, only 5 MB is downloaded.
* You push all your assets to the CDN, and the app just downloads what it needs at runtime.
* When you change an asset (e.g. change specularity on a material), that material just becomes a new, tiny .bundle file next to the old one. You sync to the same CDN (tiny upload), and release a new version of the app.
* Inspired by Unreal Engine's extremely fine-grained patching system. Client only downloads exactly what it needs. CDN updates are tiny and overlaid.

# Why?

* Multi-file bundles are bad for users, because they result in extreme over-caching especially on mobile.
* By making every texture, prefab, material, or whatever, its own Addressable, downloading and updating is as nimble as possible.
* Manually labelling Addressables is a drag, especially if you have 10,000 assets.
* You already have a folder structre. Why can't Addressables just mirror it?
* AutoBundles is two-click automation to do this.

# How To Use It:

Simply open the AutoBundles **Analyze** Rule:
 
![AutoBundles](https://github.com/perholmes/UnityAutoBundles/raw/master/Images/analyze.png)
 
Then press *Fix Selected Rules*, and the Addressables are updated to mirror the AutoBundles folder structure. Only groups starting with "(Auto)" are synced, and tagging of individual assets is preserved.
Downsides

* It may bundle some things that won't be used by anyone. We could create more filters, or tags to handle this. Or just ignore it, CDN storage is cheap. We only care about end-users not downloading more than they need.
* It's unknown how it scales. Hasn't been tested with tens of thousands of assets.
* It's many more web requests. But with concurrent requests set to just 32, you don't feel any slowdown.
* It's unknown how much overhead is added with individual bundle files. But it's surely less than downloading just one JPG you don't need.

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

# How Addressables Team could be inspired by this

* Best Option: Allow selecting top-level Asset folders where each subfolder will become a group, and every asset in each folder becomes an automatic addressable under those groups (same structure as created by this script, allows people to manage addressables directly from their folders).
* Next-Best Option: Allow mapping a single folder in Assets to a single group, and create and maintain an Addressable for each asset in the folder. Allow creating multiple of these links. Achieves same result as above, but with more work.
* Next-Next-Best Option: Allow folders dropped into Addressables to have an option for "(X) Create Addressables for all Sub-Assets". This says that all the sub-entries are managed, and are created and removed at a pre-build stage in order to stay in sync with the folder the assets came from.

Cheers,

Per Holmes
Hollywood Camera Work
www.hollywoodcamerawork.com


