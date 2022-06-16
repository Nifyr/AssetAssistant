using Newtonsoft.Json;
using SmartPoint.AssetAssistant.UnityExtensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace SmartPoint.AssetAssistant
{
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public class AssetBundleDownloadManifest : IDictionary<string, AssetBundleRecord>, IList<AssetBundleRecord>
    {
        private const int currentVersion = 6;

        [SerializeField]
        [JsonProperty]
        private int _version;

        [SerializeField]
        [JsonProperty]
        private string _projectName;

        [SerializeField]
        [JsonProperty]
        private AssetBundleRecord[] _records;

        [SerializeField]
        [JsonProperty]
        private string[] _assetBundleNamesWithVariant;

        [NonSerialized]
        private Dictionary<string, HashSet<string>> _variantMap;

        [NonSerialized]
        private Dictionary<string, AssetBundleRecord> _recordLookupFromAssetBundleName;

        [NonSerialized]
        private Dictionary<string, AssetBundleRecord> _recordLookupFromAssetPath;

        [NonSerialized]
        private bool _dirty;

        [NonSerialized]
        private string _path;

        [NonSerialized]
        private AssetBundleDownloadManifest _latest;

        public delegate void OnRecordCreated(AssetBundleRecord record);


        /// <summary>
        /// What's the projectName field in this say?
        /// </summary>
        public string projectName
        {
            get => _projectName;
            set => _projectName = value;
        }

        /// <summary>
        /// Unfinished, don't bother.
        /// </summary>
        public string path
        {
            get => _path;
            set => _path = value;
        }

        /// <summary>
        /// Unfinished, don't bother.
        /// </summary>
        public AssetBundleDownloadManifest latest
        {
            get => _latest;
            set => _latest = value;
        }

        /// <summary>
        /// Just how many records are there?
        /// </summary>
        public int recordCount
        {
            get => _records.Length;
        }

        /// <summary>
        /// Just gimme all of the AssetBundleRecords.
        /// </summary>
        public AssetBundleRecord[] records
        {
            get
            {
                _dirty = true;
                return _records;
            }
        }


        /// <summary>
        /// What would I get if I added together all of the records' size fields?
        /// I bet it'd be a lot.
        /// </summary>
        public long totalSize
        {
            get
            {
                long size = 0;
                foreach (var r in _records)
                    size += r.size;
                return size;
            }
        }

        /// <summary>
        /// Unfinished, don't bother.
        /// </summary>
        public long installSize
        {
            get
            {
                long size = 0;
                foreach (var r in _records)
                    if (r.isBeginInstalled) size += r.size;
                return size;
            }
        }

        /// <summary>
        /// Unfinished, don't bother.
        /// </summary>
        public int installCount
        {
            get
            {
                int cnt = 0;
                foreach (var r in _records) cnt += r.isBeginInstalled ? 1 : 0;
                return cnt;
            }
        }

        /// <summary>
        /// Unfinished, don't bother.
        /// </summary>
        public AssetBundleRecord[] installAssetBundleRecords
        {
            get
            {
                int latest = 0;
                foreach (var r in _records)
                {
                    if (r.latest != null) latest++;
                }
                //TODO
                throw new NotImplementedException();
            }
        }

        public ICollection<string> Keys => GetAllAssetBundleNames();

        public ICollection<AssetBundleRecord> Values => records;

        public int Count => recordCount;

        public bool IsReadOnly => false;

        public AssetBundleRecord this[int index] { get => records[index];
            set
            {
                if (_dirty)
                    BuildLookupTables();
                if (index < 0 || index >= _records.Length)
                    throw new ArgumentOutOfRangeException(nameof(index));
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                if (IsExist(value.assetBundleName) && value.assetBundleName != _records[index].assetBundleName)
                    throw new ArgumentException("Adding this record would result in duplicate assetbundle names:\n" +
                        value.assetBundleName);
                if (AssetPathExists(value, _records[index]))
                    throw new ArgumentException("Adding this record would result in duplicate asset paths:\n" +
                        value.assetBundleName);

                _recordLookupFromAssetBundleName.Remove(_records[index].assetBundleName);
                foreach (string s in _records[index].assetPaths)
                    _recordLookupFromAssetPath.Remove(s);
                _records[index] = value;
                _recordLookupFromAssetBundleName[value.assetBundleName] = value;
                foreach (string s in value.assetPaths)
                    _recordLookupFromAssetPath[s] = value;
            }
        }

        public AssetBundleRecord this[string assetBundleName] 
        { 
            get => GetAssetBundleRecord(assetBundleName);
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (value.assetBundleName != assetBundleName)
                    if (assetBundleName != value.assetBundleName)
                        throw new ArgumentException("This record does not have the specified name:\n" +
                            "assetBundleName: " + assetBundleName + "\n" +
                            "record's assetBundleName: " + value.assetBundleName);
                if (IsExist(assetBundleName))
                    this[IndexOf(assetBundleName)] = value;
                else
                    Add(assetBundleName, value);
            }
        }

        private bool AssetPathExists(AssetBundleRecord record, AssetBundleRecord exclude)
        {
            foreach (string s in record.assetPaths)
                if (AssetPathExists(s, exclude))
                    return true;
            return false;
        }

        private bool AssetPathExists(string assetPath, AssetBundleRecord exclude)
        {
            if (_dirty)
                BuildLookupTables();
            return _recordLookupFromAssetPath.ContainsKey(assetPath) && !exclude.assetPaths.Contains(assetPath);
        }

        private int IndexOf(string assetBundleName)
        {
            return GetAllAssetBundleNames().ToList().IndexOf(assetBundleName);
        }

        /// <summary>
        /// Could ya generate me an instance from this here byte array?
        /// </summary>
        public static AssetBundleDownloadManifest Load(byte[] data)
        {
            AssetBundleDownloadManifest ret = (AssetBundleDownloadManifest)new MemoryStream(data).DeserializeBinaryFormatter();
            ret.BuildLookupTables();
            return ret;
        }

        /// <summary>
        /// Could ya generate me an instance from this here file?
        /// </summary>
        public static AssetBundleDownloadManifest Load(string path, bool isSimulation = false)
        {
            AssetBundleDownloadManifest ret = null;
            if (path.IsUrl())
            {
                var web = UnityWebRequest.Get(path);
                web.SendWebRequest();
                while (!web.isNetworkError && !web.isHttpError)
                {
                    if (web.isDone)
                    {
                        ret = Load(web.downloadHandler.data);
                    }
                }
            }
            else
            {
                if (File.Exists(path))
                {
                    ret = (AssetBundleDownloadManifest)new FileStream(path, FileMode.Open).DeserializeBinaryFormatter();
                }
            }
            if (ret._version != 6) ret.Clear();
            if (isSimulation)
            {
                //TODO
                throw new NotImplementedException("Yeah, uh... The simulation part is unfinished...\n" +
                    "Just don't set 'isSimulation' to true for now, okay?");
            }
            ret.BuildLookupTables();
            return ret;
        }

        /// <summary>
        /// Could ya generate me an instance from this here file?
        /// </summary>
        public static AssetBundleDownloadManifest Load(string targetPath, AssetBundleManifest other, OnRecordCreated callback)
        {
            AssetBundleDownloadManifest ret = new AssetBundleDownloadManifest();
            ret.LoadFromAssetBundleManifest(targetPath, other, callback);
            return ret;
        }

        public string[] GetDependencies(string assetBundleName)
        {
            if (!IsExist(assetBundleName))
                throw new KeyNotFoundException("No assetBundle with that name I'm afraid...\n" + assetBundleName);
            return _records.Where(x => x.assetBundleName == assetBundleName).FirstOrDefault().allDependencies;
        }

        /// <summary>
        /// Unfinished, don't bother.
        /// </summary>
        private void LoadFromAssetBundleManifest(string targetPath, AssetBundleManifest other, OnRecordCreated callback)
        {
            _projectName = Path.GetFileNameWithoutExtension(targetPath);
            other.GetAllAssetBundles();
            other.GetAllAssetBundlesWithVariant();

            if (callback != null)
            {
                foreach (var bund in other.GetAllAssetBundles())
                {
                    var hash = other.GetAssetBundleHash(bund);
                    RecordedHash.Parse(hash.ToString());
                }
            }
        }

        /// <summary>
        /// Alright, we're done! Generate me an AssetAssistant-object file!
        /// </summary>
        public void Save(string path)
        {
            new FileStream(path, FileMode.Create).SerializeBinaryFormatter(this);
        }

        /// <summary>
        /// I know it's enticing, but please use AssetBundleDownloadManifest.Load() instead.
        /// </summary>
        public AssetBundleDownloadManifest()
        {
            _version = currentVersion;
            _projectName = string.Empty;
            _assetBundleNamesWithVariant = ArrayHelper.Empty<string>();
            _path = string.Empty;
            _records = ArrayHelper.Empty<AssetBundleRecord>();
            _recordLookupFromAssetBundleName = new Dictionary<string, AssetBundleRecord>();
            _dirty = false;
        }

        /// <summary>
        /// Unfinished, don't bother.
        /// </summary>
        public void Append(string projectName, AssetBundleDownloadManifest appendManifest)
        {
            //TODO
            throw new NotImplementedException();
        }

        private void MarkDifference(AssetBundleDownloadManifest latestManifest)
        {
            //TODO
            throw new NotImplementedException();
        }

        private void BuildLookupTables()
        {
            _recordLookupFromAssetBundleName = new Dictionary<string, AssetBundleRecord>();
            _recordLookupFromAssetPath = new Dictionary<string, AssetBundleRecord>();
            foreach (AssetBundleRecord r in _records)
            {
                _recordLookupFromAssetBundleName[r.assetBundleName] = r;
                foreach (string s in r.assetPaths)
                    _recordLookupFromAssetPath[s] = r;
            }
            _dirty = false;
        }

        /// <summary>
        /// Create me a new entry in the table, please.
        /// (And return it so I can configure it further!)
        /// </summary>
        public AssetBundleRecord AddRecord(string projectName, string assetBundleName)
        {
            AssetBundleRecord record = new AssetBundleRecord();
            record.projectName = projectName;
            record.assetBundleName = assetBundleName;
            record.allDependencies = Array.Empty<string>();
            record.assetPaths = Array.Empty<string>();
            Add(record);
            _dirty = true;
            return record;
        }

        private static void AppendArray<T>(ref T[] a, T t)
        {
            List<T> n = a.ToList();
            n.Add(t);
            a = n.ToArray();
        }

        /// <summary>
        /// Delete everything. (Why?)
        /// </summary>
        public void Clear()
        {
            _records = ArrayHelper.Empty<AssetBundleRecord>();
            _recordLookupFromAssetBundleName?.Clear();
            //_variantMap?.Clear();
            _recordLookupFromAssetPath.Clear();
            _version = 6;
            //MarkDifference(_latest);
        }

        /// <summary>
        /// There doesn't happen to be a assetbundle with this name, does there?
        /// </summary>
        public bool IsExist(string assetBundleName)
        {
            if (_dirty)
                BuildLookupTables();
            return _recordLookupFromAssetBundleName.ContainsKey(assetBundleName);
        }

        /// <summary>
        /// Okay, which ones of these actually exist?
        /// </summary>
        public string[] GetExists(string[] assetBundleNames)
        {
            if (assetBundleNames == null)
                throw new ArgumentNullException(nameof(assetBundleNames));
            List<string> list = new List<string>();
            foreach (string s in assetBundleNames)
                if (IsExist(s))
                    list.Add(s);
            return list.ToArray();
        }

        /// <summary>
        /// Delete these entries, please.
        /// </summary>
        public void RemoveRecords(string[] assetBundleNames)
        {
            if (_dirty)
                BuildLookupTables();
            if (!IsExist(assetBundleNames))
                throw new KeyNotFoundException("An assetbundle name here doesn't exist I'm afraid...\n" +
                    "Might I recommend using GetExists()?");
            List<AssetBundleRecord> list = new List<AssetBundleRecord>();
            foreach (AssetBundleRecord record in _records)
                if (!assetBundleNames.Contains(record.assetBundleName))
                    list.Add(record);
            foreach (string s in assetBundleNames)
            {
                foreach (string assetPath in _recordLookupFromAssetBundleName[s].assetPaths)
                    _recordLookupFromAssetPath.Remove(assetPath);
                _recordLookupFromAssetBundleName.Remove(s);
            }
            _records = list.ToArray();
        }

        private bool IsExist(string[] assetBundleNames)
        {
            foreach (string s in assetBundleNames)
                if (!IsExist(s))
                    return false;
            return true;
        }

        /// <summary>
        /// Unfinished, don't bother.
        /// </summary>
        public void RestrictRecords(string[] assetBundleNames)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// What are all the assetbundles' names?
        /// </summary>
        public string[] GetAllAssetBundleNames()
        {
            return _records.Select(r => r.assetBundleName).ToArray();
        }

        /// <summary>
        /// Unfinished, don't bother.
        /// </summary>
        public string[] GetAssetBundleNamesWithVariant()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unfinished, don't bother.
        /// </summary>
        public string FindMatchAssetBundleNameWithVariants(string assetBundleName, string[] variants)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// What assetbundle is this asset path found in?
        /// </summary>
        public string GetAssetBundleNameAtPath(string path)
        {
            if (_dirty)
                BuildLookupTables();
            AssetBundleRecord rec = null;
            _recordLookupFromAssetPath.TryGetValue(path, out rec);
            if (rec == null)
                throw new KeyNotFoundException("No such asset path I'm afraid...\n" + path);
            string ret = rec.assetBundleName;
            return ret;
        }

        /// <summary>
        /// I'm looking for an entry by this name.
        /// </summary>
        public AssetBundleRecord GetAssetBundleRecord(string assetBundleName)
        {
            if (_dirty)
                BuildLookupTables();
            AssetBundleRecord rec = null;
            _recordLookupFromAssetBundleName.TryGetValue(assetBundleName, out rec);
            if (assetBundleName == null)
                throw new ArgumentNullException(nameof(assetBundleName));
            if (rec == null)
                throw new KeyNotFoundException("No assetBundle with that name I'm afraid...\n" + assetBundleName);
            _dirty = true;
            return rec;
        }

        /// <summary>
        /// Gimme this entry and all of its dependencies' entries.
        /// </summary>
        public AssetBundleRecord[] GetAssetBundleRecordsWithDependencies(string assetBundleName, bool recursive = false)
        {
            if (_dirty)
                BuildLookupTables();
            AssetBundleRecord rec = null;
            List<AssetBundleRecord> recList = new List<AssetBundleRecord>();

            _recordLookupFromAssetBundleName.TryGetValue(assetBundleName, out rec);
            if (rec == null)
                throw new KeyNotFoundException("No assetBundle with that name I'm afraid...\n" + assetBundleName);

            recList.Add(rec);

            foreach (var r in rec.allDependencies)
            {
                if (recursive)
                {
                    recList.AddRange(GetAssetBundleRecordsWithDependencies(r, recursive));
                    continue;
                }
                _recordLookupFromAssetBundleName.TryGetValue(r, out rec);
                recList.Add(rec);
            }

            _dirty = true;
            return recList.ToArray();
        }

        public bool ContainsKey(string assetBundleName)
        {
            return IsExist(assetBundleName);
        }

        public void Add(string assetBundleName, AssetBundleRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (assetBundleName != record.assetBundleName)
                throw new ArgumentException("This record does not have the specified name:\n" +
                    "assetBundleName: " + assetBundleName + "\n" +
                    "record's assetBundleName: " + record.assetBundleName);
            Add(record);
        }

        private bool AssetPathExists(AssetBundleRecord record)
        {
            foreach (string s in record.assetPaths)
                if (AssetPathExists(s))
                    return true;
            return false;
        }

        private bool AssetPathExists(string assetPath)
        {
            if (_dirty)
                BuildLookupTables();
            return _recordLookupFromAssetPath.ContainsKey(assetPath);
        }

        public bool Remove(string assetBundleName)
        {
            if(!IsExist(assetBundleName))
                return false;
            RemoveRecords(new string[] { assetBundleName });
            return true;
        }

        public bool TryGetValue(string assetBundleName, out AssetBundleRecord record)
        {
            if (_dirty)
                BuildLookupTables();
            return ((IDictionary<string, AssetBundleRecord>)_recordLookupFromAssetBundleName).TryGetValue(assetBundleName, out record);
        }

        public void Add(KeyValuePair<string, AssetBundleRecord> item)
        {
            Add(item.Key, item.Value);
        }

        public bool Contains(KeyValuePair<string, AssetBundleRecord> item)
        {
            return IsExist(item.Key);
        }

        public void CopyTo(KeyValuePair<string, AssetBundleRecord>[] array, int arrayIndex)
        {
            if (_dirty)
                BuildLookupTables();
            ((ICollection<KeyValuePair<string, AssetBundleRecord>>)_recordLookupFromAssetBundleName).CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<string, AssetBundleRecord> item)
        {
            return Remove(item.Key);
        }

        public IEnumerator<KeyValuePair<string, AssetBundleRecord>> GetEnumerator()
        {
            if (_dirty)
                BuildLookupTables();
            return ((IEnumerable<KeyValuePair<string, AssetBundleRecord>>)_recordLookupFromAssetBundleName).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _records.GetEnumerator();
        }

        public int IndexOf(AssetBundleRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            return IndexOf(record.assetBundleName);
        }

        public void Insert(int index, AssetBundleRecord record)
        {
            if (_dirty)
                BuildLookupTables();
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (index < 0 || index >= _records.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (IsExist(record.assetBundleName))
                throw new ArgumentException("There's already an entry with the same name...\n" + record.assetBundleName);
            if (AssetPathExists(record))
                throw new ArgumentException("Adding this record would result in duplicate asset paths:\n" +
                    record.assetBundleName);

            List<AssetBundleRecord> n = _records.ToList();
            n.Insert(index, record);
            _records = n.ToArray();
            _recordLookupFromAssetBundleName[record.assetBundleName] = record;
            foreach (string s in record.assetPaths)
                _recordLookupFromAssetPath[s] = record;
        }

        public void RemoveAt(int index)
        {
            if (_dirty)
                BuildLookupTables();
            if (index < 0 || index >= _records.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            _recordLookupFromAssetBundleName.Remove(_records[index].assetBundleName);
            foreach (string s in _records[index].assetPaths)
                _recordLookupFromAssetPath.Remove(s);
            List<AssetBundleRecord> n = _records.ToList();
            n.RemoveAt(index);
            _records = n.ToArray();
        }

        public void Add(AssetBundleRecord record)
        {
            if (_dirty)
                BuildLookupTables();
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (IsExist(record.assetBundleName))
                throw new ArgumentException("There's already a record with this name:\n" + record.assetBundleName);
            if (AssetPathExists(record))
                throw new ArgumentException("This record shares an asset path with another record:\n" + record.assetBundleName);
            AppendArray(ref _records, record);
            _recordLookupFromAssetBundleName[record.assetBundleName] = record;
            foreach (string s in record.assetPaths)
                _recordLookupFromAssetPath[s] = record;
        }

        public bool Contains(AssetBundleRecord record)
        {
            return IsExist(record.assetBundleName);
        }

        public void CopyTo(AssetBundleRecord[] array, int arrayIndex)
        {
            _records.CopyTo(array, arrayIndex);
        }

        public bool Remove(AssetBundleRecord record)
        {
            return Remove(record.assetBundleName);
        }

        IEnumerator<AssetBundleRecord> IEnumerable<AssetBundleRecord>.GetEnumerator()
        {
            return ((IEnumerable<AssetBundleRecord>)_records).GetEnumerator();
        }
    }
}
