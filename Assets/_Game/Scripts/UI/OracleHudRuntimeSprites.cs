using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Assigne les sprites HUD (PA / PM / timer depuis UI_Maj) sur des <see cref="Image"/> si l’Inspector est vide.
/// Utilise <c>Assets/_Game/Resources/OracleHUD/*.gif</c> (mêmes fichiers que UI_Maj, copiés pour Resources.Load).
/// </summary>
public static class OracleHudRuntimeSprites
{
    const string ResourcesSubfolder = "OracleHUD/";

    /// <summary>
    /// Resources.Load&lt;Sprite&gt; peut échouer selon l’import ; on retombe sur Texture2D + Sprite.Create
    /// ou sur le premier sous-asset Sprite.
    /// </summary>
    public static Sprite LoadSpriteFlexible(params string[] resourceNamesWithoutSubfolder)
    {
        foreach (var name in resourceNamesWithoutSubfolder)
        {
            if (string.IsNullOrEmpty(name)) continue;
            string path = ResourcesSubfolder + name;

            var s = Resources.Load<Sprite>(path);
            if (s != null && s.texture != null)
                return s;

            var fromAll = Resources.LoadAll<Sprite>(path);
            if (fromAll != null && fromAll.Length > 0)
                return fromAll[0];

            var tex = Resources.Load<Texture2D>(path);
            if (tex != null && tex.width > 0)
            {
                var created = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                created.name = name;
                return created;
            }

            var allObjs = Resources.LoadAll<Texture2D>(path);
            if (allObjs != null && allObjs.Length > 0 && allObjs[0].width > 0)
            {
                var t0 = allObjs[0];
                return Sprite.Create(t0,
                    new Rect(0, 0, t0.width, t0.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
            }
        }

        return null;
    }

    public static Sprite LoadManaIcon() =>
        LoadSpriteFlexible("pa_icon_hud", "mana_icon_hud");

    public static Sprite LoadMovementIcon() =>
        LoadSpriteFlexible("pm_icon_hud", "mouvement_icon_hud");

    public static Sprite LoadTimerIcon() =>
        LoadSpriteFlexible("timer_icon_hud");

    public static void ApplyCombatHudIfMissing(Image paIcon, Image pmIcon, TimerUI timer)
    {
        if (paIcon != null)
        {
            var loaded = LoadManaIcon();
            if (loaded != null)
                paIcon.sprite = loaded;
            if (paIcon.sprite == null || paIcon.sprite.texture == null)
                paIcon.sprite = CreateTintSprite(new Color(0.35f, 0.55f, 1f, 1f));
            paIcon.preserveAspect = true;
            paIcon.color = Color.white;
            paIcon.enabled = paIcon.sprite != null;
        }
        if (pmIcon != null)
        {
            var loaded = LoadMovementIcon();
            if (loaded != null)
                pmIcon.sprite = loaded;
            if (pmIcon.sprite == null || pmIcon.sprite.texture == null)
                pmIcon.sprite = CreateTintSprite(new Color(0.45f, 0.9f, 0.55f, 1f));
            pmIcon.preserveAspect = true;
            pmIcon.color = Color.white;
            pmIcon.enabled = pmIcon.sprite != null;
        }
        if (timer != null && timer.timerIconImage != null)
        {
            var loadedT = LoadTimerIcon();
            if (loadedT != null)
                timer.timerIconImage.sprite = loadedT;
            timer.timerIconImage.preserveAspect = true;
        }
    }

    static Sprite CreateTintSprite(Color color)
    {
        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        var px = new Color[32 * 32];
        for (int i = 0; i < px.Length; i++)
            px[i] = color;
        tex.SetPixels(px);
        tex.Apply(false, true);
        tex.hideFlags = HideFlags.HideAndDontSave;
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }

    public static void ApplyPassiveTimerIfMissing(Image timerIcon)
    {
        if (timerIcon == null || timerIcon.sprite != null) return;
        var s = LoadTimerIcon();
        if (s != null) timerIcon.sprite = s;
    }
}
