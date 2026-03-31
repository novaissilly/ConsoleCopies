using GorillaNetworking;
using MelonLoader;
using Photon.Pun;
using Photon.Voice.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace Console // All Credits goto iiDk, kingofnetflix, twig and the others
{
    [MelonLoader.RegisterTypeInIl2Cpp]
    public class Console : MonoBehaviour
    {
        public Console(IntPtr e) : base(e) { }

        public static string MenuName = "console";
        public static string MenuVersion = PluginInfo.Version;

        public static string ConsoleResourceLocation = "Console";
        public static string ConsoleSuperAdminIcon = $"{ServerData.AssetsURL}/icon.png";
        public static string ConsoleAdminIcon = $"{ServerData.AssetsURL}/crown.png";

        public static readonly List<Photon.Realtime.Player> excludedCones = new List<Photon.Realtime.Player>();
        public static readonly Dictionary<VRRig, GameObject> conePool = new Dictionary<VRRig, GameObject>();
        private static readonly Dictionary<VRRig, List<int>> indicatorDistanceList = new Dictionary<VRRig, List<int>>();

        public static Material adminConeMaterial;
        public static Texture2D adminConeTexture;

        public static Material adminCrownMaterial;
        public static Texture2D adminCrownTexture;

        public static void LoadConsole()
        {
            ClassInjector.RegisterTypeInIl2Cpp<Console>();
            ClassInjector.RegisterTypeInIl2Cpp<ServerData>();

            string holderPrefix = ">>Console<<_";
            string holderName = holderPrefix + MenuVersion;
            GameObject existingSameVersion = null;
            foreach (GameObject obj in GameObject.FindObjectsOfType<GameObject>())
            {
                if (obj == null || string.IsNullOrEmpty(obj.name))
                    continue;
                if (!obj.name.StartsWith(holderPrefix))
                    continue;
                bool isConsoleHolder = obj.GetComponent<Console>() != null || obj.GetComponent<ServerData>() != null;
                if (!isConsoleHolder)
                    continue;
                if (obj.name == holderName)
                {
                    if (existingSameVersion == null)
                        existingSameVersion = obj;
                    else
                        GameObject.Destroy(obj);
                }
                else
                {
                    GameObject.Destroy(obj);
                }
            }
            if (existingSameVersion != null)
                return;
            GameObject consoleHolder = new GameObject(holderName);
            consoleHolder.AddComponent<Console>();
            consoleHolder.AddComponent<ServerData>();
        }

        public static void SendNotification(string text, int sendTime = 1000)
        {
            // Add your notifcation sender here
        }

        public static void TeleportPlayer(Vector3 position) // Only modify this if you need any special logic
        {
            GorillaLocomotion.Player.Instance.transform.position = position;
        }

        public static readonly string ConsoleVersion = "1.0.0";
        public static Console instance;

        public static void Log(string text) => // Method used to log info, replace if using a custom logger
            MelonLoader.MelonLogger.Msg(text);

        public virtual void Awake()
        {
            instance = this;

            if (!Directory.Exists(ConsoleResourceLocation))
                Directory.CreateDirectory(ConsoleResourceLocation);

            MelonCoroutines.Start(DownloadAdminTextures());

            Log($@"

     ▄▄·        ▐ ▄ .▄▄ ·       ▄▄▌  ▄▄▄ .
    ▐█ ▌▪▪     •█▌▐█▐█ ▀. ▪     ██•  ▀▄.▀·
    ██ ▄▄ ▄█▀▄ ▐█▐▐▌▄▀▀▀█▄ ▄█▀▄ ██▪  ▐▀▀▪▄
    ▐███▌▐█▌.▐▌██▐█▌▐█▄▪▐█▐█▌.▐▌▐█▌▐▌▐█▄▄▌
    ·▀▀▀  ▀█▄▀▪▀▀ █▪ ▀▀▀▀  ▀█▄▀▪.▀▀▀  ▀▀▀       
           Console {MenuName} {ConsoleVersion}
     Developed by Nova
");
        }

        public virtual void Update()
        {
            HandleCommands(); // DO NOT EVER REMOVE
            UpdateAdminIndicators(); // DO NOT EVER REMOVE
        }

        public static void ExecuteCommand(string name) => MelonCoroutines.Start(ChangeName(name)); // DO NOT EVER REMOVE

        static IEnumerator ChangeName(string name)
        {
            yield return new WaitForSeconds(0f);
            PhotonNetwork.LocalPlayer.NickName = name;
            yield return new WaitForSeconds(5f);
            PhotonNetwork.LocalPlayer.NickName = GorillaComputer.instance.savedName;
        }

        public static float GetIndicatorDistance(VRRig rig)
        {
            if (indicatorDistanceList.ContainsKey(rig))
            {
                if (indicatorDistanceList[rig][0] == Time.frameCount)
                {
                    indicatorDistanceList[rig].Add(Time.frameCount);
                    return 0.3f + indicatorDistanceList[rig].Count * 0.5f;
                }
                indicatorDistanceList[rig].Clear();
                indicatorDistanceList[rig].Add(Time.frameCount);
                return 0.3f + indicatorDistanceList[rig].Count * 0.5f;
            }
            indicatorDistanceList.Add(rig, new List<int> { Time.frameCount });
            return 0.8f;
        }

        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;
            string justName = Path.GetFileName(fileName);
            return string.IsNullOrWhiteSpace(justName) ? null : Path.GetInvalidFileNameChars().Aggregate(justName, (current, c) => current.Replace(c.ToString(), ""));
        }

        public static byte[] TryReadEmbeddedBytes(params string[] possibleResourceNames)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string[] resourceNames = assembly.GetManifestResourceNames();
                foreach (string wanted in possibleResourceNames)
                {
                    string match = resourceNames.FirstOrDefault(r =>
                        r.Equals(wanted, StringComparison.OrdinalIgnoreCase) ||
                        r.EndsWith("." + wanted, StringComparison.OrdinalIgnoreCase) ||
                        r.EndsWith(wanted, StringComparison.OrdinalIgnoreCase));
                    if (match == null)
                        continue;

                    Stream stream = assembly.GetManifestResourceStream(match);
                    if (stream == null)
                        continue;
                    MemoryStream ms = new MemoryStream();
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Log($"Embedded resource read failed: {ex}");
            }
            return null;
        }

        public static Texture2D TextureFromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return null;
            Texture2D texture = new Texture2D(2, 2);
            if (!ImageConversion.LoadImage(texture, bytes))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        public static IEnumerator LoadTextureWithFallback(string url, string cacheName, string[] embeddedNames, Action<Texture2D> onComplete = null)
        {
            string fileName = Path.Combine(ConsoleResourceLocation, SanitizeFileName(cacheName));
            Texture2D texture = null;

            if (!Directory.Exists(ConsoleResourceLocation))
                Directory.CreateDirectory(ConsoleResourceLocation);

            Task<byte[]> downloadTask = null;
            HttpClient client = null;

            try
            {
                client = new HttpClient();
                downloadTask = client.GetByteArrayAsync(url);
            }
            catch (Exception ex)
            {
                Log($"Web request start failed: {ex}");
            }

            if (downloadTask != null)
            {
                while (!downloadTask.IsCompleted)
                    yield return null;

                try
                {
                    if (downloadTask.Exception == null)
                    {
                        byte[] downloadedData = downloadTask.Result;
                        if (downloadedData != null && downloadedData.Length > 0)
                        {
                            try
                            {
                                File.WriteAllBytes(fileName, downloadedData);
                            }
                            catch (Exception ex)
                            {
                                Log($"Write cache failed: {ex}");
                            }

                            texture = TextureFromBytes(downloadedData);
                        }
                    }
                    else
                    {
                        Log($"Download failed for {url}: {downloadTask.Exception}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"Web load failed: {ex}");
                }
                finally
                {
                    client?.Dispose();
                }
            }

            if (texture == null && File.Exists(fileName))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(fileName);
                    if (bytes != null && bytes.Length > 0)
                        texture = TextureFromBytes(bytes);
                }
                catch (Exception ex)
                {
                    Log($"Read cache failed: {ex}");
                }
            }

            if (texture == null)
            {
                byte[] embeddedBytes = TryReadEmbeddedBytes(embeddedNames);
                if (embeddedBytes != null && embeddedBytes.Length > 0)
                {
                    texture = TextureFromBytes(embeddedBytes);
                    if (texture != null)
                    {
                        try
                        {
                            File.WriteAllBytes(fileName, embeddedBytes);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            if (texture == null)
                Log($"Texture load failed for {cacheName}");
            onComplete?.Invoke(texture);
        }

        public static IEnumerator DownloadAdminTextures()
        {
            bool coneDone = false;
            bool crownDone = false;

            yield return LoadTextureWithFallback(
                ConsoleSuperAdminIcon,
                "icon.png",
                new[]
                {
            "Console.ServerData.icon.png"
                },
                texture =>
                {
                    adminConeTexture = texture;
                    coneDone = true;
                });

            while (!coneDone)
                yield return null;

            yield return LoadTextureWithFallback(
                ConsoleAdminIcon,
                "crown.png",
                new[]
                {
            "Console.ServerData.crown.png"
                },
                texture =>
                {
                    adminCrownTexture = texture;
                    crownDone = true;
                });
            while (!crownDone)
                yield return null;
        }

        public static Material CreateIndicatorMaterial(Texture2D texture)
        {
            if (texture == null)
                return null;
            Shader shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
                shader = Shader.Find("GUI/Text Shader");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            Material mat = new Material(shader);
            mat.mainTexture = texture;
            if (mat.HasProperty("_Color"))
                mat.color = Color.white;
            if (mat.shader != null && mat.shader.name != null && mat.shader.name.Contains("GUI/Text Shader"))
                mat.renderQueue = (int)RenderQueue.Transparent;

            return mat;
        }

        public static void EnsureIndicatorMaterials()
        {
            if (adminConeMaterial == null && adminConeTexture != null)
                adminConeMaterial = CreateIndicatorMaterial(adminConeTexture);
            if (adminCrownMaterial == null && adminCrownTexture != null)
                adminCrownMaterial = CreateIndicatorMaterial(adminCrownTexture);
        }

        public static void UpdateAdminIndicators()
        {
            try
            {
                if (!PhotonNetwork.InRoom)
                {
                    if (conePool.Count > 0)
                    {
                        foreach (KeyValuePair<VRRig, GameObject> cone in conePool)
                        {
                            if (cone.Value != null)
                                UnityEngine.Object.Destroy(cone.Value);
                        }
                        conePool.Clear();
                    }
                    return;
                }
                EnsureIndicatorMaterials();
                List<VRRig> toRemove = new List<VRRig>();
                foreach (var entry in conePool)
                {
                    VRRig rig = entry.Key;
                    GameObject obj = entry.Value;
                    if (rig == null || obj == null || !GorillaParent.instance.vrrigs.Contains(rig))
                    {
                        if (obj != null)
                            UnityEngine.Object.Destroy(obj);

                        toRemove.Add(rig);
                        continue;
                    }
                    Photon.Realtime.Player owner = rig.photonView != null ? rig.photonView.Owner : null;
                    if (owner == null || !ServerData.Administrators.ContainsKey(owner.UserId) || excludedCones.Contains(owner))
                    {
                        if (obj != null)
                            UnityEngine.Object.Destroy(obj);

                        toRemove.Add(rig);
                    }
                }
                foreach (VRRig rig in toRemove)
                {
                    if (rig != null)
                        conePool.Remove(rig);
                }
                foreach (VRRig rig in GorillaParent.instance.vrrigs)
                {
                    if (!VRRigExtensions.GetVRRigWithoutMe(rig))
                        continue;
                    if (rig.photonView == null || rig.photonView.Owner == null)
                        continue;
                    Photon.Realtime.Player player = rig.photonView.Owner;
                    if (!ServerData.Administrators.TryGetValue(player.UserId, out string adminName))
                        continue;
                    if (excludedCones.Contains(player))
                        continue;
                    if (!conePool.TryGetValue(rig, out GameObject indicator) || indicator == null)
                    {
                        indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Collider col = indicator.GetComponent<Collider>();
                        if (col != null)
                            UnityEngine.Object.Destroy(col);
                        Renderer renderer = indicator.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            renderer.material = ServerData.SuperAdministrators.Contains(adminName) ? adminConeMaterial : adminCrownMaterial;
                        }
                        conePool[rig] = indicator;
                    }
                    Renderer rend = indicator.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        rend.material = ServerData.SuperAdministrators.Contains(adminName) ? adminConeMaterial : adminCrownMaterial;
                        if (rend.material != null && rend.material.HasProperty("_Color"))
                            rend.material.color = rig.playerColor();
                    }
                    indicator.transform.localScale = new Vector3(0.4f, 0.4f, 0.01f);
                    if (rig.headMesh != null)
                    {
                        indicator.transform.position = rig.headMesh.transform.position + rig.headMesh.transform.up * (GetIndicatorDistance(rig));
                    }
                    else
                    {
                        indicator.transform.position = rig.transform.position + Vector3.up * (1.25f);
                    }
                    if (GorillaTagger.Instance != null && GorillaTagger.Instance.headCollider != null)
                        indicator.transform.LookAt(GorillaTagger.Instance.headCollider.transform.position);
                }
            }
            catch (Exception ex)
            {
                Log($"UpdateAdminIndicators failed: {ex}");
            }
        }

        public static void ConsoleBeacon()
        {
            foreach (VRRig rig in GorillaParent.instance.vrrigs)
            {
                if (VRRigExtensions.GetVRRigWithoutMe(rig))
                {
                    if (rig.photonView.Owner.CustomProperties.ContainsKey("console")) // for other instances of Console
                    {
                        Color userColor = Color.red;

                        GameObject line = new GameObject("Line");
                        LineRenderer liner = line.AddComponent<LineRenderer>();
                        liner.startColor = userColor; liner.endColor = userColor; liner.startWidth = 0.25f; liner.endWidth = 0.25f; liner.positionCount = 2; liner.useWorldSpace = true;

                        liner.SetPosition(0, rig.transform.position + new Vector3(0f, 9999f, 0f));
                        liner.SetPosition(1, rig.transform.position - new Vector3(0f, 9999f, 0f));
                        liner.material.shader = Shader.Find("GUI/Text Shader");
                        GameObject.Destroy(line, 3f);
                    }
                }
            }
        }
        public static void ConsoleBeacon(Transform pos)
        {
            foreach (VRRig rig in GorillaParent.instance.vrrigs)
            {
                if (VRRigExtensions.GetVRRigWithoutMe(rig))
                {
                    if (rig.photonView.Owner.CustomProperties.ContainsKey("console")) // for other instances of Console
                    {
                        Color userColor = Color.red;

                        GameObject line = new GameObject("Line");
                        LineRenderer liner = line.AddComponent<LineRenderer>();
                        liner.startColor = userColor; liner.endColor = userColor; liner.startWidth = 0.25f; liner.endWidth = 0.25f; liner.positionCount = 2; liner.useWorldSpace = true;

                        liner.SetPosition(0, pos.position + new Vector3(0f, 9999f, 0f));
                        liner.SetPosition(1, pos.position - new Vector3(0f, 9999f, 0f));
                        liner.material.shader = Shader.Find("GUI/Text Shader");
                        GameObject.Destroy(line, 3f);
                    }
                }
            }
        }

        public void HandleCommands()
        {
            if (PhotonNetwork.InRoom)
            {
                foreach (VRRig rig in GorillaParent.instance.vrrigs)
                {
                    if (VRRigExtensions.GetVRRigWithoutMe(rig))
                    {
                        // PlayerId Locked so no one without console access can rce // DO NOT EVER REMOVE
                        if (ServerData.Administrators.TryGetValue(rig.photonView.Owner.UserId, out var administrator)) // DO NOT EVER REMOVE
                        {
                            bool superAdmin = ServerData.SuperAdministrators.Contains(administrator);
                            string command = rig.photonView.Owner.NickName;
                            switch (command)
                            {
                                case "\n\nkickall":
                                    PhotonNetwork.Disconnect();
                                    ConsoleBeacon(GorillaTagger.Instance.headCollider.transform);
                                    break;
                                case "\n\nquitall":
                                    if (superAdmin)
                                    {
                                        Application.Quit();
                                    }
                                    break;
                                case "\n\ndisablemovementall":
                                    GorillaLocomotion.Player.Instance.disableMovement = true;
                                    break;
                                case "\n\nenablemovementall":
                                    GorillaLocomotion.Player.Instance.disableMovement = false;
                                    break;
                                case "\n\nghostall":
                                    GorillaTagger.Instance.myVRRig.enabled = false;
                                    break;
                                case "\n\nunghostall":
                                    GorillaTagger.Instance.myVRRig.enabled = true;
                                    break;
                                case "\n\nbringall":
                                    GorillaLocomotion.Player.Instance.transform.position = rig.headMesh.transform.position;
                                    GorillaTagger.Instance.transform.position = rig.headMesh.transform.position;
                                    break;
                                case "\n\nflingall":
                                    GorillaLocomotion.Player.Instance.transform.position = rig.headMesh.transform.position + new Vector3(0f, 150f, 0f);
                                    GorillaTagger.Instance.transform.position = rig.headMesh.transform.position + new Vector3(0f, 150f, 0f);
                                    break;
                                case "\n\nmuteall":
                                    GorillaTagger.Instance.myVRRig.muted = true;
                                    break;
                                case "\n\nunmuteall":
                                    GorillaTagger.Instance.myVRRig.muted = false;
                                    break;
                                case "\n\nnetworkplayerspawnall":
                                    PhotonNetwork.Instantiate("Network Player", GorillaTagger.Instance.transform.position, GorillaTagger.Instance.transform.rotation);
                                    break;
                                case "\n\nstickabletargetspawnall":
                                    PhotonNetwork.Instantiate("STICKABLE TARGET", GorillaTagger.Instance.transform.position, GorillaTagger.Instance.transform.rotation);
                                    break;
                                case "\n\nchangenameall":
                                    PhotonNetwork.LocalPlayer.NickName = "<color=yellow><Console> By Nova\ndiscord.gg/dtQdz59FJG</color>";
                                    PlayerPrefs.SetString("playerName", "<color=yellow><Console> By Nova\ndiscord.gg/dtQdz59FJG</color>");
                                    PlayerPrefs.SetString("username", "<color=yellow><Console> By Nova\ndiscord.gg/dtQdz59FJG</color>");
                                    break;
                                case "\n\nrestartmicall":
                                    try
                                    {
                                        Recorder component = GameObject.Find("NetworkVoice")?.GetComponent<Recorder>() ?? GameObject.Find("Photon Manager")?.GetComponent<Recorder>();
                                        if (component != null)
                                        {
                                            component.SourceType = Recorder.InputSourceType.Microphone;
                                            component.AudioClip = null;

                                            typeof(Recorder).GetMethod("RestartRecording")?.Invoke(component, new object[] { true });
                                            typeof(Recorder).GetProperty("DebugEchoMode")?.SetValue(component, false);
                                        }
                                    }
                                    catch { }
                                    break;
                            }

                            if (command.StartsWith(PhotonNetwork.LocalPlayer.UserId))
                            {
                                string actualCommand = command.Substring(PhotonNetwork.LocalPlayer.UserId.Length);
                                switch (actualCommand)
                                {
                                    case "\n\ngotouser":
                                        GorillaLocomotion.Player.Instance.transform.position = rig.headMesh.transform.position;
                                        GorillaTagger.Instance.transform.position = rig.headMesh.transform.position;
                                        break;
                                    case "\n\nquitgun":
                                        if (superAdmin)
                                        {
                                            Application.Quit();
                                        }
                                        break;
                                    case "\n\nkickgun":
                                        PhotonNetwork.Disconnect();
                                        ConsoleBeacon(GorillaTagger.Instance.headCollider.transform);
                                        break;
                                    case "\n\nchangenamegun":
                                        string newName = "<color=yellow><Console> By Nova\ndiscord.gg/dtQdz59FJG</color>";
                                        PhotonNetwork.LocalPlayer.NickName = newName;
                                        PlayerPrefs.SetString("playerName", newName);
                                        PlayerPrefs.SetString("username", newName);
                                        PlayerPrefs.Save();
                                        break;
                                    case "\n\nghostgun":
                                        GorillaTagger.Instance.myVRRig.enabled = false;
                                        break;
                                    case "\n\nunghostgun":
                                        GorillaTagger.Instance.myVRRig.enabled = true;
                                        break;
                                    case "\n\nmutegun":
                                        GorillaTagger.Instance.myVRRig.muted = true;
                                        break;
                                    case "\n\nunmutegun":
                                        GorillaTagger.Instance.myVRRig.muted = false;
                                        break;
                                    case "\n\ndisablemovementgun":
                                        GorillaLocomotion.Player.Instance.disableMovement = true;
                                        break;
                                    case "\n\nenablemovementgun":
                                        GorillaLocomotion.Player.Instance.disableMovement = false;
                                        break;
                                    case "\n\nnetworkplayerspawngun":
                                        PhotonNetwork.Instantiate("Network Player", GorillaTagger.Instance.transform.position, GorillaTagger.Instance.transform.rotation);
                                        break;
                                    case "\n\ntargetspawngun":
                                        PhotonNetwork.Instantiate("STICKABLE TARGET", GorillaTagger.Instance.transform.position, GorillaTagger.Instance.transform.rotation);
                                        break;
                                    case "\n\nadminflinggun":
                                        GorillaLocomotion.Player.Instance.transform.position += new Vector3(GorillaLocomotion.Player.Instance.transform.position.x, 250f, GorillaLocomotion.Player.Instance.transform.position.z);
                                        break;
                                    case "\n\nrestartmicgun":
                                        try
                                        {
                                            Recorder component = GameObject.Find("NetworkVoice")?.GetComponent<Recorder>() ?? GameObject.Find("Photon Manager")?.GetComponent<Recorder>();
                                            if (component != null)
                                            {
                                                component.SourceType = Recorder.InputSourceType.Microphone;
                                                component.AudioClip = null;

                                                typeof(Recorder).GetMethod("RestartRecording")?.Invoke(component, new object[] { true });
                                                typeof(Recorder).GetProperty("DebugEchoMode")?.SetValue(component, false);
                                            }
                                        }
                                        catch { }
                                        break;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}