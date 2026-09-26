using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Yx.Shared;

namespace YxGuides
{
    public sealed class CardRule
    {
        public string Name, Reason;
        public int Level, Count;
    }
    public sealed class SlotRule { public readonly List<CardRule> Options = new List<CardRule>(); }
    public sealed class StageRule
    {
        public string Id, Name, Note;
        public int Priority, RealmMin, RealmMax, RoundMin, RoundMax;
        public int PerRound, PerStage, Reserve;
        public bool Stop;
        public readonly List<CardRule> Requires = new List<CardRule>();
        public CardRule IfHand;
        public string ElseOf;
        public readonly List<CardRule> Keep = new List<CardRule>();
        public readonly List<SlotRule> Target = new List<SlotRule>();
    }
    public sealed class GuideBook
    {
        public string Id, Title, Author, Summary, Hero, Career, GameVersion, Raw;
        public int Version;
        public readonly int[] Talents = new int[4];
        public int TalentCount;
        public readonly List<StageRule> Stages = new List<StageRule>();

        public static GuideBook Read(string raw)
        {
            if (raw == null || Encoding.UTF8.GetByteCount(raw) > 64 * 1024) throw new FormatException("攻略过大");
            Dictionary<string, object> root = Json.ParseObject(raw);
            Dictionary<string, object> g = Json.GetObject(root, "guide");
            if (g == null || Number(g, "schemaVersion", 0) != 1) throw new FormatException("不支持的攻略格式，请更新 MOD");
            var book = new GuideBook();
            book.Id = Text(root, "id"); book.Version = Number(root, "version", 0);
            if (book.Id.Length != 36 || book.Version < 1) throw new FormatException("攻略标识不正确");
            book.Author = Text(root, "author"); book.Title = Text(g, "title"); book.Summary = Text(g, "summary");
            book.Hero = Text(g, "hero"); book.Career = Text(g, "career"); book.GameVersion = Text(g, "gameVersion"); book.Raw = raw;
            List<object> talents = Json.GetArray(g, "talents");
            if (talents != null)
            {
                if (talents.Count > 4) throw new FormatException("仙命数量不正确");
                for (int i = 0; i < talents.Count; i++)
                {
                    int id = Convert.ToInt32(talents[i], CultureInfo.InvariantCulture);
                    if (id < 0) throw new FormatException("仙命标识不正确");
                    book.Talents[book.TalentCount++] = id;
                }
            }
            List<object> stages = Json.GetArray(g, "stages");
            if (stages == null || stages.Count < 1 || stages.Count > 24) throw new FormatException("阶段数量不正确");
            for (int i = 0; i < stages.Count; i++)
            {
                Dictionary<string, object> d = stages[i] as Dictionary<string, object>;
                if (d == null) throw new FormatException("阶段格式不正确");
                var s = new StageRule(); s.Id = Text(d, "id"); s.Name = Text(d, "name"); s.Note = Text(d, "note");
                s.Priority = Number(d, "priority", 0); s.RealmMin = Number(d, "realmMin", 1); s.RealmMax = Number(d, "realmMax", 5);
                s.RoundMin = Number(d, "roundMin", 1); s.RoundMax = Number(d, "roundMax", 999);
                Dictionary<string, object> swap = Json.GetObject(d, "swap");
                s.PerRound = Number(swap, "perRound", -1); s.PerStage = Number(swap, "perStage", -1); s.Reserve = Number(swap, "reserve", 0);
                s.Stop = Json.GetBool(d, "stopWhenTargetReady", false);
                ReadCards(Json.GetArray(d, "requires"), s.Requires); ReadCards(Json.GetArray(d, "keep"), s.Keep);
                Dictionary<string, object> hand = Json.GetObject(d, "ifHand");
                if (hand != null)
                {
                    s.IfHand = new CardRule { Name = Text(hand, "name"), Level = Number(hand, "level", 1), Count = Number(hand, "count", 1) };
                    if (s.IfHand.Name.Length == 0 || s.IfHand.Level < 1 || s.IfHand.Level > 3 || s.IfHand.Count < 1 || s.IfHand.Count > 8)
                        throw new FormatException("如果条件不正确");
                }
                s.ElseOf = Text(d, "elseOf");
                List<object> slots = Json.GetArray(d, "target");
                if (slots != null) for (int j = 0; j < slots.Count; j++)
                {
                    var slot = new SlotRule(); ReadCards(Json.GetArray(slots[j] as Dictionary<string, object>, "options"), slot.Options); s.Target.Add(slot);
                }
                if (s.Target.Count > 8 || s.Keep.Count > 40 || s.Requires.Count > 12 || s.RealmMin > s.RealmMax || s.RoundMin > s.RoundMax)
                    throw new FormatException("阶段规则不正确");
                for (int j = 0; j < book.Stages.Count; j++) if (book.Stages[j].Id == s.Id) throw new FormatException("阶段 ID 重复");
                book.Stages.Add(s);
            }
            for (int i = 0; i < book.Stages.Count; i++)
            {
                StageRule s = book.Stages[i];
                if (string.IsNullOrEmpty(s.ElseOf)) continue;
                StageRule parent = null;
                for (int j = 0; j < book.Stages.Count; j++) if (book.Stages[j].Id == s.ElseOf) parent = book.Stages[j];
                if (parent == null || parent == s || parent.IfHand == null || s.IfHand != null) throw new FormatException("否则阶段引用不正确");
            }
            return book;
        }
        static void ReadCards(List<object> rows, List<CardRule> result)
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Count; i++)
            {
                Dictionary<string, object> c = rows[i] as Dictionary<string, object>;
                var rule = new CardRule(); rule.Name = Text(c, "name"); rule.Level = Number(c, "level", 1); rule.Count = Number(c, "count", 1); rule.Reason = Text(c, "reason");
                if (rule.Name.Length == 0 || rule.Level < 1 || rule.Level > 3 || rule.Count < 1 || rule.Count > 8) throw new FormatException("卡牌规则不正确");
                result.Add(rule);
            }
        }
        public static string Text(Dictionary<string, object> d, string key) { return Json.GetString(d, key, ""); }
        public static int Number(Dictionary<string, object> d, string key, int fallback) { return (int)Json.GetNumber(d, key, fallback); }
        public static string Num(int n) { return n.ToString(CultureInfo.InvariantCulture); }
    }
    public sealed class HeldCard { public string Name; public int Level; public bool InHand; public object View; }
    public sealed class GuideSnapshot
    {
        public string Session, Hero, Career;
        public int Realm, Round, Remaining;
        public readonly List<HeldCard> Cards = new List<HeldCard>();
    }
    public static class GuideRules
    {
        public static string Normalize(string name) { return (name ?? "").Trim().Replace("•", "·"); }
        public static bool Match(CardRule rule, HeldCard card) { return Normalize(rule.Name) == Normalize(card.Name) && card.Level >= rule.Level; }
        public static int Count(CardRule rule, List<HeldCard> cards)
        { int n = 0; for (int i = 0; i < cards.Count; i++) if (Match(rule, cards[i])) n++; return n; }
        static int HandCount(CardRule rule, List<HeldCard> cards)
        { int n = 0; for (int i = 0; i < cards.Count; i++) if (cards[i].InHand && Match(rule, cards[i])) n++; return n; }
        public static StageRule Select(GuideBook book, GuideSnapshot state, out string error)
        {
            error = "";
            if (book.Hero != "全部" && Normalize(book.Hero) != Normalize(state.Hero)) { error = "此攻略不适用于当前角色"; return null; }
            if (book.Career != "全部" && Normalize(book.Career) != Normalize(state.Career)) { error = "此攻略不适用于当前副职"; return null; }
            StageRule best = null; bool conflict = false;
            for (int i = 0; i < book.Stages.Count; i++)
            {
                StageRule s = book.Stages[i];
                if (state.Realm < s.RealmMin || state.Realm > s.RealmMax || state.Round < s.RoundMin || state.Round > s.RoundMax) continue;
                if (s.IfHand != null && HandCount(s.IfHand, state.Cards) < s.IfHand.Count) continue;
                if (!string.IsNullOrEmpty(s.ElseOf))
                {
                    StageRule parent = null;
                    for (int p = 0; p < book.Stages.Count; p++) if (book.Stages[p].Id == s.ElseOf) parent = book.Stages[p];
                    if (parent == null || parent.IfHand == null || HandCount(parent.IfHand, state.Cards) >= parent.IfHand.Count) continue;
                }
                bool matched = true;
                for (int j = 0; j < s.Requires.Count; j++) if (Count(s.Requires[j], state.Cards) < s.Requires[j].Count) matched = false;
                if (!matched) continue;
                if (best == null || s.Priority > best.Priority) { best = s; conflict = false; }
                else if (s.Priority == best.Priority) conflict = true;
            }
            if (conflict) { error = "多个阶段优先级相同，请作者调整；暂不提供确定建议"; return null; }
            if (best == null) error = "攻略未覆盖当前阶段";
            return best;
        }
        // 用二分图增广匹配：替代牌与重复格不能重复使用同一张实体牌。
        public static int[] Assign(StageRule s, List<HeldCard> cards)
        {
            int[] owner = new int[cards.Count]; for (int i = 0; i < owner.Length; i++) owner[i] = -1;
            for (int i = 0; i < s.Target.Count; i++) Place(i, s, cards, owner, new bool[cards.Count]);
            int[] slots = new int[s.Target.Count]; for (int i = 0; i < slots.Length; i++) slots[i] = -1;
            for (int i = 0; i < owner.Length; i++) if (owner[i] >= 0) slots[owner[i]] = i;
            return slots;
        }
        static bool Place(int slot, StageRule s, List<HeldCard> cards, int[] owner, bool[] seen)
        {
            for (int o = 0; o < s.Target[slot].Options.Count; o++)
                for (int c = 0; c < cards.Count; c++)
                {
                    if (seen[c] || !Match(s.Target[slot].Options[o], cards[c])) continue;
                    seen[c] = true;
                    if (owner[c] < 0 || Place(owner[c], s, cards, owner, seen)) { owner[c] = slot; return true; }
                }
            return false;
        }
        public static bool Ready(StageRule s, List<HeldCard> cards)
        {
            int[] assigned = Assign(s, cards); bool any = false;
            for (int i = 0; i < assigned.Length; i++) if (s.Target[i].Options.Count > 0) { any = true; if (assigned[i] < 0) return false; }
            return any;
        }
        public static int Budget(StageRule s, int remaining, int roundUsed, int stageUsed, bool ready)
        {
            if (s.Stop && ready) return 0;
            int budget = Math.Max(0, remaining - s.Reserve);
            if (s.PerRound >= 0) budget = Math.Min(budget, Math.Max(0, s.PerRound - roundUsed));
            if (s.PerStage >= 0) budget = Math.Min(budget, Math.Max(0, s.PerStage - stageUsed));
            return budget;
        }
        public static string Describe(StageRule s)
        {
            var b = new StringBuilder(); b.Append(s.Name).Append(" · 境界 ").Append(GuideBook.Num(s.RealmMin)).Append("—").Append(GuideBook.Num(s.RealmMax));
            b.Append((char)10).Append("轮次 ").Append(GuideBook.Num(s.RoundMin)).Append("—").Append(GuideBook.Num(s.RoundMax)).Append(" · 优先级 ").Append(GuideBook.Num(s.Priority));
            for (int i = 0; i < s.Target.Count; i++) { b.Append((char)10).Append("第 ").Append(GuideBook.Num(i + 1)).Append(" 格：");
                if (s.Target[i].Options.Count == 0) b.Append("留空");
                for (int j = 0; j < s.Target[i].Options.Count; j++) { if (j > 0) b.Append(" / "); CardRule c = s.Target[i].Options[j]; b.Append(c.Name).Append(" ≥").Append(GuideBook.Num(c.Level)).Append("级"); } }
            for (int i = 0; i < s.Keep.Count; i++) { CardRule c = s.Keep[i]; b.Append((char)10).Append("保留 ").Append(c.Name).Append(" ≥").Append(GuideBook.Num(c.Level)).Append("级 ×").Append(GuideBook.Num(c.Count)).Append("：").Append(c.Reason); }
            b.Append((char)10).Append("换牌：每轮 ").Append(Limit(s.PerRound)).Append(" / 阶段 ").Append(Limit(s.PerStage)).Append(" / 保底 ").Append(GuideBook.Num(s.Reserve));
            if (s.Stop) b.Append((char)10).Append("目标凑齐后停止换牌");
            if (s.Requires.Count > 0) { b.Append((char)10).Append("进入条件："); for (int i = 0; i < s.Requires.Count; i++) { CardRule c = s.Requires[i]; b.Append(c.Name).Append(" ≥").Append(GuideBook.Num(c.Level)).Append("级 ×").Append(GuideBook.Num(c.Count)).Append("；"); } }
            b.Append((char)10).Append(s.Note); return b.ToString();
        }
        static string Limit(int n) { return n < 0 ? "不限" : GuideBook.Num(n); }
    }
}
