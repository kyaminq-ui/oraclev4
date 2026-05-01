#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Outil d'import automatique du Sprite-01 vers PlayerAnimator.
///
/// Fonctionnalités :
///   1. Extraction des frames GIF → PNG individuels (via System.Drawing)
///   2. Import Unity des PNG en tant que sprites
///   3. Assignation automatique aux 12 slots du PlayerAnimator
///   4. Marquage du prefab / scène dirty pour sauvegarde
///
/// Accès : menu Tools → Oracle → Setup Sprite-01 Player Animator
/// </summary>
public class Sprite01SetupTool : EditorWindow
{
    // ─────────────────────────────────────────────────────────────
    // CONSTANTES
    // ─────────────────────────────────────────────────────────────
    private const string DEFAULT_SPRITE_FOLDER  = "Assets/_Game/Sprites/Characters/Sprite-01";
    private const string FRAMES_SUBFOLDER       = "Frames";
    private const float  DEFAULT_FPS_IDLE       = 6f;
    private const float  DEFAULT_FPS_WALK       = 10f;
    private const float  DEFAULT_FPS_DEATH      = 8f;

    // ─────────────────────────────────────────────────────────────
    // ÉTAT DE LA FENÊTRE
    // ─────────────────────────────────────────────────────────────
    private PlayerAnimator target;
    private string spriteFolderPath = DEFAULT_SPRITE_FOLDER;
    private float fpsIdle  = DEFAULT_FPS_IDLE;
    private float fpsWalk  = DEFAULT_FPS_WALK;
    private float fpsDeath = DEFAULT_FPS_DEATH;
    private Vector2 scroll;

    private readonly List<string> log = new();

    // ─────────────────────────────────────────────────────────────
    // MENU
    // ─────────────────────────────────────────────────────────────
    [MenuItem("Tools/Oracle/Setup Sprite-01 Player Animator")]
    static void ShowWindow() =>
        GetWindow<Sprite01SetupTool>("Sprite-01 Setup").minSize = new Vector2(420, 540);

    // ─────────────────────────────────────────────────────────────
    // GUI
    // ─────────────────────────────────────────────────────────────
    void OnGUI()
    {
        EditorGUILayout.Space(6);
        GUILayout.Label("⚙  Sprite-01 → PlayerAnimator Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // ── Cible ─────────────────────────────────────────────
        EditorGUI.BeginChangeCheck();
        target = (PlayerAnimator)EditorGUILayout.ObjectField(
            "PlayerAnimator cible", target, typeof(PlayerAnimator), allowSceneObjects: true);
        if (target == null)
        {
            // Tentative de recherche automatique dans la scène
            if (GUILayout.Button("Trouver automatiquement dans la scène"))
                target = FindFirstObjectByType<PlayerAnimator>();
        }

        EditorGUILayout.Space(4);
        spriteFolderPath = EditorGUILayout.TextField("Dossier Sprite-01", spriteFolderPath);

        EditorGUILayout.Space(6);
        GUILayout.Label("Cadences (FPS)", EditorStyles.miniBoldLabel);
        fpsIdle  = EditorGUILayout.Slider("Idle",  fpsIdle,  1, 24);
        fpsWalk  = EditorGUILayout.Slider("Walk",  fpsWalk,  1, 24);
        fpsDeath = EditorGUILayout.Slider("Death", fpsDeath, 1, 24);

        EditorGUILayout.Space(8);

        // ── Boutons ───────────────────────────────────────────
        GUI.enabled = true;
        if (GUILayout.Button("① Extraire les frames GIF → PNG", GUILayout.Height(30)))
            ExtractAllGifFrames();

        EditorGUILayout.Space(4);

        GUI.enabled = target != null;
        if (GUILayout.Button("② Assigner les sprites au PlayerAnimator", GUILayout.Height(30)))
            AssignAllSprites();
        GUI.enabled = true;

        EditorGUILayout.Space(4);

        GUI.color = new Color(0.4f, 0.9f, 0.5f);
        if (GUILayout.Button("TOUT FAIRE EN UNE ÉTAPE  (① + ②)", GUILayout.Height(36)))
        {
            ExtractAllGifFrames();
            if (target != null) AssignAllSprites();
        }
        GUI.color = Color.white;

        // ── Log ───────────────────────────────────────────────
        EditorGUILayout.Space(8);
        GUILayout.Label("Journal :", EditorStyles.miniBoldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(180));
        foreach (var line in log)
            EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("Effacer le journal"))
            log.Clear();
    }

