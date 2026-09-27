using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Xunit;
using Yx.Shared;
using YxGuides;

public class GuideSubscriptionsTests
{
    static GuideBook Book(string id, string raw) { return new GuideBook { Id = id, Raw = raw }; }

    [Fact]
    public void FullCacheRejectsNewGuideWithoutChangingSavedGuides()
    {
        var current = new List<GuideBook>();
        for (int i = 0; i < 24; i++) current.Add(Book(i.ToString(CultureInfo.InvariantCulture), "saved"));
        List<object> next;
        Assert.False(GuideSubscriptions.TryPrepare(current, Book("new", "new guide"), out next));
        Assert.Null(next);
        Assert.Equal(24, current.Count);
        Assert.All(current, book => Assert.Equal("saved", book.Raw));
    }

    [Fact]
    public void FullCacheCanReplaceAnExistingVersion()
    {
        var current = new List<GuideBook>();
        for (int i = 0; i < 24; i++) current.Add(Book(i.ToString(CultureInfo.InvariantCulture), "saved"));
        List<object> next;
        Assert.True(GuideSubscriptions.TryPrepare(current, Book("10", "updated"), out next));
        Assert.Equal(24, next.Count);
        Assert.Equal("updated", next[10]);
        Assert.Equal("saved", current[10].Raw);
    }

    [Fact]
    public void ByteLimitIncludesUtf8AndSerializedCacheOverhead()
    {
        string text = new string('牌', 17000);
        var current = new List<GuideBook> { Book("a", text), Book("b", text), Book("c", text) };
        var candidate = new List<object> { text, text, text, "" };
        int used = Encoding.UTF8.GetByteCount(Json.Serialize(candidate));
        string exact = new string('x', 150 * 1024 - used);
        List<object> next;
        Assert.True(GuideSubscriptions.TryPrepare(current, Book("d", exact), out next));
        Assert.Equal(150 * 1024, Encoding.UTF8.GetByteCount(Json.Serialize(next)));
        Assert.False(GuideSubscriptions.TryPrepare(current, Book("d", exact + "x"), out next));
        Assert.Null(next);
        Assert.Equal(3, current.Count);
    }

    [Fact]
    public void OversizedReplacementKeepsTheOldVersion()
    {
        string text = new string('牌', 17000);
        var current = new List<GuideBook> { Book("a", text), Book("b", text), Book("c", text) };
        List<object> next;
        Assert.False(GuideSubscriptions.TryPrepare(current, Book("b", text + new string('牌', 500)), out next));
        Assert.Null(next);
        Assert.Equal(text, current[1].Raw);
    }
}
