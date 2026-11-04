using System;

// Shared hooks between the new-game setup screen and the endgame/legacy system.
public static class NewGameHooks
{
    // Raised once, right after a brand-new game's state and map are created (never for a loaded save).
    public static event Action<Settlement> Started;
    public static void RaiseStarted(Settlement settlement) => Started?.Invoke(settlement);

    // Something (e.g. a victory screen) asks for the new-game setup screen to open.
    public static event Action NewGameRequested;
    public static void RequestNewGame() => NewGameRequested?.Invoke();
}
