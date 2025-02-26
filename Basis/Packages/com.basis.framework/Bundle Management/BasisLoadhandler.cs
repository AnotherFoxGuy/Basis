using Basis.Scripts.Avatar;
using BasisSerializer.OdinSerializer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BasisLoadHandler
{
    public static Dictionary<string, BasisTrackedBundleWrapper> LoadedBundles = new Dictionary<string, BasisTrackedBundleWrapper>();
    public static ConcurrentDictionary<string, OnDiscInformation> OnDiscData = new ConcurrentDictionary<string, OnDiscInformation>();
    public static bool IsInitialized = false;

    private static readonly object _discInfoLock = new object();
    private static SemaphoreSlim _initSemaphore = new SemaphoreSlim(1, 1);
    public static int TimeUntilMemoryRemoval = 30;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static async Task OnGameStart()
    {
        BasisDebug.Log("Game has started after scene load.", BasisDebug.LogTag.Event);
        await EnsureInitializationComplete();
        SceneManager.sceneUnloaded += sceneUnloaded;
    }

    private static async void sceneUnloaded(Scene UnloadedScene)
    {
        foreach (KeyValuePair<string, BasisTrackedBundleWrapper> kvp in LoadedBundles)
        {
            if (kvp.Value != null)
            {
                if (kvp.Value.MetaLink == UnloadedScene.path)
                {
                    kvp.Value.DeIncrement();
                    bool State = await kvp.Value.UnloadIfReady();
                    if (State)
                    {
                        LoadedBundles.Remove(kvp.Key);
                        return;
                    }
                }
            }
        }
    }
    /// <summary>
    /// this will take 30 seconds to execute
    /// the gameobject will be nuked right away
    /// after that we wait for 30 seconds to see if we can also remove the bundle!
    /// </summary>
    /// <param name="Destroy"></param>
    /// <param name="LoadedKey"></param>
    /// <param name="DestroyImmediately"></param>
    /// <returns></returns>
    public static async Task DestroyGameobject(GameObject Destroy, string LoadedKey, bool DestroyImmediately = false)
    {
        if (DestroyImmediately)
        {
            GameObject.DestroyImmediate(Destroy);
        }
        else
        {
            GameObject.Destroy(Destroy);
        }
        if (LoadedBundles.TryGetValue(LoadedKey, out BasisTrackedBundleWrapper Wrapper))
        {
            Wrapper.DeIncrement();
            bool State = await Wrapper.UnloadIfReady();
            if (State)
            {
                LoadedBundles.Remove(LoadedKey);
                return;
            }
        }
        else
        {
            if (LoadedKey.ToLower() != BasisAvatarFactory.LoadingAvatar.BasisRemoteBundleEncrypted.CombinedURL.ToLower())
            {
                BasisDebug.LogError($"tried to find Loaded Key {LoadedKey} but could not find it!");
            }
        }
    }
    public static async Task<GameObject> LoadGameObjectBundle(BasisLoadableBundle loadableBundle, bool useContentRemoval, BasisProgressReport report, CancellationToken cancellationToken, Vector3 Position, Quaternion Rotation,Vector3 Scale,bool ModifyScale, Transform Parent = null)
    {
        await EnsureInitializationComplete();

        if (LoadedBundles.TryGetValue(loadableBundle.BasisRemoteBundleEncrypted.CombinedURL, out BasisTrackedBundleWrapper wrapper))
        {
            try
            {
                await wrapper.WaitForBundleLoadAsync();
                return await BasisBundleLoadAsset.LoadFromWrapper(wrapper, useContentRemoval, Position, Rotation, ModifyScale, Scale, Parent);
            }
            catch (Exception ex)
            {
                BasisDebug.LogError($"Failed to load content: {ex}");
                LoadedBundles.Remove(loadableBundle.BasisRemoteBundleEncrypted.CombinedURL);
                return null;
            }
        }

        return await HandleFirstBundleLoad(loadableBundle, useContentRemoval, report, cancellationToken, Position, Rotation, Scale, ModifyScale, Parent);
    }

    public static async Task<Scene> LoadSceneBundle(bool makeActiveScene, BasisLoadableBundle loadableBundle, BasisProgressReport report, CancellationToken cancellationToken)
    {
        await EnsureInitializationComplete();

        if (LoadedBundles.TryGetValue(loadableBundle.BasisRemoteBundleEncrypted.CombinedURL, out BasisTrackedBundleWrapper wrapper))
        {
            BasisDebug.Log($"Bundle On Disc Loading", BasisDebug.LogTag.Networking);
            await wrapper.WaitForBundleLoadAsync();
            BasisDebug.Log($"Bundle Loaded, Loading Scene", BasisDebug.LogTag.Networking);
            return await BasisBundleLoadAsset.LoadSceneFromBundleAsync(wrapper, makeActiveScene, report);
        }

       return await HandleFirstSceneLoad(loadableBundle, makeActiveScene, report, cancellationToken);
    }

    private static async Task<Scene> HandleFirstSceneLoad(BasisLoadableBundle loadableBundle, bool makeActiveScene, BasisProgressReport report, CancellationToken cancellationToken)
    {
        BasisTrackedBundleWrapper wrapper = new BasisTrackedBundleWrapper { AssetBundle = null, LoadableBundle = loadableBundle };

        if (!LoadedBundles.TryAdd(loadableBundle.BasisRemoteBundleEncrypted.CombinedURL, wrapper))
        {
            BasisDebug.LogError("Unable to add bundle wrapper.");
            return new Scene();
        }

        await HandleBundleAndMetaLoading(wrapper, report, cancellationToken);
        return await BasisBundleLoadAsset.LoadSceneFromBundleAsync(wrapper, makeActiveScene, report);
    }

    private static async Task<GameObject> HandleFirstBundleLoad(BasisLoadableBundle loadableBundle, bool useContentRemoval, BasisProgressReport report, CancellationToken cancellationToken, Vector3 Position, Quaternion Rotation, Vector3 Scale,bool ModifyScale, Transform Parent = null)
    {
        BasisTrackedBundleWrapper wrapper = new BasisTrackedBundleWrapper
        {
            AssetBundle = null,
            LoadableBundle = loadableBundle
        };

        if (!LoadedBundles.TryAdd(loadableBundle.BasisRemoteBundleEncrypted.CombinedURL, wrapper))
        {
            BasisDebug.LogError("Unable to add bundle wrapper.");
            return null;
        }

        try
        {
            await HandleBundleAndMetaLoading(wrapper, report, cancellationToken);
            return await BasisBundleLoadAsset.LoadFromWrapper(wrapper, useContentRemoval, Position, Rotation, ModifyScale, Scale, Parent);
        }
        catch (Exception ex)
        {
            BasisDebug.LogError(ex);
            LoadedBundles.Remove(loadableBundle.BasisRemoteBundleEncrypted.CombinedURL);
            CleanupFiles(loadableBundle.BasisLocalEncryptedBundle);
            OnDiscData.TryRemove(loadableBundle.BasisRemoteBundleEncrypted.CombinedURL, out _);
            return null;
        }
    }

    public static async Task HandleBundleAndMetaLoading(BasisTrackedBundleWrapper wrapper, BasisProgressReport report, CancellationToken cancellationToken)
    {
        bool IsMetaOnDisc = IsMetaDataOnDisc(wrapper.LoadableBundle.BasisRemoteBundleEncrypted.CombinedURL, out OnDiscInformation MetaInfo);
        /*here LD
        if(MetaInfo.StoredLocal.LocalConnectorPath)
        {

        }
        */
        // bool IsBundleOnDisc = IsBundleDataOnDisc(wrapper.LoadableBundle.BasisRemoteBundleEncrypted.BundleURL, out OnDiscInformation BundleInfo);
        bool IsBundleOnDisc = false;
        if (IsMetaOnDisc)
        {
            BasisDebug.Log("ProcessOnDiscMetaDataAsync", BasisDebug.LogTag.Event);
            await BasisBundleManagement.ProcessOnDiscMetaDataAsync(wrapper, MetaInfo.StoredLocal, report, cancellationToken);
        }
        else
        {
            BasisDebug.Log("Meta was no on disc downloading on next stage", BasisDebug.LogTag.Event);
        }
        if (IsBundleOnDisc == false)
        {
            BasisDebug.Log("DownloadStoreMetaAndBundle", BasisDebug.LogTag.Event);
            await BasisBundleManagement.DownloadStoreMetaAndBundle(wrapper, report, cancellationToken);
        }
        else
        {
            BasisDebug.Log("Bundle was already on disc proceeding", BasisDebug.LogTag.Event);
        }
        if (wrapper.LoadableBundle.BasisBundleConnector.GetPlatform(out BasisBundleGenerated Generated))
        {
            IEnumerable<AssetBundle> AssetBundles = AssetBundle.GetAllLoadedAssetBundles();
            foreach (AssetBundle assetBundle in AssetBundles)
            {
                if (assetBundle != null && assetBundle.Contains(Generated.AssetToLoadName))
                {
                    wrapper.AssetBundle = assetBundle;
                    BasisDebug.Log("we already have this AssetToLoadName in our loaded bundles using that instead!");
                    if (IsMetaOnDisc == false || IsBundleOnDisc == false)
                    {
                        OnDiscInformation newDiscInfo = new OnDiscInformation
                        {
                            StoredRemote = wrapper.LoadableBundle.BasisRemoteBundleEncrypted,
                            StoredLocal = wrapper.LoadableBundle.BasisLocalEncryptedBundle,
                            AssetIDToLoad = Generated.AssetToLoadName,
                        };

                        await AddDiscInfo(newDiscInfo);
                    }
                    return;
                }
            }

            AssetBundleCreateRequest bundleRequest = await BasisEncryptionToData.GenerateBundleFromFile(
                wrapper.LoadableBundle.UnlockPassword,
                wrapper.LoadableBundle.BasisLocalEncryptedBundle.LocalConnectorPath,
                Generated.AssetBundleCRC,
                report
            );

            wrapper.AssetBundle = bundleRequest.assetBundle;

            if (IsMetaOnDisc == false || IsBundleOnDisc == false)
            {
                OnDiscInformation newDiscInfo = new OnDiscInformation
                {
                    StoredRemote = wrapper.LoadableBundle.BasisRemoteBundleEncrypted,
                    StoredLocal = wrapper.LoadableBundle.BasisLocalEncryptedBundle,
                    AssetIDToLoad = Generated.AssetToLoadName,
                };

                await AddDiscInfo(newDiscInfo);
            }
        }
        else
        {
           BasisDebug.LogError("Missing Bundle Request Platform for " + Application.platform);
        }
    }
    public static async Task HandleMetaLoading(BasisTrackedBundleWrapper wrapper, BasisProgressReport report, CancellationToken cancellationToken)
    {
        bool isOnDisc = IsMetaDataOnDisc(wrapper.LoadableBundle.BasisRemoteBundleEncrypted.CombinedURL, out OnDiscInformation discInfo);

        if (isOnDisc)
        {
            await BasisBundleManagement.ProcessOnDiscMetaDataAsync(wrapper, discInfo.StoredLocal, report, cancellationToken);
        }
        else
        {
            await BasisBundleManagement.DownloadAndSaveMetaFile(wrapper, report, cancellationToken);//just save the meta data
        }

        if (!isOnDisc)
        {
            if (wrapper.LoadableBundle.BasisBundleConnector.GetPlatform(out BasisBundleGenerated Generated))
            {
                OnDiscInformation newDiscInfo = new OnDiscInformation
                {
                    StoredRemote = wrapper.LoadableBundle.BasisRemoteBundleEncrypted,
                    StoredLocal = wrapper.LoadableBundle.BasisLocalEncryptedBundle,
                    AssetIDToLoad = Generated.AssetToLoadName,
                };
                await AddDiscInfo(newDiscInfo);
            }
            else
            {
                BasisDebug.LogError("Missing Bundle Request Platform for " + Application.platform);
            }
        }
    }
    public static bool IsMetaDataOnDisc(string MetaURL, out OnDiscInformation info)
    {
        lock (_discInfoLock)
        {
            foreach (var discInfo in OnDiscData.Values)
            {
                if (discInfo.StoredRemote.CombinedURL == MetaURL)
                {
                    info = discInfo;
                    if (File.Exists(discInfo.StoredLocal.LocalConnectorPath))
                    {
                        return true;
                    }
                }
            }

            info = new OnDiscInformation();
            return false;
        }
    }
    public static bool IsBundleDataOnDisc(string BundleURL, out OnDiscInformation info)
    {
        lock (_discInfoLock)
        {
            foreach (var discInfo in OnDiscData.Values)
            {
                if (discInfo.StoredRemote.CombinedURL == BundleURL)
                {
                    info = discInfo;
                    if (File.Exists(discInfo.StoredLocal.LocalConnectorPath))
                    {
                        return true;
                    }
                }
            }

            info = new OnDiscInformation();
            return false;
        }
    }

    public static async Task AddDiscInfo(OnDiscInformation discInfo)
    {
        if (OnDiscData.TryAdd(discInfo.StoredRemote.CombinedURL, discInfo))
        {
        }
        else
        {
            OnDiscData[discInfo.StoredRemote.CombinedURL] = discInfo;
            BasisDebug.Log("Disc info updated.", BasisDebug.LogTag.Event);
        }
        string filePath = BasisIOManagement.GenerateFilePath($"{discInfo.AssetIDToLoad}{BasisBundleManagement.MetaLinkBasisSuffix}", BasisBundleManagement.AssetBundlesFolder);
        byte[] serializedData = SerializationUtility.SerializeValue(discInfo, DataFormat.Binary);

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            await File.WriteAllBytesAsync(filePath, serializedData);
            BasisDebug.Log($"Disc info saved to {filePath}", BasisDebug.LogTag.Event);
        }
        catch (Exception ex)
        {
            BasisDebug.LogError($"Failed to save disc info: {ex.Message}", BasisDebug.LogTag.Event);
        }
    }

    public static void RemoveDiscInfo(string metaUrl)
    {
        if (OnDiscData.TryRemove(metaUrl, out _))
        {
            string filePath = BasisIOManagement.GenerateFilePath($"{metaUrl}{BasisBundleManagement.MetaLinkBasisSuffix}", BasisBundleManagement.AssetBundlesFolder);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                BasisDebug.Log($"Deleted disc info from {filePath}", BasisDebug.LogTag.Event);
            }
            else
            {
                BasisDebug.LogWarning($"File not found at {filePath}", BasisDebug.LogTag.Event);
            }
        }
        else
        {
            BasisDebug.LogError("Disc info not found or already removed.", BasisDebug.LogTag.Event);
        }
    }

    private static async Task EnsureInitializationComplete()
    {
        if (!IsInitialized)
        {
            await _initSemaphore.WaitAsync();
            try
            {
                if (!IsInitialized)
                {
                    await LoadAllDiscData();
                    IsInitialized = true;
                }
            }
            finally
            {
                _initSemaphore.Release();
            }
        }
    }

    private static async Task LoadAllDiscData()
    {
        BasisDebug.Log("Loading all disc data...", BasisDebug.LogTag.Event);
        string path = BasisIOManagement.GenerateFolderPath(BasisBundleManagement.AssetBundlesFolder);
        string[] files = Directory.GetFiles(path, $"*{BasisBundleManagement.MetaLinkBasisSuffix}");

        List<Task> loadTasks = new List<Task>();

        foreach (string file in files)
        {
            loadTasks.Add(Task.Run(async () =>
            {
                BasisDebug.Log($"Loading file: {file}");
                try
                {
                    byte[] fileData = await File.ReadAllBytesAsync(file);
                    OnDiscInformation discInfo = SerializationUtility.DeserializeValue<OnDiscInformation>(fileData, DataFormat.Binary);
                    OnDiscData.TryAdd(discInfo.StoredRemote.CombinedURL, discInfo);
                }
                catch (Exception ex)
                {
                    BasisDebug.LogError($"Failed to load disc info from {file}: {ex.Message}", BasisDebug.LogTag.Event);
                }
            }));
        }

        await Task.WhenAll(loadTasks);

        BasisDebug.Log("Completed loading all disc data.");
    }

    private static void CleanupFiles(BasisStoredEncryptedBundle bundle)
    {
        if (File.Exists(bundle.LocalConnectorPath))
        {
            File.Delete(bundle.LocalConnectorPath);
        }
        /* here LD
        BasisEncryptionToData.GenerateBundleFromFile(, bundle.LocalConnectorPath,bundle);

        bundle.LocalConnectorPath
        if (File.Exists(bundle.LocalBundleFile))
        {
            File.Delete(bundle.LocalBundleFile);
        }
        */
    }
}
