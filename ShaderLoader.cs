using System.Collections;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

namespace KerbPaint.Shaders
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class KerbPaintShaderLoader : MonoBehaviour
    {
        private static bool _loaded;

        private static string _bundlePath;

        public static Shader paintShader;

        public static Dictionary<string, Shader> LoadedShaders = new Dictionary<string, Shader>();

        public string BundlePath
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.OSXPlayer:
                        return _bundlePath + Path.DirectorySeparatorChar +
                               "shaders_macosx.bundle";
                    case RuntimePlatform.WindowsPlayer:
                        return _bundlePath + Path.DirectorySeparatorChar +
                               "shaders_windows.bundle";
                    case RuntimePlatform.LinuxPlayer:
                        return _bundlePath + Path.DirectorySeparatorChar +
                               "shaders_macosx.bundle";
                    default:
                        return _bundlePath + Path.DirectorySeparatorChar +
                               "shaders_windows.bundle";
                }
            }
        }

        private void Awake()
        {
            _bundlePath = KSPUtil.ApplicationRootPath + "GameData" +
                                                    Path.DirectorySeparatorChar +
                                                    "KerbPaint" + Path.DirectorySeparatorChar + "AssetBundles";
        }
        private void Start()
        {
            if (!_loaded)
            {
                Debug.Log("[KerbPaint] start bundle load process");
                StartCoroutine(LoadBundleAssets());
                _loaded = true;
            }
        }

        private AssetBundle shaderBundle;

        private IEnumerator LoadBundleAssets()
        {
            Debug.Log("[KerbPaint] Loading bundle data");

            shaderBundle = AssetBundle.LoadFromFile(BundlePath);

            if (shaderBundle != null)
            {
                var shaders = shaderBundle.LoadAllAssets<Shader>();

                foreach (var shader in shaders)
                {
                    Debug.Log($"[KerbPaint] Shader \"{shader.name}\" loaded. Shader supported? {shader.isSupported}");
                    LoadedShaders.Add(shader.name, shader);
                }
                yield return null;
                Debug.Log("[KerbPaint] unloading bundle");
                shaderBundle.Unload(false); // unload the raw asset bundle
            }
            else
            {
                Debug.Log("[KerbPaint] Error: Found no asset bundle to load");
            }
        }

        public static Shader LoadShader(string name)
        {
            switch (name)
            {
                //Alpha, AlphaSpec, Bump, BumpSpec, Cutoff, CutoffBump, Diffuse, Emissive, EmissiveBumpSpec, EmissiveSpec, Spec
                case "Alpha":
                    name = "KerbPaint/Alpha/Translucent";
                    break;
                case "AlphaSpec":
                    name = "KerbPaint/Alpha/Translucent Specular";
                    break;
                case "Bump":
                    name = "KerbPaint/Bumped";
                    break;
                case "BumpSpec":
                    name = "KerbPaint/Bumped Specular";
                    break;
                case "Cutoff":
                    name = "KerbPaint/Alpha/Cutoff";
                    break;
                case "CutoffBump":
                    name = "KerbPaint/Alpha/Cutoff Bumped";
                    break;
                case "Diffuse":
                    name = "KerbPaint/Diffuse";
                    break;
                case "Emissive":
                    name = "KerbPaint/Emissive/Diffuse";
                    break;
                case "EmissiveBumpSpec":
                    name = "KerbPaint/Alpha/ Translucent";
                    break;
                case "EmissiveSpec":
                    name = "KerbPaint/Emissive/Specular";
                    break;
                case "Spec":
                    name = "KerbPaint/Specular";
                    break;
                default:
                    name = "KerbPaint/Bumped";
                    break;
            }

            if (LoadedShaders.ContainsKey(name))
                return LoadedShaders[name];

            Debug.Log("Shader " + name + " not found!");
            return Shader.Find("Hidden/InternalErrorShader");
        }

    }
}
