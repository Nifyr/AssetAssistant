using SmartPoint.AssetAssistant.UnityExtensions;
using System;

namespace SmartPoint.AssetAssistant
{
    [Serializable]
    public class AssetBundleRecord : ICloneable, IEquatable<AssetBundleRecord>
    {
        public string projectName;
        public string assetBundleName;
        public RecordedHash hash;
        public DateTime lastWriteTime;
        public bool isStreamingSceneAssetBundle;
        public string[] allDependencies;
        public string[] assetPaths;
        public long size;
        [NonSerialized]
        public AssetBundleRecord latest;
        [NonSerialized]
        public bool isBeginInstalled;
        [NonSerialized]
        public bool isSimulation;

        public AssetBundleRecord() { }

        public AssetBundleRecord(string _projectName, string _assetBundleName)
        {
            projectName = _projectName;
            assetBundleName = _assetBundleName;
            allDependencies = ArrayHelper.Empty<string>();
            assetPaths = ArrayHelper.Empty<string>();
            var prox = Sequencer.editorProxy;
            //TODO
        }

        public AssetBundleRecord(AssetBundleRecord other)
        {
            projectName = other.projectName;
            assetBundleName = other.assetBundleName;
            hash = other.hash;
            lastWriteTime = other.lastWriteTime;
            allDependencies = (string[])other.allDependencies.Clone();
            assetPaths = (string[])other.assetPaths.Clone();
            size = other.size;
            isStreamingSceneAssetBundle = other.isStreamingSceneAssetBundle;
            isSimulation = other.isSimulation;
        }

        public object Clone()
        {
            return new AssetBundleRecord(this);
        }

        public bool Equals(AssetBundleRecord other)
        {
            return assetBundleName == other.assetBundleName;
        }
    }
}
