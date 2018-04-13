// Copyright (c) 2015 hugula
// direct https://github.com/tenvick/hugula
//
using System;
using System.Collections;
using System.IO;
using SLua;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lua = SLua.LuaSvr;
using Hugula.Loader;
using Hugula.Utils;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hugula
{
    /// <summary>
    /// 
    /// </summary>
    [CustomLuaClass]
    public class PLua : MonoBehaviour
    {

        public static string enterLua = "main";
        public LuaFunction onDestroyFn;
        public LuaFunction onAppPauseFn;
        public LuaFunction onAppQuitFn;
        public LuaFunction onAppFocusFn;

#if UNITY_EDITOR
        const string KeyDebugString = "_Plua_Debug_string";
        [SLua.DoNotToLua]
        public static bool isDebug
        {
            get
            {
                bool _debug = EditorPrefs.GetBool(KeyDebugString, true);
                return _debug;
            }
            set
            {
                EditorPrefs.SetBool(KeyDebugString, value);
            }
        }
#endif

        private static Lua lua;

        private static bool isLuaInitFinished = false;
        private string luaMain = "";
        private LuaFunction _updateFn;

        private static bool is_destroy = false;
        #region mono

        public LuaFunction updateFn
        {
            get { return _updateFn; }
            set
            {
                _updateFn = value;
            }
        }

        void Awake () {
            DontDestroyOnLoad (this.gameObject);
            _instance = this;
#if !HUGULA_NO_LOG
            Debug.Log (this.name + "Awake");
#endif
            if (lua == null) PreInitLua ();
        }

        IEnumerator Start () {
#if !HUGULA_NO_LOG
            Debug.Log ("Plua Start");
#endif
            while (isLuaInitFinished == false) {
                yield return null;
            }

            is_destroy = false;
#if !HUGULA_NO_LOG
            Debug.Log ("PLua Start lua init");
#endif
            LoadScript ();
#if !HUGULA_RELEASE
            // Debug.Log(luaBytesAsset);
            Debug.LogFormat("domain frame {0}", Time.frameCount);
#endif
            DoMain();
        }

	    void Update()
	    {
	        if (_updateFn != null) _updateFn.call();
	    }

        public static void DestoryLua()
        {
            if (lua != null && lua.luaState != null) lua.luaState.Close();
            lua = null;
        }

        void OnDestroy () {
            Debug.Log ("OnDestroy = " + name);
            if (onDestroyFn != null) onDestroyFn.call ();
            RemoveAllEvents ();
            StopAllCoroutines ();
            isLuaInitFinished = false;
            is_destroy = true;
            if (_instance == this) _instance = null;
            // if (ManifestManager.assetBundleManifest != null) UnityEngine.Object.Destroy(ManifestManager.assetBundleManifest);

        }

        void OnApplicationFocus(bool focusStatus)
        {
            if (onAppFocusFn != null) onAppFocusFn.call(this, focusStatus);
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (onAppPauseFn != null) onAppPauseFn.call(this, pauseStatus);
        }

        void OnApplicationQuit()
        {
            if (onAppQuitFn != null) onAppQuitFn.call(this, true);
        }
        #endregion

        #region private method

        private void SetLuaPath()
        {
            this.luaMain = "return require(\"" + enterLua + "\") \n";
        }

        private void LoadScript()
        {
            SetLuaPath();

            RegisterFunc();
#if UNITY_EDITOR
            Debug.LogFormat("<color=green>running {0} mode </color> <color=#8cacbc> change( menu Hugula->Debug Lua)</color>", isDebug ? "debug" : "release");
#endif
        }

        /// <summary>
        /// lua begin
        /// </summary>
        private void DoMain()
        {
            CUtils.DebugCastTime("");
            lua.luaState.doString(this.luaMain);
        }

        #endregion

        #region public method

        /// <summary>
        /// Pre Init Lua.
        /// </summary>
        /// <param name="sconds">Sconds.</param>
        public void PreInitLua () {
#if !HUGULA_RELEASE
            Debug.LogFormat ("ManagedThreadId = {0},frame={1}", System.Threading.Thread.CurrentThread.ManagedThreadId, Time.frameCount);
#endif
            if (lua == null) lua = new Lua ();
            Debug.LogFormat ("PreInitLua {0}", lua);
            CUtils.DebugCastTime ("");
            lua.init (null, () => {
                CUtils.DebugCastTime ("Slua binded");
                isLuaInitFinished = true;
            }, LuaSvrFlag.LSF_3RDDLL);
        }

        /// <summary>
        /// ReStart.
        /// </summary>
        /// <param name="sconds">Sconds.</param>
        public static void ReStart (float sconds) {
            StopAllDelay();
            var ins = _instance;
            if(ins)
            {
                GameObject.Destroy (ins.gameObject);
            }
            LoadFirstHelper.ReOpen (sconds);
        }

        /// <summary>
        /// Removes all events.
        /// </summary>
        public void RemoveAllEvents()
        {
            onDestroyFn = null;
            onAppPauseFn = null;
            onAppQuitFn = null;
            onAppFocusFn = null;
        }
        #endregion

        #region toolMethod

        /// <summary>
        /// load lua bytes
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private byte[] LoadLuaBytes(string name)
        {
            byte[] ret = null;
#if UNITY_EDITOR_WIN
            string cryName = CUtils.GetRightFileName(string.Format("{0}.{1}", name, Common.ASSETBUNDLE_SUFFIX));
            string path = CUtils.PathCombine(Application.dataPath, Common.LUACFOLDER + "/win");
            path = CUtils.PathCombine(path, cryName);
            ret = File.ReadAllBytes(path);
#elif UNITY_EDITOR_OSX
            string cryName = CUtils.GetRightFileName (string.Format ("{0}.{1}", name, Common.ASSETBUNDLE_SUFFIX));
            string path = CUtils.PathCombine (Application.dataPath, Common.LUACFOLDER + "/osx");
            path = CUtils.PathCombine (path, cryName);
            ret = File.ReadAllBytes (path);
#elif UNITY_IOS
            string cryName = "";
            if (System.IntPtr.Size == 4)
                cryName = CUtils.GetRightFileName (name);
            else
                cryName = CUtils.GetRightFileName (string.Format ("{0}_64", name));

            string abName = cryName + Common.CHECK_ASSETBUNDLE_SUFFIX;
            bool isupdate = ManifestManager.CheckIsUpdateFile(abName);
            string path = CUtils.PathCombine (CUtils.realPersistentDataPath,abName );
            if (isupdate &&  File.Exists (path)) {
                ret = File.ReadAllBytes (path);
            } else {
                var textAsset =(TextAsset)Resources.Load ("luac/"+cryName);
                ret = textAsset.bytes; // --Resources.Load
                Resources.UnloadAsset(textAsset);
            }
#else //android
            string cryName = CUtils.GetRightFileName (name);
            string abName = cryName + Common.CHECK_ASSETBUNDLE_SUFFIX;

            bool isupdate = ManifestManager.CheckIsUpdateFile(abName);
            string path = CUtils.PathCombine (CUtils.realPersistentDataPath, abName);
            if (isupdate && File.Exists (path)) {
                ret = File.ReadAllBytes (path);
            } else {
                var textAsset = (TextAsset) Resources.Load ("luac/" + cryName); //pc luaBundle.LoadAsset<TextAsset>(cryName);
                ret = textAsset.bytes; // --Resources.Load
                Resources.UnloadAsset (textAsset);
                // ret = luaBytesAsset.GetBytesByFileName(cryName);
            }
#endif
            return ret;
        }

        private void RegisterFunc()
        {
            LuaState.loaderDelegate = Loader;
        }

        /// <summary>
        ///  loader
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private byte[] Loader(string name)
        {
            byte[] str = null;
#if UNITY_EDITOR

            if (isDebug)
            {
                string name1 = name.Replace('.', '/');
                string path = Application.dataPath + "/Lua/" + name1 + ".lua";
                if (!File.Exists(path))
                    path = Application.dataPath + "/Config/" + name1 + ".lua";

                if (File.Exists(path))
                {
                    str = File.ReadAllBytes(path);
                }
                else
                {
                    Debug.LogWarningFormat("lua({0}) path={1} not exists.", name, path);
                    name = name.Replace('.', '+').Replace('/', '+');
                    str = LoadLuaBytes(name);
                }
            }
            else
            {
                name = name.Replace('.', '+').Replace('/', '+');
                str = LoadLuaBytes(name);
            }

#elif UNITY_STANDALONE && !HUGULA_RELEASE

            name = name.Replace ('.', '/');
            string path = Application.dataPath + "/config_data/" + name + ".lua"; //���ȶ�ȡ�����ļ�
            if (File.Exists(path))
            {
                str = File.ReadAllBytes(path);
            }
            else
            {
                name = name.Replace('.', '+').Replace('/', '+');
                str = LoadLuaBytes(name);
            }

#else
            name = name.Replace ('.', '+').Replace ('/', '+');
            str = LoadLuaBytes (name);
#endif
            return str;
        }

        public static void MultipleRequires (string[] requires) {
             coroutine.StartCoroutine (MultipleRequiresDo (requires, null));
        }

        public static void MultipleRequires (string[] requires, LuaFunction luafn) {
            coroutine.StartCoroutine (MultipleRequiresDo (requires, luafn));
        }

        public static object Delay (LuaFunction luafun, float time, params object[] args) {
            var _corout = DelayDo (luafun, time, args);
            coroutine.StartCoroutine (_corout);
            return _corout;
        }

        public static void StopDelay (object arg) {
            if (arg is IEnumerator)
                coroutine.StopCoroutine ((IEnumerator)arg);
            else
                Debug.LogWarningFormat("StopDelay argument error:{0}",arg);
        }

        public static void StopAllDelay () {
            coroutine.StopAllCoroutines ();
        }

        private static IEnumerator MultipleRequiresDo (string[] requires, LuaFunction onCompFn) {
            if (_instance == null)
                yield break;

            var item = requires.GetEnumerator ();
            string luastr = string.Empty;
            // var lua = _instance.lua;
            while (item.MoveNext ()) {
                luastr = item.Current.ToString ();
                if ("eof".Equals (luastr))
                    yield return null;
                else if (luastr.Contains ("(")) {
#if UNITY_EDITOR
                    // Debug.LogFormat("{0},framcount={1}", luastr, Time.frameCount);
#endif
                    lua.luaState.doString (luastr);
                } else {
#if UNITY_EDITOR
                    // Debug.LogFormat("{0},framcount={1}", luastr, Time.frameCount);
#endif    
                    lua.luaState.doFile (luastr);
                }
            }
            yield return null;
            // Debug.LogFormat("call function {0} ;", onCompFn != null);
            if (onCompFn != null) onCompFn.call ();
        }

        private static IEnumerator DelayDo (LuaFunction luafun, float time, params object[] args) {
            yield return new WaitForSeconds (time);
            if (!is_destroy)
                luafun.call (args);
        }

        #endregion

        #region static

        private static Coroutines _coroutine;
        public static Coroutines coroutine {
            get {
                if (_coroutine == null) {
                    Debug.Log ("create coroutine gameObject");
                    var obj = new GameObject ("coroutine");
                    _coroutine=obj.AddComponent<Coroutines>();
                    DontDestroyOnLoad (obj);
                }
                return _coroutine;
            }
        }
        private static PLua _instance;
        public static PLua instance
        {
            get
            {
                return _instance;
            }
        }
        // internal static PLua CreateInstance () {
        //     Debug.Log ("create instance PLua");
        //     var gam = new GameObject ("PLua");
        //     _instance = gam.AddComponent<PLua> ();
        //     return _instance;
        // }
        #endregion

    }

    [CustomLuaClass]
    public class Coroutines : MonoBehaviour { }
}