    // ─────────────────────────────────────────────────────────────
    // ÉTAPE 1 : EXTRACTION DES FRAMES GIF
    // ─────────────────────────────────────────────────────────────
    private void ExtractAllGifFrames()
    {
        log.Add($"── Extraction démarrée ({DateTime.Now:HH:mm:ss}) ──");

        string absFolder = Path.GetFullPath(spriteFolderPath);
        if (!Directory.Exists(absFolder))
        {
            Log($"[ERREUR] Dossier introuvable : {absFolder}");
            return;
        }

        string[] gifFiles = Directory.GetFiles(absFolder, "*.gif", SearchOption.TopDirectoryOnly);
        if (gifFiles.Length == 0)
        {
            Log("[WARN] Aucun .gif trouvé dans le dossier spécifié.");
            return;
        }

        foreach (var gifPath in gifFiles)
            ExtractGif(gifPath);

        AssetDatabase.Refresh();
        Log("── Extraction terminée, sprites importés ──");
    }

    private void ExtractGif(string gifAbsPath)
    {
        string gifName    = Path.GetFileNameWithoutExtension(gifAbsPath);
        string outDirAbs  = Path.Combine(Path.GetDirectoryName(gifAbsPath), FRAMES_SUBFOLDER, gifName);
        string outDirAsset= $"{spriteFolderPath}/{FRAMES_SUBFOLDER}/{gifName}";

        Directory.CreateDirectory(outDirAbs);

        try
        {
            // System.Drawing est disponible dans l'éditeur Unity (Windows/.NET)
            using var gif = System.Drawing.Image.FromFile(gifAbsPath);
            var fd = new System.Drawing.Imaging.FrameDimension(
                gif.FrameDimensionsList[0]);
            int frameCount = gif.GetFrameCount(fd);

            Log($"{gifName} → {frameCount} frame(s)");

            for (int i = 0; i < frameCount; i++)
            {
                gif.SelectActiveFrame(fd, i);

                string pngName = $"{gifName}_{i:D3}.png";
                string pngAbs  = Path.Combine(outDirAbs, pngName);

                // Sauvegarder la frame en PNG
                using var bmp = new System.Drawing.Bitmap(gif.Width, gif.Height);
                using var g   = System.Drawing.Graphics.FromImage(bmp);
                g.DrawImage(gif, 0, 0);
                bmp.Save(pngAbs, System.Drawing.Imaging.ImageFormat.Png);
            }

            // Configurer l'import Unity pour chaque PNG généré (après AssetDatabase.Refresh)
            // On stocke le chemin pour la post-import (voir ImportPngsInFolder)
        }
        catch (Exception ex)
        {
            Log($"[ERREUR] {gifName} : {ex.Message}");

            // Fallback : si System.Drawing n'est pas dispo, copier le GIF en PNG (1 frame)
            string fallbackPng = Path.Combine(outDirAbs, $"{gifName}_000.png");
            if (!File.Exists(fallbackPng))
            {
                try
                {
                    // Lire le GIF en tant que texture Unity et l'exporter
                    string gifAssetPath = $"{spriteFolderPath}/{Path.GetFileName(gifAbsPath)}";
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(gifAssetPath);
                    if (tex != null)
                    {
                        MakeTextureReadable(gifAssetPath);
                        tex = AssetDatabase.LoadAssetAtPath<Texture2D>(gifAssetPath);
                        File.WriteAllBytes(fallbackPng, tex.EncodeToPNG());
                        Log($"  └ Fallback 1-frame : {fallbackPng}");
                    }
                }
                catch (Exception ex2)
                {
                    Log($"  └ Fallback échoué : {ex2.Message}");
                }
            }
        }
    }

