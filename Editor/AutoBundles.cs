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
        bool neverBundleNoReferences = true; // Never bundle assets that aren't referenced from anywhere in any Assets folder.

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

            if (!BuildUtility.CheckModifiedScenesAndAskToSave())
            {
                Debug.LogError("Cannot run Analyze with unsaved scenes");
                results.Add(new AnalyzeResult { resultName = ruleName + "Cannot run Analyze with unsaved scenes" });
                return results;
            }

            // Get all folders in Assets/AutoBundles

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

            // Collect all assets that have zero or one references. They will not be bundled, because
            // Addressables will automatically bring them in. This reduces the number of individual bundles.

            Dictionary<string, int> refCounts = new Dictionary<string, int>();
            var guids = AssetDatabase.FindAssets(assetFilter, new [] {"Assets"});
            
            foreach (var guid in guids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                var dependencies = AssetDatabase.GetDependencies(path);
                foreach (var asset in dependencies) {
                    if (asset == path) {
                        // Ignore self
                        continue;
                    }
                    if (refCounts.ContainsKey(asset)) {
                        refCounts[asset]++;
                    } else {
                        refCounts[asset] = 1;
                    }
                }
            }

            // Select which items to never bundle. This includes items that have no references or only one references,
            // as well as unwanted file extensions.

            HashSet<string> neverBundle = new HashSet<string>();

            foreach (KeyValuePair<string, int> asset in refCounts) {
                if (asset.Value == 0 && neverBundleNoReferences || asset.Value == 1) {
                    neverBundle.Add(asset.Key);
                }

                bool ignore = false;
                foreach (var ext in ignoreExtensions) {
                    if (asset.Key.ToLower().EndsWith(ext)) {
                        ignore = true;
                        break;
                    }
                }
                if (ignore) {
                    neverBundle.Add(asset.Key);
                }
            }
            
            // Collect all assets to create as addressables

            string preamble = "Assets/" + autoBundlesFolderName + "/";

            foreach (var folder in folderNames) {
                var assetGuids = AssetDatabase.FindAssets(assetFilter, new [] {"Assets/" + autoBundlesFolderName + "/" + folder});

                // Schedule creation/moving of assets that exist

                foreach (var guid in assetGuids) {
                    var addrPath = AssetDatabase.GUIDToAssetPath(guid);
                    
                    // Skip assets we're never bundling

                    if (neverBundle.Contains(addrPath)) {
                        continue;
                    }

                    // Remove the Assets/AutoBundles/ part of assets paths.

                    if (addrPath.StartsWith(preamble)) {
                        addrPath = addrPath.Substring(preamble.Length);
                    }

                    // Create asset creation/moving action.
                    
                    string autoGroup = autoGroupPrefix + folder;

                    assetActions.Add(new AssetAction() {
                        create = true,
                        inGroup = autoGroup,
                        assetGuid = guid,
                        addressablePath = addrPath
                    });

                    AddressableAssetEntry entry = settings.FindAssetEntry(guid);
                    if (entry == null) {
                        results.Add(new AnalyzeResult(){resultName = "Add:" + addrPath});
                    } else {
                        results.Add(new AnalyzeResult(){resultName = "Keep or move:" + addrPath}); 
                    }
                }

                // Schedule removal of assets that exist as addressables but don't exist anywhere under the AutoBundles tree

                string autoName = autoGroupPrefix + folder;
                var group = settings.FindGroup(autoName);

                if (group != null) {
                    List<AddressableAssetEntry> result = new List<AddressableAssetEntry>();
                    group.GatherAllAssets(result, true, true, true);

                    foreach (var entry in result) {
                        if (entry.IsSubAsset) {
                            continue;
                        }
                        if (entry.guid == "") {
                            Debug.Log("Entry has no guid! " + entry.address);
                        }

                        string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);


                        if (!assetPath.StartsWith("Assets/" + autoBundlesFolderName + "/")) {
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

        public override void FixIssues(AddressableAssetSettings settings)
        {
            // Load template used for creating new groups

            var groupTemplates = settings.GroupTemplateObjects;
            AddressableAssetGroupTemplate foundTemplate = null;

            foreach (var template in groupTemplates) {
                if (template.name == templateToUse) {
                    foundTemplate = template as AddressableAssetGroupTemplate;
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
