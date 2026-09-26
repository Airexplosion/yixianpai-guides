using System.Collections.Generic;
using System.Globalization;
using Proto;

namespace YxGuides
{
    internal static class GameReader
    {
        public static GuideSnapshot Read()
        {
            BattleManager bm = BattleManager.Instance;
            GameStatus gs = bm == null ? null : bm.currentGameStatus;
            if (gs == null || gs.isEnded || SceneLoader.currentSceneName != "Battle" || gs.playerPrivateData == null
                || bm.currentScene != SceneType.修炼阶段 || bm.isSpectating || bm.isRealtimeSpectating || bm.replaying) return null;
            BattlePlayerData me = GameStatusExtension.GetMainPlayerData(gs);
            if (me == null) return null;
            BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
            CardPanel cp = bp == null ? null : bp.FindILRSubPanel<CardPanel>();
            if (cp == null) return null;
            var s = new GuideSnapshot();
            s.Session = gs.codeId.ToString(CultureInfo.InvariantCulture) + ":" + me.uid;
            s.Realm = (int)me.level; s.Round = gs.round; s.Remaining = gs.playerPrivateData.replaceCardChance;
            s.Hero = TranslateUtil.GetCharacterNameTranslate(me.characterId); s.Career = TranslateUtil.GetCareerTranslate(me.career);
            List<CardItem> hand = cp.GetHandCards();
            if (hand != null) for (int i = 0; i < hand.Count; i++) Add(s, hand[i]);
            List<CardGrid> grids = cp.GetCardGrids();
            if (grids != null) for (int i = 0; i < grids.Count; i++) if (grids[i] != null && grids[i].unlocked) Add(s, grids[i].GetCard());
            // 拖动过程中不提供缺牌结论。
            if (CardItem.draggingCard != null) return null;
            return s;
        }
        static void Add(GuideSnapshot s, CardItem item)
        {
            if (item == null || item.cardInfo == null || item.cardConfig == null) return;
            var card = new HeldCard(); card.Name = item.cardConfig.name; card.Level = item.cardConfig.rarity + 1; card.View = item;
            s.Cards.Add(card);
        }
    }
}