    private static void MakeTextureReadable(string assetPath)
    {
        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp == null || imp.isReadable) return;
        imp.isReadable = true;
        imp.SaveAndReimport();
    }

    // ─────────────────────────────────────────────────────────────
    // POST-IMPORT : configurer les PNG comme sprites pixel art
    // ─────────────────────────────────────────────────────────────
    private void ConfigurePngImportSettings(string assetPath)
    {
        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp == null) return;

        imp.textureType           = TextureImporterType.Sprite;
        imp.spriteImportMode      = SpriteImportMode.Single;
        imp.filterMode            = FilterMode.Point;
        imp.textureCompression    = TextureImporterCompression.Uncompressed;
        imp.alphaIsTransparency   = true;
        imp.mipmapEnabled         = false;
        imp.spritePixelsPerUnit   = 100;

        imp.SaveAndReimport();
    }

    // ─────────────────────────────────────────────────────────────
    // ÉTAPE 2 : ASSIGNATION AU PLAYERANIMATOR
    // ─────────────────────────────────────────────────────────────
    private void AssignAllSprites()
    {
        if (target == null) { Log("[ERREUR] Aucun PlayerAnimator sélectionné."); return; }

        log.Add($"── Assignation démarrée ({DateTime.Now:HH:mm:ss}) ──");
        Undo.RecordObject(target, "Sprite01 Auto-Assign");

        // Configurer tous les PNG déjà importés
        string framesAssetDir = $"{spriteFolderPath}/{FRAMES_SUBFOLDER}";
        ConfigureAllPngsInDirectory(framesAssetDir);

        // Table de correspondance : clé GIF → (ref anim, fps, loop)
        var table = new (string gif, Func<DirectionalAnimation> getter, Action<DirectionalAnimation> setter, float fps, bool loop)[]
        {
            // ── Idle ──
            ("Idle1_SO", () => target.idleSO, a => target.idleSO = a, fpsIdle,  true),
            ("Idle2_SE", () => target.idleSE, a => target.idleSE = a, fpsIdle,  true),
            ("Idle3_NE", () => target.idleNE, a => target.idleNE = a, fpsIdle,  true),
            ("Idle4_NO", () => target.idleNO, a => target.idleNO = a, fpsIdle,  true),
            // ── Walk ──
            ("Walk4_SO", () => target.walkSO, a => target.walkSO = a, fpsWalk,  true),
            ("Walk1_SE", () => target.walkSE, a => target.walkSE = a, fpsWalk,  true),
            ("Walk2_NE", () => target.walkNE, a => target.walkNE = a, fpsWalk,  true),
            ("Walk3_NO", () => target.walkNO, a => target.walkNO = a, fpsWalk,  true),
            // ── Death ──
            ("Death1_SO", () => target.deathSO, a => target.deathSO = a, fpsDeath, false),
            ("Death2_SE", () => target.deathSE, a => target.deathSE = a, fpsDeath, false),
            ("Death3_NE", () => target.deathNE, a => target.deathNE = a, fpsDeath, false),
            ("Death4_NO", () => target.deathNO, a => target.deathNO = a, fpsDeath, false),
        };

        foreach (var entry in table)
        {
            var frames = LoadFramesFor(entry.gif);
            var anim   = new DirectionalAnimation { frames = frames, fps = entry.fps, loop = entry.loop };
            entry.setter(anim);

            if (frames == null || frames.Length == 0)
                Log($"[WARN] {entry.gif} : aucune frame trouvée");
            else
                Log($"✓ {entry.gif} → {frames.Length} frame(s)");
        }

        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
        Log("── Assignation terminée ──");
    }

    /// <summary>
    /// Charge les sprites de frames pour un GIF donné.
    /// Cherche d'abord les PNG extraits, puis le GIF lui-même en fallback.
    /// </summary>
    private Sprite[] LoadFramesFor(string gifName)
    {
        // 1. Chercher dans le dossier Frames/{gifName}/
        string framesDir = $"{spriteFolderPath}/{FRAMES_SUBFOLDER}/{gifName}";
        string[] pngGuids = AssetDatabase.FindAssets("t:Sprite", new[] { framesDir });

        var sprites = new List<(string path, Sprite spr)>();
        foreach (var guid in pngGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (spr != null)
                sprites.Add((path, spr));
        }

        if (sprites.Count > 0)
        {
            // Trier par nom de fichier pour respecter l'ordre des frames
            sprites.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.OrdinalIgnoreCase));
            var result = new Sprite[sprites.Count];
            for (int i = 0; i < sprites.Count; i++) result[i] = sprites[i].spr;
            return result;
        }

        // 2. Fallback : utiliser le sprite du GIF directement (1 frame)
        string gifAssetPath = $"{spriteFolderPath}/{gifName}.gif";
        var fallback = AssetDatabase.LoadAssetAtPath<Sprite>(gifAssetPath);
        if (fallback != null)
        {
            Log($"  └ {gifName} : fallback 1-frame (GIF non extrait)");
            return new[] { fallback };
        }

        return null;
    }

    private void ConfigureAllPngsInDirectory(string assetDir)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { assetDir });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                ConfigurePngImportSettings(path);
        }
    }

    private void Log(string msg)
    {
        log.Add(msg);
        Debug.Log($"[Sprite01Setup] {msg}");
        Repaint();
    }
}
#endif
