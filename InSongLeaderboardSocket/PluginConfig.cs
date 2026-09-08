using System;
using IPA.Config.Stores;
using UnityEngine;

namespace InSongLeaderboardSocket;

public class PluginConfig
{
    internal static PluginConfig? Instance { get; set; }

    public virtual int Rows { get; set; } = 10;
    public virtual bool ShowImages { get; set; }
    public virtual bool ShowStats { get; set; }
    public virtual string Source { get; set; } = "BeatLeader";
    public virtual bool ShowWhenIdle { get; set; } = true;
    public virtual int FontSizeBase { get; set; } = 25;
    public virtual int BackgroundOpacity { get; set; } = 100;
    public virtual Color PlayerColor { get; set; } = new Color(0.4f, 0.8f, 1f);
    public virtual Color PbColor { get; set; } = new Color(1f, 0.8f, 0.2f);
    public virtual Color TextColor { get; set; } = new Color(1f, 1f, 1f);
    public virtual string Alignment { get; set; } = "left";
    public virtual int FadeDepth { get; set; } = 5;
    public virtual bool RankByAccuracy { get; set; }
    public virtual bool ClickedDisclaimer { get; set; }

    public event Action? ConfigChanged;

    public virtual void Changed()
    {
        ConfigChanged?.Invoke();
    }
}
