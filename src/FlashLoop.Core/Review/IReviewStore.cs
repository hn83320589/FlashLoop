using FlashLoop.Core.Model;

namespace FlashLoop.Core.Review;

/// <summary>複習過程需要的持久化動作。實作必須保證每個方法為單一 transaction。</summary>
public interface IReviewStore
{
    /// <summary>更新卡片排程並新增評分紀錄，回傳帶有資料庫 Id 的紀錄。</summary>
    ReviewLog SaveReview(Card updated, ReviewLog log);

    /// <summary>將卡片還原成評分前的狀態並刪除該筆評分紀錄。</summary>
    void UndoReview(Card previous, long logId);
}
