using FlashLoop.App.Tests.Support;

namespace FlashLoop.App.Tests;

/// <summary>驗收 #6（重來重排）、#8（鍵盤完整操作）、#11（撤銷還原資料庫與統計）的端到端流程。</summary>
public class KeyboardReviewFlowTests
{
    private static void SeedBasicDeck(AppHarness app)
    {
        var deck = app.Services.Decks.Create("N2 基礎");
        app.Services.Import.Import(deck.Id, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "n2_basic.csv")));
    }

    [Fact]
    public void Enter_starts_review_space_flips_and_number_keys_rate_only_after_flipping() => Sta.Run(app =>
    {
        SeedBasicDeck(app);
        app.StartReview("N2 基礎");
        var review = app.Review;
        var front = AppHarness.Field<Label>(review, "_front");
        var back = AppHarness.Field<Label>(review, "_back");

        Assert.True(review.InProgress);
        Assert.Equal("曖昧", front.Text);
        Assert.Equal("", back.Text);

        app.Press(review, Keys.D3);
        Assert.Equal(0, app.Session.Summary.TotalRatings);

        app.Press(review, Keys.Space);
        Assert.Equal("あいまい／模糊不清", back.Text);

        app.Press(review, Keys.D3);
        Assert.Equal(1, app.Session.Summary.TotalRatings);
        Assert.Equal("", back.Text);
        Assert.NotEqual("曖昧", front.Text);
    });

    [Fact]
    public void Again_card_returns_at_the_end_and_leaves_after_good() => Sta.Run(app =>
    {
        SeedBasicDeck(app);
        app.StartReview("N2 基礎");
        var review = app.Review;
        var front = AppHarness.Field<Label>(review, "_front");
        var againCard = app.Session.Current!;

        app.Press(review, Keys.Space);
        app.Press(review, Keys.D1);
        var shown = new List<string> { againCard.Front };
        for (var guard = 0; review.InProgress && guard < 100; guard++)
        {
            shown.Add(front.Text);
            app.Press(review, Keys.Space);
            app.Press(review, Keys.D3);
        }

        Assert.False(review.InProgress);
        Assert.Equal(21, shown.Count);
        Assert.Equal([0, 20], shown.Select((f, i) => (f, i)).Where(x => x.f == againCard.Front).Select(x => x.i));
        Assert.All(shown.Zip(shown.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
        Assert.Equal(20, app.Session.Summary.DistinctCards); // 每日新卡上限 20
        Assert.StartsWith("ease=2.3 reps=1 interval=1 ", app.CardRow(againCard.Id));

        Assert.Contains("完成", AppHarness.Field<Label>(review, "_summaryTitle").Text);
        var body = AppHarness.Field<Label>(review, "_summaryBody").Text;
        Assert.Contains("複習卡片　20 張（評分 21 次）", body);
        Assert.Contains("重來 1", body);
    });

    [Fact]
    public void Ctrl_Z_restores_the_database_row_and_today_statistics() => Sta.Run(app =>
    {
        SeedBasicDeck(app);
        app.StartReview("N2 基礎");
        var review = app.Review;
        for (var i = 0; i < 3; i++)
        {
            app.Press(review, Keys.Space);
            app.Press(review, Keys.D3);
        }

        var card = app.Session.Current!;
        var rowBefore = app.CardRow(card.Id);
        var statsBefore = app.Services.Stats.Get();
        app.Press(review, Keys.Space);
        app.Press(review, Keys.D4);
        Assert.NotEqual(rowBefore, app.CardRow(card.Id));
        Assert.Equal(statsBefore.RatingsToday + 1, app.Services.Stats.Get().RatingsToday);

        app.Press(review, Keys.Control | Keys.Z);

        Assert.Equal(rowBefore, app.CardRow(card.Id));
        var statsAfter = app.Services.Stats.Get();
        Assert.Equal(statsBefore.CardsReviewedToday, statsAfter.CardsReviewedToday);
        Assert.Equal(statsBefore.RatingsToday, statsAfter.RatingsToday);
        Assert.Equal(card.Id, app.Session.Current!.Id);
        Assert.Equal("", AppHarness.Field<Label>(review, "_back").Text);
    });
}
