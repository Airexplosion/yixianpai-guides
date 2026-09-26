using System;
using System.Collections.Generic;
using Proto;
using Yx.ModSdk;

namespace YxGuides
{
    /// <summary>把发布攻略投影成游戏自己的秘籍详情数据，只读打开原生详情面板。</summary>
    internal static class NativeGuideView
    {
        const int LocalShortId = -20260926;

        public static bool Show(GuideBook book, ModContext context)
        {
            if (book == null) return false;
            try
            {
                LobbyPanel lobby = ILRPanelBase.FindILRPanel<LobbyPanel>();
                if (lobby == null) return false;
                EsotericMainPanel main = lobby.FindILRSubPanelRuntime<EsotericMainPanel>();
                if (main == null) return false;
                EsotericDetailPanel panel = lobby.FindILRSubPanelRuntime<EsotericDetailPanel>(main.subPanelContainer);
                if (panel == null) return false;
                panel.Show(Build(book, context));
                return true;
            }
            catch (Exception error)
            {
                context.Log.Warn("原生攻略详情打开失败：" + error.Message);
                return false;
            }
        }

        static EsotericData Build(GuideBook book, ModContext context)
        {
            var data = new EsotericData();
            data.shortId = LocalShortId;
            data.complete = false;
            data.isPrivate = true;
            data.title = book.Title;
            data.remark = book.Summary;
            data.authorName = book.Author;
            data.version = context.Versions.Game;
            data.charId = FindCharacter(book.Hero);
            if (data.charId != 0) data.sect = ConfigManager.GetCharacterConfig(data.charId).sect;
            data.career = FindCareer(book.Career);
            for (int i = 0; i < book.TalentCount; i++) data.talents.Add(book.Talents[i] == 0 ? -999999 : book.Talents[i]);
            data.unitDatas.Clear();
            for (int i = 0; i < book.Stages.Count; i++)
            {
                StageRule stage = book.Stages[i];
                var unit = new EsotericUnitData();
                unit.level = StageLevel(stage.RealmMin);
                unit.remark = stage.Name + (char)10 + stage.Note;
                for (int slot = 0; slot < stage.Target.Count && slot < 8; slot++)
                {
                    if (stage.Target[slot].Options.Count == 0) { unit.cards.Add(0); continue; }
                    unit.cards.Add(FindCard(stage.Target[slot].Options[0]));
                }
                while (unit.cards.Count < 8) unit.cards.Add(0);
                data.unitDatas.Add(unit);
            }
            data.unitCount = data.unitDatas.Count;
            if (data.unitDatas.Count > 0) data.cover = data.unitDatas[0];
            return data;
        }

        static int FindCharacter(string name)
        {
            if (string.IsNullOrEmpty(name) || name == "全部") return 0;
            for (int i = 0; i < ConfigManager.characterConfigs.Count; i++)
                if (ConfigManager.characterConfigs[i].name == name) return ConfigManager.characterConfigs[i].id;
            return 0;
        }

        static Career FindCareer(string name)
        {
            if (name == "炼丹师") return Career.LianDanShi;
            if (name == "符咒师") return Career.FuZhouShi;
            if (name == "琴师") return Career.QinShi;
            if (name == "画师") return Career.HuaShi;
            if (name == "阵法师") return Career.ZhenFaShi;
            if (name == "灵植师") return Career.LingZhiShi;
            if (name == "命理师") return Career.MingLiShi;
            return Career.InvalidCareer;
        }

        static Level StageLevel(int realm)
        {
            if (realm <= 1) return Level.LianQi;
            if (realm == 2) return Level.ZhuJi;
            if (realm == 3) return Level.JinDan;
            if (realm == 4) return Level.YuanYing;
            return Level.HuaShen;
        }

        static int FindCard(CardRule rule)
        {
            string wanted = Normalize(rule.Name);
            List<CardConfig> cards = ConfigManager.GetCardConfigs();
            for (int i = 0; i < cards.Count; i++)
            {
                CardConfig card = cards[i];
                if (Normalize(card.name) == wanted && card.rarity == rule.Level - 1) return card.id;
            }
            for (int i = 0; i < cards.Count; i++) if (Normalize(cards[i].name) == wanted) return cards[i].id;
            return 0;
        }

        static string Normalize(string value) { return (value ?? "").Trim().Replace("•", "·"); }
    }
}
