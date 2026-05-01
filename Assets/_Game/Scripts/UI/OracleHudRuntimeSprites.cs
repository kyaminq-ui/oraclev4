using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Assigne les sprites HUD (PA / PM / timer depuis UI_Maj) sur des <see cref="Image"/> si l’Inspector est vide.
/// Utilise <c>Assets/_Game/Resources/OracleHUD/*.gif</c> (mêmes fichiers que UI_Maj, copiés pour Resources.Load).
/// </summary>
public static class OracleHudRuntimeSprites
{
    const string ResourcesSubfolder = "OracleHUD/";

    public static Sprite LoadManaIcon() =>
        Resources.Load<Sprite>(ResourcesSubfolder + "mana_icon_hud");

    public static Sprite LoadMovementIcon() =>
        Resources.Load<Sprite>(ResourcesSubfolder + "mouvement_icon_hud");

    public static Sprite LoadTimerIcon() =>
        Resources.Load<Sprite>(ResourcesSubfolder + "timer_icon_hud");

    public static void ApplyCombatHudIfMissing(Image paIcon, Image pmIcon, TimerUI timer)
    {
        if (paIcon != null && paIcon.sprite == null)
        {
            var s = LoadManaIcon();
            if (s != null) paIcon.sprite = s;
        }
        if (pmIcon != null && pmIcon.sprite == null)
        {
            var s = LoadMovementIcon();
            if (s != null) pmIcon.sprite = s;
        }
        if (timer != null && timer.timerIconImage != null && timer.timerIconImage.sprite == null)
        {
            var s = LoadTimerIcon();
            if (s != null) timer.timerIconImage.sprite = s;
        }
    }

    public static void ApplyPassiveTimerIfMissing(Image timerIcon)
    {
        if (timerIcon == null || timerIcon.sprite != null) return;
        var s = LoadTimerIcon();
        if (s != null) timerIcon.sprite = s;
    }
}
