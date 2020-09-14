using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    class AutoBundles : AnalyzeRule
    {
        public override bool CanFix { get { return true; } }
        public override string ruleName { get { return "AutoBundles"; } }

        string templateToUse = "Packed Assets"; // Group settings template to use for newly created groups.
        string autoGroupPrefix = "(Auto) "; // Created groups will have this prefix. Do not change after starting to use it.
        string autoBundlesFolderName = "AutoBundles"; // Name of the folder that will be scanned.
        string assetFilter = "t:AnimationClip t:AudioClip t:AudioMixer t:ComputeShader t:Font t:GUISkin t:Material t:Mesh t:Model t:PhysicMaterial t:Prefab t:Scene t:Shader t:Sprite t:Texture t:VideoClip";
        string[] ignoreExtensions = {".fbx", ".psd"};
        string[] alwaysIncludeExtensions = {".unity"};
        string forceLabel = "ForceAddressable"; // Assets with this label are always included no matter what.

        public struct AssetAction {
            public bool create;     // True = create, false = remove addressable asset
            public string inGroup;  // Group name with (Auto) prefix.
            public string addressablePath;
            public string assetGuid;
        }

        List<string> groupsToCreate = new List<string>();
        List<string> groupsToRemove = new List<string>();
        List<AssetAction> assetActions = new List<AssetAction>();

        void ClearOurData()
        {
            groupsToCreate.Clear();
            groupsToRemove.Clear();
            assetActions.Clear();
        }

        public override List<AnalyzeResult> RefreshAnalysis(AddressableAssetSettings settings)
        {
            List<AnalyzeResult> results = new List<AnalyzeResult>();
            ClearAnalysis();
            ClearOurData();

            var projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets/".Length);

            if (!BuildUtility.CheckModifiedScenesAndAskToSave())
            {
                Debug.LogError("Cannot run Analyze with unsaved scenes");
                results.Add(new AnalyzeResult { resultName = ruleName + "Cannot run Analyze with unsaved scenes" });
                return results;
            }

            // Get all immediate folders in Assets/AutoBundles

            HashSet<string> folderNames = new HashSet<string>();
            var folders = AssetDatabase.GetSubFolders("Assets/" + autoBundlesFolderName);
            foreach (var folder in folders) {
                folderNames.Add(Path.GetFileName(folder));
            }

            // Get all addressable groups carrying the (Auto) prefix

            HashSet<string> autoGroups = new HashSet<string>();
            foreach (var group in settings.groups) {
                if (group.name.StartsWith(autoGroupPrefix)) {
                    autoGroups.Add(group.name);
                }
            }

            // Collect all groups that must be created or moved

            foreach (var folder in folderNames) {
                var autoName = autoGroupPrefix + folder;
                if (!autoGroups.Contains(autoName)) {
                    groupsToCreate.Add(autoName);
                    results.Add(new AnalyzeResult(){resultName = "Create group \"" + autoName + "\""});
                }
            }

            // Collect all groups that must be removed

            foreach (var groupName in autoGroups) {
                var baseName = groupName.Substring(autoGroupPrefix.Length);
                if (!folderNames.Contains(baseName)) {
                    groupsToRemove.Add(groupName);
                    results.Add(new AnalyzeResult(){resultName = "Remove group \"" + groupName + "\""});
                }
            }

            // Get all assets

            var allGuids = AssetDatabase.FindAssets(assetFilter, new [] {"Assets/" + autoBundlesFolderName});
            var neverBundle = new HashSet<string>();

            // Only include assets that pass basic filtering, like file extension.
            // Result is "assetPaths", the authoritative list of assets we're considering bundling.

            var assetPaths = new HashSet<string>();

            foreach (var guid in allGuids) {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (ShouldIgnoreAsset(path)) {
                    neverBundle.Add(path.ToLower());
                } else {
                    assetPaths.Add(path);
                }
            }

            // Collect all parents of all assets in preparation for not bundling assets with one ultimate non-scene parent.

            var parents = new Dictionary<string, HashSet<string>>(); // Map from asset guid to all its parents

            foreach (var path in assetPaths) {
                var dependencies = AssetDatabase.GetDependencies(path);

                foreach (var asset in dependencies) {
                    if (asset == path) {
                        // Ignore self
                        continue;
                    }

                    if (ShouldIgnoreAsset(asset)) {
                        continue;
                    }

                    if (!parents.ContainsKey(asset)) {
                        parents.Add(asset, new HashSet<string>());
                    }
                    parents[asset].Add(path);
                }
            }

            // Unbundle assets with zero parents

            foreach (var asset in assetPaths) {
                if (!parents.ContainsKey(asset)) {
                    neverBundle.Add(asset.ToLower());
                }
            }

            Debug.Log(neverBundle.Count + " asset have zero parents and won't be bundled");
            int floor = neverBundle.Count;

            // Unbundle assets with one parent

            foreach (var asset in assetPaths) {
                if (parents.ContainsKey(asset) && parents[asset].Count == 1) {
                    neverBundle.Add(asset.ToLower());
                }
            }

            Debug.Log((neverBundle.Count - floor) + " asset have one parent and won't be bundled");
            floor = neverBundle.Count;

            // Unbundle assets with one ultimate parent

            var ultimateParents = new Dictionary<string, HashSet<string>>();

            foreach (var asset in assetPaths) {
                if (neverBundle.Contains(asset.ToLower())) {
                    continue;
                }

                ultimateParents[asset] = new HashSet<string>();

                // Iterate all the way to the top for this asset. Assemble a list of all ultimate parents of this asset.

                var parentsToCheck = new List<string>();
                parentsToCheck.AddRange(parents[asset].ToList());

                while (parentsToCheck.Count != 0) {
                    var checking = parentsToCheck[0];
                    parentsToCheck.RemoveAt(0);

                    if (!parents.ContainsKey(checking)) {
                        // If asset we're checking doesn't itself have any parents, this is the end.
                        ultimateParents[asset].Add(checking);
                    } else {
                        parentsToCheck.AddRange(parents[checking]);
                    }
                }
            }
            
            // Unbundle all assets that don't have two or more required objects as ultimate parents.
            // Objects with one included parent will still get included if needed, just not as a separate Addressable.
            
            foreach (KeyValuePair<string, HashSet<string>> pair in ultimateParents) {
                int requiredParents = 0;
                foreach (var ultiParent in pair.Value) {
                    if (AlwaysIncludeAsset(ultiParent)) {
                        requiredParents++;
                    }
                }

                if (requiredParents <= 1) {
                    neverBundle.Add(pair.Key.ToLower());
                }
            }

            Debug.Log((neverBundle.Count - floor) + " asset have zero or one ultimate parents and won't be bundled");
            floor = neverBundle.Count;

            // Skip assets that are too small. This is a tradeoff between individual access to files,
            // versus the game not having an open file handle for every single 2 KB thing. We're choosing
            // to duplicate some things by baking them into multiple bundles, even though it requires
            // more storage and bandwidth.

            int tooSmallCount = 0;

            foreach (var asset in assetPaths) {
                if (neverBundle.Contains(asset.ToLower())) {
                    continue;
                }
                var diskPath = projectRoot + "/" + asset;
                var fileInfo = new System.IO.FileInfo(diskPath);
                if (fileInfo.Length < 10000) {
                    tooSmallCount++;
                    neverBundle.Add(asset.ToLower());
                }
            }

            Debug.Log(tooSmallCount + " assets are too small and won't be bundled");

            // Collect all assets to create as addressables

            string preamble = "Assets/" + autoBundlesFolderName + "/";
            var expresslyBundled = new HashSet<string>();

            foreach (var folder in folderNames) {
                var assetGuids = AssetDatabase.FindAssets(assetFilter, new [] {"Assets/" + autoBundlesFolderName + "/" + folder});

                // Schedule creation/moving of assets that exist

                foreach (var guid in assetGuids) {
                    var addrPath = AssetDatabase.GUIDToAssetPath(guid);
                    
                    // Skip assets we're never bundling

                    string lowerPath = addrPath.ToLower();
                    if (neverBundle.Contains(lowerPath) && !AlwaysIncludeAsset(lowerPath)) {
                        continue;
                    }

                    // Remove the Assets/AutoBundles/ part of assets paths.

                    var shortPath = addrPath;

                    if (shortPath.StartsWith(preamble)) {
                        shortPath = shortPath.Substring(preamble.Length);
                    }

                    // Create asset creation/moving action.
                    
                    string autoGroup = autoGroupPrefix + folder;

                    assetActions.Add(new AssetAction() {
                        create = true,
                        inGroup = autoGroup,
                        assetGuid = guid,
                        addressablePath = shortPath
                    });

                    AddressableAssetEntry entry = settings.FindAssetEntry(guid);
                    if (entry == null) {
                        results.Add(new AnalyzeResult(){resultName = "Add:" + shortPath});
                    } else {
                        results.Add(new AnalyzeResult(){resultName = "Keep or move:" + shortPath}); 
                    }

                    expresslyBundled.Add(shortPath);
                }
            }

            // Schedule removal of assets in auto folders that exist as addressables but aren't expressly bundled.

            foreach (var folder in folderNames) {
                string autoName = autoGroupPrefix + folder;
                var group = settings.FindGroup(autoName);

                if (group != null) {
                    List<AddressableAssetEntry> result = new List<AddressableAssetEntry>();
                    group.GatherAllAssets(result, true, false, true);

                    foreach (var entry in result) {
                        if (entry.IsSubAsset) {
                            continue;
                        }
                        if (entry.guid == "") {
                            Debug.Log("Entry has no guid! " + entry.address);
                        }

                        if (!expresslyBundled.Contains(entry.address)) {
                            assetActions.Add(new AssetAction() {
                                create = false,
                                inGroup = autoName,
                                assetGuid = entry.guid,
                                addressablePath = entry.address,
                            });

                            // Print removal message without preamble

                            results.Add(new AnalyzeResult(){resultName = "Remove:" + entry.address});
                        }
                    }
                }
            }

            return results;
        }

        public bool ShouldIgnoreAsset(string path)
        {
            foreach (var ext in alwaysIncludeExtensions) {
                if (path.ToLower().EndsWith(ext)) {
                    return false;
                }
            }

            foreach (var ext in ignoreExtensions) {
                if (path.ToLower().EndsWith(ext)) {
                    return true;
                }
            }
            return false;
        }

        public bool AlwaysIncludeAsset(string path)
        {
            foreach (var ext in alwaysIncludeExtensions) {
                if (path.ToLower().EndsWith(ext)) {
                    return true;
                }
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            var labels = AssetDatabase.GetLabels(asset);
            if (labels.Contains(forceLabel)) {
                return true;
            }

            return false;
        }

        public override void FixIssues(AddressableAssetSettings settings)
        {
            // Load template used for creating new groups

            var groupTemplates = settings.GroupTemplateObjects;
            AddressableAssetGroupTemplate foundTemplate = null;

            foreach (var template in groupTemplates) {
                if (template.name == templateToUse) {
                    foundTemplate = template as AddressableAssetGroupTemplate;
                    break;
                }
            }

            if (foundTemplate == null) {
                Debug.Log("Group template \"" + templateToUse + "\" not found. Aborting!");
                return;
            }

            // Create groups

            foreach (var groupName in groupsToCreate) {
                // I don't know enough about schemas, so schemasToCopy is set to null here.
                AddressableAssetGroup newGroup = settings.CreateGroup(groupName, false, false, true, null, foundTemplate.GetTypes());
                foundTemplate.ApplyToAddressableAssetGroup(newGroup);
            }

            // Remove groups

            foreach (var groupName in groupsToRemove) {
                foreach (var group in settings.groups) {
                    if (group.name == groupName) {
                        settings.RemoveGroup(group);
                        break;
                    }
                }
            }

            // Collect current group names

            Dictionary<string, AddressableAssetGroup> groups = new Dictionary<string, AddressableAssetGroup>();
            foreach (var group in settings.groups) {
                groups.Add(group.name, group);
            }

            // Create and remove assets

            foreach (var action in assetActions) {
                if (!groups.ContainsKey(action.inGroup)) {
                    continue;
                }

                if (action.create) {
                    AddressableAssetEntry entry = settings.CreateOrMoveEntry(action.assetGuid, groups[action.inGroup]);
                    entry.SetAddress(action.addressablePath);
                } else {
                    AddressableAssetEntry entry = settings.FindAssetEntry(action.assetGuid);
                    if (entry != null) {
                        settings.RemoveAssetEntry(action.assetGuid);
                    } else {
                        Debug.Log("Asset guid didn't produce an entry: " + action.assetGuid);
                    }
                }
            }

            ClearAnalysis();
            ClearOurData();
        }

        [InitializeOnLoad]
        class RegisterBuildBundleLayout
        {
            static RegisterBuildBundleLayout()
            {
                AnalyzeSystem.RegisterNewRule<AutoBundles>();
            }
        }
    }
}
