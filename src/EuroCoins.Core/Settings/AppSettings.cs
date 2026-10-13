namespace EuroCoins.Core.Settings;

/// <summary>
/// The app theme selected by the user.
/// </summary>
public enum AppTheme
{
    /// <summary>Follow the Windows setting.</summary>
    System,

    /// <summary>Always light.</summary>
    Light,

    /// <summary>Always dark.</summary>
    Dark,
}

/// <summary>
/// The position and size of the main window (per device).
/// </summary>
/// <param name="X">The left edge in screen pixels.</param>
/// <param name="Y">The top edge in screen pixels.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="IsMaximized">Whether the window was maximized.</param>
public sealed record WindowPlacement(int X, int Y, int Width, int Height, bool IsMaximized);

/// <summary>
/// The per-device app settings.
/// </summary>
public sealed record AppSettings
{
    /// <summary>The smallest tile zoom factor.</summary>
    public const double MinTileZoom = 0.6;

    /// <summary>The largest tile zoom factor.</summary>
    public const double MaxTileZoom = 2.0;

    /// <summary>Gets the selected theme.</summary>
    public AppTheme Theme { get; init; } = AppTheme.System;

    /// <summary>Gets the UI language (<c>de</c> or <c>en</c>), or <see langword="null"/> to follow Windows.</summary>
    public string? Language { get; init; }

    /// <summary>Gets the path of the collection file, or <see langword="null"/> for the default location.</summary>
    public string? CollectionPath { get; init; }

    /// <summary>Gets the last window placement, or <see langword="null"/> on first start.</summary>
    public WindowPlacement? Window { get; init; }

    /// <summary>Gets the zoom factor of the coin tiles (1 = default size).</summary>
    public double TileZoom { get; init; } = 1.0;
}
