using System.Collections;
using UnityEngine;

/// <summary>
/// Une animation directionnelle : tableau de sprites + cadence.
/// </summary>
[System.Serializable]
public class DirectionalAnimation
{
    [Tooltip("Frames dans l'ordre de lecture")]
    public Sprite[] frames;

    [Tooltip("Images par seconde (peut être overridé au runtime)")]
    [Min(1f)] public float fps = 8f;

    [Tooltip("Boucle infinie (désactiver pour mort / cast one-shot)")]
    public bool loop = true;
}

/// <summary>
/// Slot de direction : utilisé pour le remappage visuel.
/// </summary>
public enum DirectionSlot { SO, SE, NE, NO }

/// <summary>
/// Gère les animations sprites directionnelles du personnage (Idle / Marche / Mort).
///
/// Architecture :
///   Au démarrage, ce script crée un child "SpriteVisual" qui porte le SpriteRenderer.
///   Le parent (TacticalCharacter) garde sa position logique de grille.
///   Le child peut être déplacé/mis à l'échelle librement sans perturber la logique.
///
/// — Remappage de directions —
/// Si une direction affiche le mauvais sprite, change les dropdowns
/// "Quand le perso va vers X, jouer le sprite →" dans l'Inspector.
///
/// — Vitesse de marche —
/// 1-2 PM : marche lente (walkFpsSlow)
/// 3+ PM  : marche normale (walkFpsNormal)
/// </summary>
[RequireComponent(typeof(TacticalCharacter))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimator : MonoBehaviour
{
    // =========================================================
    // TAILLE & POSITION VISUELLE
    // =========================================================
    [Header("Taille & position visuelle")]
    [Tooltip("Facteur d'échelle du sprite (1 = original, 2 = double)")]
    public float spriteScale = 2f;

    [Tooltip("Décalage visuel du sprite par rapport à la position logique de la case.\n" +
             "X : centrage horizontal  |  Y : hauteur sur la case (positif = monter)")]
    public Vector2 visualOffset = new Vector2(0f, 0.25f);

    // =========================================================
    // ANIMATIONS — à remplir via l'outil Editor ou l'Inspector
    // =========================================================
    [Header("Idle (repos)")]
    public DirectionalAnimation idleSO;
    public DirectionalAnimation idleSE;
    public DirectionalAnimation idleNE;
    public DirectionalAnimation idleNO;

    [Header("Walk (marche)")]
    public DirectionalAnimation walkSO;
    public DirectionalAnimation walkSE;
    public DirectionalAnimation walkNE;
    public DirectionalAnimation walkNO;

    [Header("Death (mort)")]
    public DirectionalAnimation deathSO;
    public DirectionalAnimation deathSE;
    public DirectionalAnimation deathNE;
    public DirectionalAnimation deathNO;

    // =========================================================
    // VITESSE DE MARCHE (liée aux PM)
    // =========================================================
    [Header("Vitesse de marche")]
    [Tooltip("FPS pour 3+ PM (marche normale)")]
    public float walkFpsNormal = 10f;
    [Tooltip("FPS pour 1-2 PM (marche lente)")]
    public float walkFpsSlow   = 5f;

    // =========================================================
    // REMAPPAGE DES DIRECTIONS
    // =========================================================
    [Header("Remappage des directions")]
    [Tooltip("Quand le perso se déplace vers SO (bas-gauche), jouer le sprite →")]
    public DirectionSlot mapSO = DirectionSlot.SO;
    [Tooltip("Quand le perso se déplace vers SE (bas-droite), jouer le sprite →")]
    public DirectionSlot mapSE = DirectionSlot.SE;
    [Tooltip("Quand le perso se déplace vers NE (haut-droite), jouer le sprite →")]
    public DirectionSlot mapNE = DirectionSlot.NE;
    [Tooltip("Quand le perso se déplace vers NO (haut-gauche), jouer le sprite →")]
    public DirectionSlot mapNO = DirectionSlot.NO;

    // =========================================================
    // INTERNES
    // =========================================================
    private TacticalCharacter character;
    private SpriteRenderer    spriteRenderer;
    private Transform         spriteRoot;
    private Coroutine         currentAnim;
    private int               lastMoveSteps = 3;

    // =========================================================
    // CYCLE DE VIE
    // =========================================================
    void Awake()
    {
        character      = GetComponent<TacticalCharacter>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        SetupSpriteRoot();

        character.OnStateChanged  += HandleStateChanged;
        character.OnFacingChanged += HandleFacingChanged;
        character.OnMoveStarted   += HandleMoveStarted;

        PlayForCurrentState();
    }

    void OnDestroy()
    {
        if (character == null) return;
        character.OnStateChanged  -= HandleStateChanged;
        character.OnFacingChanged -= HandleFacingChanged;
        character.OnMoveStarted   -= HandleMoveStarted;
    }

    // =========================================================
    // SETUP DU CHILD SPRITE
    // =========================================================
    /// <summary>
    /// Crée un child "SpriteVisual" qui porte le SpriteRenderer visuel.
    /// Le parent garde la position logique de grille ; le child gère
    /// l'échelle et l'offset purement visuels.
    /// </summary>
    private void SetupSpriteRoot()
    {
        var original = GetComponent<SpriteRenderer>();

        // Créer le child
        var spriteRootGO = new GameObject("SpriteVisual");
        spriteRoot = spriteRootGO.transform;
        spriteRoot.SetParent(transform, worldPositionStays: false);
        spriteRoot.localPosition = new Vector3(visualOffset.x, visualOffset.y, 0f);
        spriteRoot.localScale    = new Vector3(spriteScale, spriteScale, 1f);

        // Transférer les propriétés du SpriteRenderer original vers le child
        var childRenderer = spriteRootGO.AddComponent<SpriteRenderer>();
        childRenderer.sprite           = original.sprite;
        childRenderer.color            = original.color;
        childRenderer.material         = original.material;
        childRenderer.sortingLayerID   = original.sortingLayerID;
        childRenderer.sortingLayerName = original.sortingLayerName;
        childRenderer.sortingOrder     = original.sortingOrder;

        // Désactiver l'original, utiliser le child
        original.enabled = false;
        spriteRenderer = childRenderer;

        // Mettre à jour la référence dans TacticalCharacter
        character.spriteRenderer = childRenderer;
    }

    // =========================================================
    // CALLBACKS
    // =========================================================
    private void HandleStateChanged(CharacterState _) => PlayForCurrentState();

    private void HandleFacingChanged(FacingDirection _)
    {
        if (character.State != CharacterState.Dead)
            PlayForCurrentState();
    }

    private void HandleMoveStarted(int steps)
    {
        lastMoveSteps = steps;
    }

    // =========================================================
    // LOGIQUE PRINCIPALE
    // =========================================================
    private void PlayForCurrentState()
    {
        var anim = ResolveAnimation();
        if (anim == null || anim.frames == null || anim.frames.Length == 0) return;

        float fpsOverride = -1f;
        if (character.State == CharacterState.Moving)
            fpsOverride = lastMoveSteps >= 3 ? walkFpsNormal : walkFpsSlow;

        if (currentAnim != null) StopCoroutine(currentAnim);
        currentAnim = StartCoroutine(RunAnimation(anim, fpsOverride));
    }

    private DirectionalAnimation ResolveAnimation()
    {
        switch (character.State)
        {
            case CharacterState.Idle:
            case CharacterState.Casting:
                return PickDirection(idleSO, idleSE, idleNE, idleNO);

            case CharacterState.Moving:
                return PickDirection(walkSO, walkSE, walkNE, walkNO);

            case CharacterState.Dead:
                return PickDirection(deathSO, deathSE, deathNE, deathNO);

            default:
                return null;
        }
    }

    // =========================================================
    // REMAPPAGE DE DIRECTION
    // =========================================================
    private DirectionalAnimation PickDirection(
        DirectionalAnimation so, DirectionalAnimation se,
        DirectionalAnimation ne, DirectionalAnimation no)
    {
        DirectionSlot slot;
        switch (character.Facing)
        {
            case FacingDirection.SouthWest: slot = mapSO; break;
            case FacingDirection.SouthEast: slot = mapSE; break;
            case FacingDirection.NorthEast: slot = mapNE; break;
            case FacingDirection.NorthWest: slot = mapNO; break;
            default:                        slot = mapSE; break;
        }

        switch (slot)
        {
            case DirectionSlot.SO: return so;
            case DirectionSlot.SE: return se;
            case DirectionSlot.NE: return ne;
            case DirectionSlot.NO: return no;
            default:               return se;
        }
    }

    // =========================================================
    // COROUTINE D'ANIMATION
    // =========================================================
    private IEnumerator RunAnimation(DirectionalAnimation anim, float fpsOverride = -1f)
    {
        Sprite[] frames = anim.frames;
        if (frames == null || frames.Length == 0) yield break;

        if (frames.Length == 1)
        {
            spriteRenderer.sprite = frames[0];
            yield break;
        }

        float fps   = fpsOverride > 0f ? fpsOverride : anim.fps;
        float delay = 1f / Mathf.Max(0.1f, fps);
        int   frame = 0;

        while (true)
        {
            spriteRenderer.sprite = frames[frame];
            frame++;

            if (frame >= frames.Length)
            {
                if (!anim.loop) yield break;
                frame = 0;
            }

            yield return new WaitForSeconds(delay);
        }
    }
}
