using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct BundleListInfo
{
    public string bundleName;
    public long bundleTotalHashCode;
}

[Serializable]
public class BundleFileInfo
{
    public string filePath;
    public string fileName;
}

[Serializable]
public class BundleListsDic
{
    public List<BundleListInfo> bundleNameLists = new List<BundleListInfo>();
    public Dictionary<string, List<BundleFileInfo>> bundleObjectLists = new Dictionary<string, List<BundleFileInfo>>();

    public void AddBundleName(BundleListInfo info)
    {
        bundleNameLists.Add(info);
    }

    public void AddObjectsRejeon(string bundleName, List<BundleFileInfo> files)
    {
        bundleObjectLists[bundleName] = files;
    }
}

[Serializable]
public class AtlasInfo
{
    public string atlasName;
    public List<string> spriteLists = new List<string>();
}

[Serializable]
public class AtlasLists
{
    public List<AtlasInfo> atlaseLists = new List<AtlasInfo>();
}
