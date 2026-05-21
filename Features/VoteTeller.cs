using System;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;
using SkiaSharp;

namespace CS2GameHelper.Features;

/// <summary>
/// v2.0: detects active in-game vote and renders Yes/No counts on the overlay.
/// Port of FullyExternalCS2 v2.0 logic (sweeperxz). Reads the CCSPlayerResource-side
/// vote_controller entity from the entity list and pulls active issue/team/yes/no.
/// All memory reads are wrapped in try/catch to avoid destabilising the overlay.
/// </summary>
internal class VoteTeller : ThreadedServiceBase
{
    private const int EntityListEntryOffset = 16;
    private const int EntityListStride = 112;
    private const int VoteControllerStartIndex = 64;
    private const int VoteControllerMaxIndex = 8192;

    // Vote controller field offsets observed in CS2 (upstream v2.0).
    // Not exported by cs2-dumper as named fields, so kept as constants and overridable later.
    private const int OffActiveIssue = 1552;
    private const int OffVotingTeam = 1556;
    private const int OffYesVotes = 1560;
    private const int OffNoVotes = 1564;

    private readonly GameProcess _gameProcess;

    private static bool _isVoting;
    private static int _votingTeam;
    private static int _yesVotes;
    private static int _noVotes;
    private static int _activeIssue;

    public VoteTeller(GameProcess gameProcess)
    {
        _gameProcess = gameProcess ?? throw new ArgumentNullException(nameof(gameProcess));
    }

    protected override string ThreadName => nameof(VoteTeller);

    protected override void FrameAction()
    {
        try
        {
            if (_gameProcess.ModuleClient == null || _gameProcess.Process == null || !_gameProcess.IsValid)
            {
                ResetVote();
                return;
            }

            var entityList = _gameProcess.ModuleClient.Read<IntPtr>(Offsets.dwEntityList);
            if (entityList == IntPtr.Zero)
            {
                ResetVote();
                return;
            }

            var voteController = FindVoteController(entityList);
            if (voteController == IntPtr.Zero)
            {
                ResetVote();
                return;
            }

            _activeIssue = SafeRead(() => _gameProcess.Read<int>(voteController + OffActiveIssue), -1);
            _votingTeam = SafeRead(() => _gameProcess.Read<int>(voteController + OffVotingTeam), 0);
            _yesVotes = SafeRead(() => _gameProcess.Read<int>(voteController + OffYesVotes), 0);
            _noVotes = SafeRead(() => _gameProcess.Read<int>(voteController + OffNoVotes), 0);
            _isVoting = _activeIssue > 0;
        }
        catch
        {
            ResetVote();
        }
    }

    private IntPtr FindVoteController(IntPtr entityList)
    {
        if (_gameProcess.Process == null) return IntPtr.Zero;

        for (var i = VoteControllerStartIndex; i < VoteControllerMaxIndex; i++)
        {
            var listEntry = SafeRead(() => _gameProcess.Read<IntPtr>(entityList + 8 * (i >> 9) + EntityListEntryOffset), IntPtr.Zero);
            if (listEntry == IntPtr.Zero) continue;

            var entity = SafeRead(() => _gameProcess.Read<IntPtr>(listEntry + EntityListStride * (i & 0x1FF)), IntPtr.Zero);
            if (entity == IntPtr.Zero) continue;

            var entityIdentity = SafeRead(() => _gameProcess.Read<IntPtr>(entity + 0x10), IntPtr.Zero);
            if (entityIdentity == IntPtr.Zero) continue;

            var designerNamePtr = SafeRead(() => _gameProcess.Read<IntPtr>(entityIdentity + 0x20), IntPtr.Zero);
            if (designerNamePtr == IntPtr.Zero) continue;

            string? designerName = null;
            try { designerName = _gameProcess.ReadString(designerNamePtr, 64); } catch { /* ignore */ }
            if (designerName == "vote_controller") return entity;
        }

        return IntPtr.Zero;
    }

    private static T SafeRead<T>(Func<T> read, T fallback)
    {
        try { return read(); } catch { return fallback; }
    }

    private static void ResetVote()
    {
        _isVoting = false;
        _votingTeam = 0;
        _yesVotes = 0;
        _noVotes = 0;
        _activeIssue = 0;
    }

    public static void Draw(ModernGraphics graphics)
    {
        if (!_isVoting) return;

        var cfg = ConfigManager.Load();
        var vc = cfg?.VoteTeller ?? new ConfigManager.VoteTellerConfig();
        if (!vc.Enabled) return;

        string teamName = _votingTeam switch
        {
            2 => "TERRORISTS",
            3 => "COUNTER-TERRORISTS",
            _ => "ALL"
        };

        var hexColor = _votingTeam switch
        {
            2 => vc.ColorT,
            3 => vc.ColorCT,
            _ => vc.ColorAll
        };

        uint color = ParseHex(hexColor);

        float x = vc.X;
        float y = vc.Y;
        const float lineHeight = 18f;

        // Shadow for legibility
        graphics.DrawText($"Vote: {teamName}", x + 1, y + 1, 0xFF000000u, 14);
        graphics.DrawText($"Issue ID: {_activeIssue}", x + 1, y + 1 + lineHeight, 0xFF000000u, 14);
        graphics.DrawText($"Yes: {_yesVotes} | No: {_noVotes}", x + 1, y + 1 + lineHeight * 2, 0xFF000000u, 14);

        graphics.DrawText($"Vote: {teamName}", x, y, color, 14);
        graphics.DrawText($"Issue ID: {_activeIssue}", x, y + lineHeight, color, 14);
        graphics.DrawText($"Yes: {_yesVotes} | No: {_noVotes}", x, y + lineHeight * 2, color, 14);
    }

    private static uint ParseHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return 0xFFFFFFFFu;
        hex = hex.Trim();
        if (hex.StartsWith("#")) hex = hex.Substring(1);
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);
        if (hex.Length > 8) hex = hex.Substring(hex.Length - 8);
        try { return Convert.ToUInt32(hex, 16); }
        catch { return 0xFFFFFFFFu; }
    }
}
