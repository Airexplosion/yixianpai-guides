using System.Collections.Generic;
using Xunit;
using YxGuides;

public class GuideRulesTests
{
    static CardRule Rule(string name, int level = 1, int count = 1) { return new CardRule { Name = name, Level = level, Count = count }; }
    static HeldCard Card(string name, int level = 1) { return new HeldCard { Name = name, Level = level }; }
    static StageRule Stage() { return new StageRule { Id = "qi", Name = "炼气", RealmMin = 1, RealmMax = 2, RoundMin = 1, RoundMax = 9, PerRound = 2, PerStage = 4, Reserve = 6 }; }
    static SlotRule Slot(params CardRule[] rules) { var s = new SlotRule(); s.Options.AddRange(rules); return s; }
    [Fact] public void SpecificStageWinsAndTiesAreNotGuessed()
    {
        var book = new GuideBook { Hero = "全部", Career = "全部" }; var first = Stage(); var second = Stage(); second.Id = "b"; second.Priority = 5;
        book.Stages.Add(first); book.Stages.Add(second); var snap = new GuideSnapshot { Realm = 1, Round = 3 }; string error;
        Assert.Same(second, GuideRules.Select(book, snap, out error));
        first.Priority = 5; Assert.Null(GuideRules.Select(book, snap, out error)); Assert.Contains("优先级", error);
    }
    [Fact] public void RealmRoundHeroCareerAndRequiredCardAllGateStage()
    {
        var book = new GuideBook { Hero = "田屠馗", Career = "灵植师" }; var s = Stage(); s.Requires.Add(Rule("锻拳", 2, 2)); book.Stages.Add(s);
        var snap = new GuideSnapshot { Hero = "田屠馗", Career = "灵植师", Realm = 1, Round = 1 }; string error;
        snap.Cards.Add(Card("锻拳", 2)); Assert.Null(GuideRules.Select(book,snap,out error));
        snap.Cards.Add(Card("锻拳", 3)); Assert.Same(s,GuideRules.Select(book,snap,out error));
        snap.Round = 10; Assert.Null(GuideRules.Select(book,snap,out error)); snap.Round = 1;
        snap.Realm = 3; Assert.Null(GuideRules.Select(book,snap,out error)); snap.Realm = 1;
        snap.Hero = "其他"; Assert.Null(GuideRules.Select(book,snap,out error)); snap.Hero = "田屠馗";
        snap.Career = "其他"; Assert.Null(GuideRules.Select(book,snap,out error));
    }
    [Fact] public void OnePhysicalCardCannotFillTwoSlots()
    {
        var s = Stage(); s.Target.Add(Slot(Rule("A"))); s.Target.Add(Slot(Rule("A")));
        var cards = new List<HeldCard> { Card("A") }; Assert.False(GuideRules.Ready(s,cards));
        cards.Add(Card("A")); Assert.True(GuideRules.Ready(s,cards));
    }
    [Fact] public void AlternativeMatchingReservesUniqueCardForRestrictedSlot()
    {
        var s=Stage();s.Target.Add(Slot(Rule("A"),Rule("B")));s.Target.Add(Slot(Rule("A")));
        var cards=new List<HeldCard>{Card("A"),Card("B")};var assigned=GuideRules.Assign(s,cards);
        Assert.Equal(1,assigned[0]);Assert.Equal(0,assigned[1]);Assert.True(GuideRules.Ready(s,cards));
    }
    [Fact] public void EmptySlotsDoNotConsumeCardsAndEmptyTargetDoesNotStopSwapping()
    {
        var s=Stage();s.Target.Add(Slot());var cards=new List<HeldCard>();Assert.False(GuideRules.Ready(s,cards));
        s.Target.Add(Slot(Rule("A")));cards.Add(Card("A"));Assert.True(GuideRules.Ready(s,cards));
    }
    [Fact] public void LevelAndBulletNormalizationAreExplicit()
    {
        Assert.True(GuideRules.Match(Rule("崩拳·寸劲",2),Card("崩拳•寸劲",3)));
        Assert.False(GuideRules.Match(Rule("崩拳·寸劲",2),Card("崩拳•寸劲",1)));
        Assert.False(GuideRules.Match(Rule("崩拳"),Card("崩拳·寸劲")));
    }
    [Theory]
    [InlineData(10,0,0,false,2)] [InlineData(10,1,0,false,1)] [InlineData(7,0,0,false,1)]
    [InlineData(6,0,0,false,0)] [InlineData(20,0,4,false,0)] [InlineData(20,2,2,false,0)]
    public void BudgetsRespectAllThreeLimits(int remaining,int round,int stage,bool ready,int expected)
    { Assert.Equal(expected,GuideRules.Budget(Stage(),remaining,round,stage,ready)); }
    [Fact] public void ZeroUnlimitedAndStopConditionAreDifferent()
    {
        var s=Stage();s.PerRound=0;Assert.Equal(0,GuideRules.Budget(s,30,0,0,false));s.PerRound=-1;s.PerStage=-1;
        Assert.Equal(24,GuideRules.Budget(s,30,9,90,false));s.Stop=true;Assert.Equal(0,GuideRules.Budget(s,30,0,0,true));
    }
    [Fact] public void VersionedGuideParserPreservesNullBudgetAndEmptySlot()
    {
        string raw="{\"id\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"version\":1,\"author\":\"作者\",\"guide\":{\"schemaVersion\":1,\"title\":\"测试\",\"stages\":[{\"id\":\"a\",\"swap\":{\"perRound\":0,\"perStage\":null,\"reserve\":2},\"target\":[{\"options\":[]}]}]}}";
        var b=GuideBook.Read(raw);Assert.Equal(0,b.Stages[0].PerRound);Assert.Equal(-1,b.Stages[0].PerStage);Assert.Empty(b.Stages[0].Target[0].Options);
        Assert.Throws<System.FormatException>(()=>GuideBook.Read(raw.Replace("schemaVersion\":1","schemaVersion\":2")));
    }
}
