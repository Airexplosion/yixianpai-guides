using System;
using UnityEngine;
using UnityEngine.Networking;
using Yx.ModSdk;

namespace YxGuides
{
    // 所有 Unity 调用和回调都由 MOD 的主线程 OnUpdate 驱动。
    internal sealed class GuideHttp
    {
        readonly ModContext _context;
        public GuideHttp(ModContext context) { _context = context; }
        string Loc(string zh, string en) { return GuideLocale.T(zh, en); }
        UnityWebRequest _request;
        Action<string, string> _done;
        float _started;
        public bool Busy { get { return _request != null; } }
        public void Get(string path, Action<string, string> done)
        {
            if (Busy) { done(null, Loc("请求正在进行，请稍等", "A request is already in progress")); return; }
            try
            {
                _request = UnityWebRequest.Get("https://auth.kaigua.vip/v1/guides" + path);
                _request.timeout = 15; _request.redirectLimit = 0; _done = done; _started = Time.realtimeSinceStartup;
                _request.SendWebRequest();
            }
            catch (Exception) { Cancel(); done(null, Loc("无法启动攻略下载，请检查网络", "Could not start guide download; check your connection")); }
        }
        public void Tick()
        {
            if (_request == null) return;
            if (_request.downloadedBytes > 196608 || Time.realtimeSinceStartup - _started > 18f) { Finish(null, Loc("请求超时或内容过大，已保留本地订阅", "Request timed out or response too large; local subscriptions kept")); return; }
            if (!_request.isDone) return;
            if (_request.isNetworkError || _request.isHttpError) { Finish(null, Loc("攻略服务暂不可用，已保留本地订阅", "Guide service unavailable; local subscriptions kept")); return; }
            string text = _request.downloadHandler.text;
            Finish(text, null);
        }
        void Finish(string text, string error) { Action<string, string> callback = _done; Cancel(); if (callback != null) callback(text, error); }
        public void Cancel()
        {
            _done = null;
            if (_request == null) return;
            _request.Abort(); _request.Dispose(); _request = null;
        }
    }
}
