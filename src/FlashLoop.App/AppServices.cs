using FlashLoop.Core.Backup;
using FlashLoop.Core.Persistence;
using FlashLoop.Core.Services;

namespace FlashLoop.App;

/// <summary>UI 需要的所有應用服務，於 Program.cs 組裝。</summary>
public sealed record AppServices(
    DeckService Decks,
    ImportService Import,
    ReviewService Review,
    StatsService Stats,
    BackupService Backup,
    ISettingsStore Settings,
    TimeProvider Time);
