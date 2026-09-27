using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Unity;
using Yx.Shared;

namespace YxGuides
{
    public sealed class GuideMod : YxMod
    {
        readonly List<GuideBook> _subscriptions = new List<GuideBook>();
        GuideHttp _http;
        sealed class StageSpent { public string Id; public int Count; }
        readonly List<StageSpent> _spent = new List<StageSpent>();
        sealed class CardMark { public CardItem Item; public TextMeshProUGUI Label; }
        readonly List<CardMark> _marks = new List<CardMark>();
        UiKit _ui, _panelUi;
        GameObject _panel, _tab, _mini;
        TextMeshProUGUI _miniText;
        UiInput _searchInput, _shareInput;
        string _search = "", _shareCode = "", _message = "", _view = "discover", _selected = "", _session = "", _stage = "";
        GuideBook _detail, _active;
        GuideSnapshot _snapshot;
        List<object> _catalog = new List<object>();
        int _offset, _page, _stagePage, _textPage, _round, _roundUsed, _stageUsed;
        bool _more, _visible, _miniOpen = true, _sawLobby, _roundKnown, _stageKnown, _sessionKnown, _counterAvailable;
        float _nextTick;
        string L(string zh, string en) { return GuideLocale.T(zh, en); }

        public override void OnLoad(ModContext ctx)
        {
            _http = new GuideHttp(ctx);
            _ui = new UiKit(ctx);
            _panelUi = new UiKit(ctx);
            _selected = ctx.Data.Get<string>("selected", "");
            List<object> cache = ctx.Data.Get<List<object>>("subscriptions", null);
            if (cache != null) for (int i = 0; i < cache.Count; i++)
            {
                try { string raw = cache[i] as string; if (raw != null) _subscriptions.Add(GuideBook.Read(raw)); }
                catch (Exception) { ctx.Log.Warn(GuideLocale.T("一份本地攻略缓存损坏，已跳过", "A local guide cache was invalid and was skipped")); }
            }
            ctx.Input.RegisterHotkey("guides", "CTRL+ALT+G", Toggle);
            _counterAvailable = ctx.Hooks.TryPrefix("ReplaceArea", "set_replaceChance", 1, OnChance) != null;
            if (!_counterAvailable)
                ctx.Log.Warn(GuideLocale.T("换牌计数入口不可用，仅展示作者设置的预算", "Swap counter unavailable; showing the author's budget only"));
            _sawLobby = SceneLoader.currentSceneName == "Lobby" || SceneLoader.currentSceneName == "Home";
        }
        bool OnChance(HookContext h)
        {
            var area = h.Instance as ReplaceArea;
            if (area == null || h.Args == null || h.Args.Length == 0 || _session.Length == 0 || _active == null) return true;
            BattleManager bm = BattleManager.Instance;
            Proto.GameStatus gs = bm == null ? null : bm.currentGameStatus;
            Proto.BattlePlayerData me = gs == null ? null : GameStatusExtension.GetMainPlayerData(gs);
            if (me == null || gs.round != _round || gs.codeId.ToString(CultureInfo.InvariantCulture) + ":" + me.uid != _session) return true;
            int after = Convert.ToInt32(h.Args[0], CultureInfo.InvariantCulture);
            int before = area.replaceChance;
            // 只旁观原生 setter；次数增加不抵扣已花费的预算。
            if (before >= 0 && after >= 0 && after < before) { _roundUsed += before - after; _stageUsed += before - after;
                for (int i = 0; i < _spent.Count; i++) if (_spent[i].Id == _stage) _spent[i].Count = _stageUsed; }
            return true;
        }
        public override void OnUpdate()
        {
            _http.Tick();
            if (Time.realtimeSinceStartup < _nextTick) return;
            _nextTick = Time.realtimeSinceStartup + 0.5f;
            if (_tab == null) BuildTab();
            try { Observe(); }
            catch (Exception) { _snapshot = null; }
            MarkCards();
            ShowMini();
            if (_visible && _panel == null) Render();
        }
        void Observe()
        {
            BattleManager bm = BattleManager.Instance;
            if (SceneLoader.currentSceneName == "Lobby" || SceneLoader.currentSceneName == "Home") { _snapshot = null; _session = ""; _active = null; _sawLobby = true;
                if (Context.Data.Has("activeSession")) { Context.Data.Remove("activeSession"); Context.Data.Remove("activeGuide"); } return; }
            if (bm == null) { _snapshot = null; return; }
            _snapshot = GameReader.Read();
            if (_snapshot == null) return;
            if (_snapshot.Session != _session)
            {
                _session = _snapshot.Session; _active = Find(_selected); _round = _snapshot.Round; _stage = "";
                if (Context.Data.Get<string>("activeSession", "") == _session)
                { try { _active = GuideBook.Read(Context.Data.Get<string>("activeGuide", "")); } catch (Exception) { _active = null; } }
                else SaveLock();
                _roundUsed = 0; _stageUsed = 0; _spent.Clear(); _roundKnown = _sawLobby && _round == 1; _sessionKnown = _roundKnown; _stageKnown = _sessionKnown; _sawLobby = false;
            }
            if (_snapshot.Round != _round) { _round = _snapshot.Round; _roundUsed = 0; _roundKnown = true; }
            if (_active != null)
            {
                string error; StageRule s = GuideRules.Select(_active, _snapshot, out error);
                if (s != null && s.Id != _stage) { _stageKnown = _sessionKnown; _stage = s.Id; _stageUsed = 0; StageSpent found = null;
                    for (int i = 0; i < _spent.Count; i++) if (_spent[i].Id == _stage) found = _spent[i];
                    if (found == null) { found = new StageSpent(); found.Id = _stage; _spent.Add(found); } _stageUsed = found.Count; }
            }
        }
        void BuildTab()
        {
            _tab = _ui.Panel("GuideTab", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 210f), new Vector2(140f, 46f), new Color(.07f,.14f,.16f,.95f), true);
            if (_tab != null) _ui.TextButton(_tab.transform,"open",L("攻略订阅", "Guides"),new Vector2(4f,-4f),new Vector2(132f,38f),Toggle);
        }
        void Toggle() { _visible = !_visible; if (_visible) { Render(); if (_catalog.Count == 0 && !_http.Busy) Search(); } else ClosePanel(); }
        void ClosePanel() { _panelUi.DestroyAll(); _panel = null; }
        void Message(string message) { _message = message; if (_visible) Render(); }
        void Search()
        {
            if (_http.Busy) { Message(L("请求正在进行，请稍等", "A request is already in progress")); return; }
            _view = "discover"; _message = L("正在加载攻略…", "Loading guides…"); Render();
            _http.Get("?q=" + Uri.EscapeDataString(_search) + "&offset=" + GuideBook.Num(_offset), OnCatalog);
        }
        void OnCatalog(string raw, string error)
        {
            if (error != null) { Message(error); return; }
            try { Dictionary<string, object> d = Json.ParseObject(raw); _catalog = Json.GetArray(d, "items") ?? new List<object>(); _more = Json.GetBool(d, "hasMore", false); _page = 0; Message(L("攻略已刷新", "Guides refreshed")); }
            catch (Exception) { Message(L("攻略列表格式不兼容，请更新 MOD", "Guide list is incompatible; update this MOD")); }
        }
        void LoadDetail(string id)
        {
            if (_http.Busy) { Message(L("请求正在进行，请稍等", "A request is already in progress")); return; }
            _http.Get("/" + Uri.EscapeDataString(id), OnDetail);
        }
        void OnDetail(string raw, string error)
        {
            if (error != null) { Message(error); return; }
            try { _detail = GuideBook.Read(raw); _view = "detail"; _stagePage = 0; _textPage = 0; Message(L("下载完成；点击订阅保存到本地", "Downloaded. Subscribe to keep this guide locally")); }
            catch (Exception e) { Message(GuideLocale.Error(e.Message)); }
        }
        void ImportCode()
        {
            if (_shareInput != null) _shareCode = _shareInput.Text;
            string id; int version;
            if (!GuideShareCode.TryParse(_shareCode, out id, out version))
            { Message(L("订阅码格式不正确", "Invalid subscription code")); return; }
            if (_http.Busy) { Message(L("请求正在进行，请稍等", "A request is already in progress")); return; }
            _http.Get("/" + id + "/versions/" + GuideBook.Num(version), OnImport);
        }
        void OnImport(string raw, string error)
        {
            if (error != null) { Message(error); return; }
            try
            {
                _detail = GuideBook.Read(raw);
                if (!SaveSubscription()) return;
                _view = "detail";
                _stagePage = 0;
                _textPage = 0;
                Message(L("订阅码已导入", "Subscription code imported"));
            }
            catch (Exception e) { Message(GuideLocale.Error(e.Message)); }
        }
        GuideBook Find(string id) { for (int i = 0; i < _subscriptions.Count; i++) if (_subscriptions[i].Id == id) return _subscriptions[i]; return null; }
        void Subscribe() { SaveSubscription(); }
        bool SaveSubscription()
        {
            if (_detail == null) return false;
            List<object> next;
            if (!GuideSubscriptions.TryPrepare(_subscriptions, _detail, out next))
            { Message(L("本地订阅空间不足，请先取消部分订阅", "Local subscription limit reached; remove a guide first")); return false; }
            Context.Data.Set("subscriptions", next);
            _subscriptions.Clear(); for (int i = 0; i < next.Count; i++) _subscriptions.Add(GuideBook.Read((string)next[i]));
            Message(L("已订阅，离线可用。本局仍使用开局锁定的版本。", "Subscribed and available offline. This match keeps its locked version."));
            return true;
        }
        void Unsubscribe()
        {
            if (_detail == null) return;
            var next = new List<object>();
            for (int i = _subscriptions.Count - 1; i >= 0; i--) if (_subscriptions[i].Id == _detail.Id) _subscriptions.RemoveAt(i);
            for (int i = 0; i < _subscriptions.Count; i++) next.Add(_subscriptions[i].Raw);
            Context.Data.Set("subscriptions", next);
            if (_selected == _detail.Id) { _selected = ""; Context.Data.Set("selected", ""); }
            Message(L("已取消订阅；正在进行的对局保留本局版本直到结束", "Unsubscribed; an active match keeps its locked guide until it ends"));
        }
        void Use()
        {
            if (_detail == null || Find(_detail.Id) == null) { Message(L("请先订阅这份攻略", "Subscribe to this guide first")); return; }
            if (_session.Length > 0 && _active != null) { Message(L("本局已锁定攻略；返回大厅后可更换下一局攻略", "This match has a locked guide; choose another after returning to the lobby")); return; }
            _selected = _detail.Id; Context.Data.Set("selected", _selected);
            if (_session.Length > 0) { _active = Find(_selected); _roundKnown = false; _stageKnown = false; _sessionKnown = false; _stage = ""; _roundUsed = 0; _stageUsed = 0; _spent.Clear(); SaveLock(); }
            Message(L("已选择：", "Selected: ") + _detail.Title + L("。中途启用时不会猜测之前花掉的换牌次数。", ". Previous swaps are unknown if enabled mid-match."));
        }
        void Render()
        {
            if (!_visible) return; ClosePanel();
            _panel = _panelUi.Panel("GuideSubscriptions",new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(1000f,720f),new Color(.055f,.095f,.11f,.99f),true);
            if (_panel == null) return;
            Label(L("攻略订阅", "Guides"),24f,20f,550f,36f,26f);
            Button(L("关闭", "Close"),880f,18f,96f,Toggle);
            Button(L("发现攻略", "Discover"),24f,70f,150f,Discover); Button(L("我的订阅", "My guides"),184f,70f,150f,Mine); Button(L("本局攻略", "Current guide"),344f,70f,150f,Current);
            Label(_message,24f,651f,944f,52f,18f);
            if (_view == "detail" && _detail != null) { RenderDetail(); return; }
            if (_view == "current") { PagedText(Advice(),132f,480f); return; }
            if (_view == "mine")
            {
                _shareInput = _ui.TextInput(_panel.transform,"share-code",new Vector2(24f,-130f),new Vector2(730f,42f),80,OnShareText);
                _shareInput.SetText(_shareCode);
                Button(L("导入订阅码", "Import code"),774f,130f,196f,ImportCode);
                Label(L("已缓存 ", "Saved ") + GuideBook.Num(_subscriptions.Count) + L(" 份 · 局外选择，下局自动启用", " guides · select one outside battle for the next match"),24f,184f,940f,40f,20f);
                if (_subscriptions.Count == 0) Label(L("还没有订阅。到「发现攻略」打开详情并订阅。", "No subscriptions yet. Open a guide under Discover and subscribe."),24f,245f,920f,80f,22f);
                int start = _page * 5;
                for (int i = start; i < _subscriptions.Count && i < start + 5; i++)
                {
                    GuideBook book = _subscriptions[i]; float y = 232f + (i - start) * 68f;
                    Button(book.Title,24f,y,600f,delegate { _detail = book; _view = "detail"; _stagePage = 0; _textPage = 0; Render(); });
                    Label(book.Author + " · v" + GuideBook.Num(book.Version),640f,y + 8f,320f,45f,18f);
                }
                Pager(_subscriptions.Count); return;
            }
            _searchInput = _ui.TextInput(_panel.transform,"search",new Vector2(24f,-130f),new Vector2(730f,42f),80,OnSearchText); _searchInput.SetText(_search);
            Button(L("搜索", "Search"),774f,130f,196f,SearchClicked);
            if (_catalog.Count == 0) Label(L("暂无攻略。作者发布后会出现在这里。", "No guides yet. Published guides will appear here."),24f,214f,920f,80f,22f);
            int from = _page * 5;
            for (int i = from; i < _catalog.Count && i < from + 5; i++)
            {
                Dictionary<string, object> item = _catalog[i] as Dictionary<string, object>; if (item == null) continue;
                string id = GuideBook.Text(item,"id"); string title = GuideBook.Text(item,"title"); float y = 194f + (i - from) * 78f;
                GuideBook saved = Find(id); if (saved != null && saved.Version < GuideBook.Number(item,"version",0)) title += L("  [有更新]", "  [Update]");
                Button(title,24f,y,600f,delegate { LoadDetail(id); });
                string line = GuideBook.Text(item,"author") + " · " + GuideBook.Text(item,"hero") + " / " + GuideBook.Text(item,"career");
                Label(line,640f,y,330f,62f,17f);
            }
            Pager(_catalog.Count);
        }
        void OnSearchText(string text) { _search = text; }
        void OnShareText(string text) { _shareCode = text; }
        void SearchClicked() { if (_searchInput != null) _search = _searchInput.Text; _offset = 0; Search(); }
        void Discover() { _view = "discover"; _page = 0; _textPage = 0; Render(); }
        void Mine() { _view = "mine"; _page = 0; _textPage = 0; Render(); }
        void Current() { _view = "current"; _textPage = 0; Render(); }
        void Pager(int count)
        {
            Button(L("上一页", "Previous"),24f,599f,125f,delegate { if (_page > 0) { _page--; Render(); } else if (_view == "discover" && _offset > 0) { _offset = Math.Max(0,_offset-20); Search(); } });
            Label(L("第 ", "Page ") + GuideBook.Num(_offset / 5 + _page + 1) + L(" 页", ""),170f,608f,200f,30f,18f);
            Button(L("下一页", "Next"),840f,599f,130f,delegate { if ((_page+1)*5 < count) { _page++; Render(); } else if (_view == "discover" && _more) { _offset += 20; Search(); } });
        }
        void RenderDetail()
        {
            Label(_detail.Title + " · " + _detail.Author + " · v" + GuideBook.Num(_detail.Version),24f,126f,940f,45f,23f);
            Label(_detail.Hero + " / " + _detail.Career + L(" · 适用 ", " · Game ") + _detail.GameVersion,24f,171f,940f,35f,18f);
            Button(Find(_detail.Id)==null?L("订阅到本地", "Subscribe"):L("保存此版本", "Save version"),24f,215f,175f,Subscribe);
            Button(L("选择攻略", "Select guide"),210f,215f,155f,Use); Button(L("检查更新", "Check update"),376f,215f,155f,RefreshDetail); Button(L("取消订阅", "Unsubscribe"),542f,215f,155f,Unsubscribe);
            Button(L("上一阶段", "Prev stage"),708f,215f,125f,PrevStage); Button(L("下一阶段", "Next stage"),844f,215f,125f,NextStage);
            Button(L("原生查看", "Native view"),24f,265f,125f,OpenNativeDetail);
            string text = _detail.Summary + (char)10 + (char)10 + GuideRules.Describe(_detail.Stages[_stagePage], GuideLocale.English);
            if (_detail.GameVersion != Context.Versions.Game) text = L("注意：攻略版本与当前游戏版本不一致，仅供参考。", "Guide version differs from the game; reference only.") + (char)10 + text;
            PagedText(text,320f,260f);
        }
        void RefreshDetail() { if (_detail != null) LoadDetail(_detail.Id); }
        void OpenNativeDetail()
        {
            if (_detail == null) return;
            if (NativeGuideView.Show(_detail, Context)) { _visible = false; ClosePanel(); }
            else Message(L("请在游戏大厅打开原生详情；当前页面无法接入游戏面板。", "Open the native detail in the lobby; the game panel is unavailable here."));
        }
        void PrevStage() { _stagePage = Math.Max(0,_stagePage-1); _textPage=0;Render(); }
        void NextStage() { _stagePage = Math.Min(_detail.Stages.Count-1,_stagePage+1); _textPage=0;Render(); }
        void PagedText(string text, float y, float height)
        {
            // 先按可见宽度折行，再分页，避免长说明挤出面板。
            var lines = new List<string>(); string[] original = text.Replace("\r", "").Split((char)10);
            for (int i=0;i<original.Length;i++) { string line=original[i]; if(line.Length==0)lines.Add("");for(int p=0;p<line.Length;p+=43)lines.Add(line.Substring(p,Math.Min(43,line.Length-p))); }
            int per = (int)(height/27f); int pages = Math.Max(1,(lines.Count+per-1)/per);_textPage=Math.Min(_textPage,pages-1);
            var b=new StringBuilder();for(int i=_textPage*per;i<lines.Count&&i<(_textPage+1)*per;i++)b.Append(lines[i]).Append((char)10);
            Label(b.ToString(),24f,y,944f,height,20f);
            Button(L("前页", "Previous"),24f,599f,125f,delegate {_textPage=Math.Max(0,_textPage-1);Render();});
            Label(GuideBook.Num(_textPage+1)+" / "+GuideBook.Num(pages),175f,608f,150f,28f,18f);
            Button(L("后页", "Next"),840f,599f,130f,delegate {_textPage=Math.Min(pages-1,_textPage+1);Render();});
        }
        string Advice()
        {
            if (_active == null) return L("本局尚未启用攻略。先订阅，再在详情中选择攻略。", "No guide active for this match. Subscribe, then select it in the detail view.");
            if (_snapshot == null) return L("等待备战界面，拖牌时暂停更新建议。", "Waiting for preparation; tips pause while cards are being dragged.");
            if (_active.GameVersion != Context.Versions.Game) return L("攻略适用版本 ", "Guide version ") + _active.GameVersion + L("，当前游戏 ", ", current game ") + Context.Versions.Game + L("。暂停自动建议，请联系作者更新；仍可查看阶段说明。", ". Automatic tips are paused; stage notes remain available.");
            string error; StageRule s = GuideRules.Select(_active,_snapshot,out error); if(s==null)return GuideLocale.Error(error);
            var b=new StringBuilder();b.Append(_active.Title).Append(" · ").Append(_active.Author).Append(" · v").Append(GuideBook.Num(_active.Version));
            b.Append((char)10).Append(s.Name).Append(L(" · 第 ", " · Round ")).Append(GuideBook.Num(_snapshot.Round)).Append(L(" 轮", ""));
            bool known = _counterAvailable && (s.PerRound < 0 || _roundKnown) && (s.PerStage < 0 || _stageKnown);
            if(known && _snapshot.Remaining >= 0)b.Append((char)10).Append(L("预算内还可换 ", "Swaps remaining within budget: ")).Append(GuideBook.Num(GuideRules.Budget(s,_snapshot.Remaining,_roundUsed,_stageUsed,GuideRules.Ready(s,_snapshot.Cards)))).Append(L(" 次；剩余保底 ", "; reserve ")).Append(GuideBook.Num(s.Reserve));
            else b.Append((char)10).Append(L("换牌历史不完整，仅展示作者预算，不推算剩余可用次数。", "Swap history incomplete; only the author's budget is shown."));
            b.Append((char)10).Append(L("已观测本轮消耗 ", "Swaps observed this round: ")).Append(GuideBook.Num(_roundUsed)).Append(L(" 次", ""));
            int[] slots=GuideRules.Assign(s,_snapshot.Cards);
            for(int i=0;i<slots.Length;i++)if(s.Target[i].Options.Count>0){b.Append((char)10).Append(L("第 ", "Slot ")).Append(GuideBook.Num(i+1)).Append(L(" 格：", ": "));if(slots[i]>=0)b.Append(_snapshot.Cards[slots[i]].Name).Append(L("（已拥有）", " (owned)"));else {b.Append(L("缺 ", "Missing "));for(int j=0;j<s.Target[i].Options.Count;j++){if(j>0)b.Append(" / ");b.Append(s.Target[i].Options[j].Name);}}}
            for(int i=0;i<s.Keep.Count;i++){CardRule c=s.Keep[i];b.Append((char)10).Append(L("留 ", "Keep ")).Append(c.Name).Append(" ×").Append(GuideBook.Num(c.Count)).Append(L("（已有 ", " (owned ")).Append(GuideBook.Num(GuideRules.Count(c,_snapshot.Cards))).Append(L("）：", "): ")).Append(c.Reason);}
            b.Append((char)10).Append(s.Note).Append((char)10).Append(L("未提及的牌：攻略未说明；不自动换牌或炼化。", "Unmentioned cards are unspecified. This MOD does not swap or refine automatically."));return b.ToString();
        }
        void ShowMini()
        {
            bool show = _active != null && _snapshot != null && !_visible;
            if(!show){if(_mini!=null)_mini.SetActive(false);return;}
            if(_mini==null){_mini=_ui.Panel("GuideHint",new Vector2(1f,1f),new Vector2(1f,1f),new Vector2(-12f,-95f),new Vector2(390f,240f),new Color(.055f,.095f,.11f,.94f),true);if(_mini==null)return;
                _ui.TextButton(_mini.transform,"full",L("本局攻略 / 详情", "Current guide / details"),new Vector2(8f,-8f),new Vector2(272f,34f),OpenCurrent);
                _ui.TextButton(_mini.transform,"collapse",L("收起/展开", "Collapse / expand"),new Vector2(284f,-8f),new Vector2(98f,34f),Collapse);
                _miniText=_ui.Label(_mini.transform,"hint","",new Vector2(12f,-50f),new Vector2(365f,180f),18f);_miniText.richText=false;}
            _mini.SetActive(true);_miniText.gameObject.SetActive(_miniOpen);var rt=_mini.transform as RectTransform;rt.sizeDelta=new Vector2(390f,_miniOpen?240f:50f);
            string advice=Advice();string[] lines=advice.Split((char)10);var b=new StringBuilder();for(int i=0;i<lines.Length&&i<5;i++)b.Append(lines[i]).Append((char)10);_miniText.text=b.ToString();
        }
        void Collapse(){_miniOpen=!_miniOpen;}
        void SaveLock()
        {
            if (_active == null) return;
            Context.Data.Set("activeGuide", _active.Raw); Context.Data.Set("activeSession", _session);
        }
        void MarkCards()
        {
            for (int i = _marks.Count - 1; i >= 0; i--)
            { if (_marks[i].Item == null || _marks[i].Label == null) { _marks.RemoveAt(i); continue; } _marks[i].Label.gameObject.SetActive(false); }
            if (_active == null || _snapshot == null || _active.GameVersion != Context.Versions.Game) return;
            string error; StageRule stage = GuideRules.Select(_active, _snapshot, out error); if (stage == null) return;
            int[] assigned = GuideRules.Assign(stage, _snapshot.Cards); bool[] used = new bool[_snapshot.Cards.Count];
            for (int i = 0; i < assigned.Length; i++) if (assigned[i] >= 0) { used[assigned[i]] = true; Mark(_snapshot.Cards[assigned[i]], L("目标牌", "Target")); }
            for (int r = 0; r < stage.Keep.Count; r++)
            {
                CardRule rule = stage.Keep[r]; int left = rule.Count;
                for (int i = 0; i < _snapshot.Cards.Count && left > 0; i++) if (!used[i] && GuideRules.Match(rule, _snapshot.Cards[i]))
                { used[i] = true; left--; Mark(_snapshot.Cards[i], L("额外保留", "Keep")); }
            }
        }
        void Mark(HeldCard card, string text)
        {
            CardItem item = card.View as CardItem; if (item == null) return; CardMark mark = null;
            for (int i = 0; i < _marks.Count; i++) if (_marks[i].Item == item) mark = _marks[i];
            if (mark == null) { mark = new CardMark(); mark.Item = item;
                mark.Label = _ui.Label(item.transform, "GuideCardHint", "", new Vector2(8f,-32f), new Vector2(130f,26f), 17f);
                mark.Label.richText = false; mark.Label.color = new Color(.75f,1f,.55f,1f); _marks.Add(mark); }
            mark.Label.text = text; mark.Label.gameObject.SetActive(true);
        }
        void OpenCurrent(){_visible=true;Current();}
        void Label(string text,float x,float y,float width,float height,float size){TextMeshProUGUI label=_ui.Label(_panel.transform,"label",text,new Vector2(x,-y),new Vector2(width,height),size);label.richText=false;}
        void Button(string text,float x,float y,float width,Action action){UiButton button=_ui.TextButton(_panel.transform,"button",text,new Vector2(x,-y),new Vector2(width,42f),action);TextMeshProUGUI label=button.GameObject.GetComponentInChildren<TextMeshProUGUI>();if(label!=null)label.richText=false;}
        public override void OnDisable(){_http.Cancel();for(int i=0;i<_marks.Count;i++)if(_marks[i].Label!=null)UnityEngine.Object.Destroy(_marks[i].Label.transform.parent.gameObject);_marks.Clear();if(_ui!=null)_ui.DestroyAll();if(_panelUi!=null)_panelUi.DestroyAll();}
    }
}
