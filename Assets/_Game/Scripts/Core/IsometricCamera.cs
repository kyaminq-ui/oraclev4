using UnityEngine;

public class IsometricCamera : MonoBehaviour
{
    // =========================================================
    // CONFIGURATION INSPECTOR
    // =========================================================

    [Header("=== ANGLE ISOMÉTRIQUE ===")]
    [Range(20f, 90f)]
    public float isometricAngle = 30f;

    [Header("=== ZOOM ===")]
    public float defaultZoom    = 5f;
    public float minZoom        = 2f;
    public float maxZoom        = 10f;
    public float zoomSpeed      = 1f;
    [Tooltip("Durée du ralentissement après molette (secondes). Plus haut = plus de glisse.")]
    [Range(0.02f, 0.5f)]
    public float zoomSmoothTime = 0.12f;

    [Header("=== PAN (clic molette) ===")]
    [Tooltip("Sensibilité du pan. 1 = 1:1 écran/monde.")]
    public float panSpeed       = 1f;
    [Tooltip("Durée du ralentissement après relâche (secondes). 0 = arrêt immédiat.")]
    [Range(0f, 0.3f)]
    public float panInertia     = 0.08f;

    [Header("=== LIMITES ===")]
    public bool  useBounds    = true;
    public float boundLeft    = -20f;
    public float boundRight   =  20f;
    public float boundBottom  = -20f;
    public float boundTop     =  20f;

    [Header("=== SUIVI DE CIBLE (optionnel) ===")]
    [Tooltip("Laisser vide pour désactiver le suivi.")]
    public Transform target;
    [Range(1f, 20f)]
    public float followSpeed  = 5f;
    public Vector2 followOffset = Vector2.zero;

    [Header("=== PIXEL ART ===")]
    public bool  pixelPerfect   = true;
    public float pixelsPerUnit  = 32f;

    // =========================================================
    // ÉTAT INTERNE
    // =========================================================

    private Camera  cam;
    private float   targetZoom;
    private float   zoomVelocity;

    // Position lisse non-arrondie — SmoothDamp travaille dessus,
    // transform.position reçoit la version arrondie en sortie seulement.
    private Vector3 smoothPos;
    private Vector3 smoothVelocity;

    private bool    isPanning;
    private Vector2 panAnchorScreen;
    private Vector3 panAnchorCamPos;
    // Décalage manuel (pan molette) : position caméra = target + followOffset + followPanOffset.
    private Vector2 followPanOffset;
    private Vector2 panAnchorFollowOffset;

    // =========================================================
    // INITIALISATION
    // =========================================================

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) { Debug.LogError("IsometricCamera : pas de Camera sur ce GameObject."); enabled = false; return; }
        if (!cam.orthographic) { Debug.LogWarning("IsometricCamera : passage en Orthographic automatique."); cam.orthographic = true; }

        targetZoom            = defaultZoom;
        cam.orthographicSize  = defaultZoom;
        smoothPos             = transform.position;
        followPanOffset       = Vector2.zero;
        transform.rotation    = Quaternion.Euler(isometricAngle, 0f, 0f);
    }

    // =========================================================
    // UPDATE
    // =========================================================

    void Update()
    {
        HandleZoom();
        HandlePan();
        HandleFollowTarget();
        ApplyZoom();
        ApplyMovement();
    }

    // =========================================================
    // ZOOM
    // =========================================================

    void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll == 0f) return;
        targetZoom = Mathf.Clamp(targetZoom - scroll * zoomSpeed, minZoom, maxZoom);
    }

    void ApplyZoom()
    {
        cam.orthographicSize = Mathf.SmoothDamp(
            cam.orthographicSize, targetZoom, ref zoomVelocity, zoomSmoothTime);
    }

    // =========================================================
    // PAN CLIC MOLETTE
    // =========================================================

    void HandlePan()
    {
        if (Input.GetMouseButtonDown(2))
        {
            isPanning               = true;
            panAnchorScreen         = Input.mousePosition;
            panAnchorCamPos         = smoothPos;
            panAnchorFollowOffset   = followPanOffset;
            smoothVelocity          = Vector3.zero; // annule toute glisse en cours
        }

        if (Input.GetMouseButtonUp(2))
            isPanning = false;

        if (!isPanning || !Input.GetMouseButton(2)) return;

        // Delta écran → monde via la taille ortho (pas de dépendance à transform.position).
        Vector2 screenDelta = panAnchorScreen - (Vector2)Input.mousePosition;
        float   worldH      = cam.orthographicSize * 2f;
        float   worldW      = worldH * cam.aspect;
        Vector2 worldDelta  = new Vector2(
            screenDelta.x * worldW / Screen.width  * panSpeed,
            screenDelta.y * worldH / Screen.height * panSpeed);

        if (target == null)
        {
            Vector3 newPos = panAnchorCamPos + new Vector3(worldDelta.x, worldDelta.y, 0f);
            smoothPos      = Clamp(newPos);
        }
        else
            followPanOffset = panAnchorFollowOffset + worldDelta;

        smoothVelocity = Vector3.zero;
    }

    // =========================================================
    // SUIVI DE CIBLE
    // =========================================================

    void HandleFollowTarget()
    {
        if (target == null) return;
        Vector3 desired = new Vector3(
            target.position.x + followOffset.x + followPanOffset.x,
            target.position.y + followOffset.y + followPanOffset.y,
            smoothPos.z);
        smoothPos = Clamp(desired);
        // Synchroniser l’offset avec les limites (évite dérive / incohérence après clamp).
        followPanOffset = new Vector2(
            smoothPos.x - target.position.x - followOffset.x,
            smoothPos.y - target.position.y - followOffset.y);
    }

    // =========================================================
    // APPLICATION DU MOUVEMENT
    // =========================================================

    void ApplyMovement()
    {
        Vector3 dest = new Vector3(smoothPos.x, smoothPos.y, transform.position.z);

        // Pendant le clic molette : pas de SmoothDamp — la caméra suit la souris en direct.
        bool panningNow = isPanning && Input.GetMouseButton(2);
        Vector3 arrived;
        if (panningNow)
        {
            arrived        = dest;
            smoothVelocity = Vector3.zero;
        }
        else
        {
            arrived = Vector3.SmoothDamp(
                transform.position, dest, ref smoothVelocity,
                Mathf.Max(panInertia, 0.001f));

            // Tuer la vélocité résiduelle quand on est quasi-arrivé (évite la micro-dérive).
            if (smoothVelocity.sqrMagnitude < 0.00001f && Vector3.Distance(arrived, dest) < 0.0001f)
            {
                arrived        = dest;
                smoothVelocity = Vector3.zero;
            }
        }

        transform.position = pixelPerfect ? RoundToPixel(arrived) : arrived;
    }

    // =========================================================
    // UTILITAIRES
    // =========================================================

    Vector3 Clamp(Vector3 p)
    {
        if (!useBounds) return p;
        return new Vector3(
            Mathf.Clamp(p.x, boundLeft,   boundRight),
            Mathf.Clamp(p.y, boundBottom, boundTop),
            p.z);
    }

    Vector3 RoundToPixel(Vector3 p)
    {
        float s = 1f / pixelsPerUnit;
        return new Vector3(Mathf.Round(p.x / s) * s, Mathf.Round(p.y / s) * s, p.z);
    }

    // =========================================================
    // API PUBLIQUE
    // =========================================================

    public void TeleportTo(Vector2 position)
    {
        smoothPos              = new Vector3(position.x, position.y, transform.position.z);
        smoothPos              = Clamp(smoothPos);
        transform.position     = smoothPos;
        smoothVelocity         = Vector3.zero;
        SyncFollowPanOffsetFromSmoothPos();
    }

    public void MoveTo(Vector2 position)
    {
        smoothPos = Clamp(new Vector3(position.x, position.y, transform.position.z));
        SyncFollowPanOffsetFromSmoothPos();
    }

    public void SetZoom(float value)   => targetZoom = Mathf.Clamp(value, minZoom, maxZoom);
    public void ResetCamera()          { targetZoom = defaultZoom; TeleportTo(Vector2.zero); }

    void SyncFollowPanOffsetFromSmoothPos()
    {
        if (target == null) { followPanOffset = Vector2.zero; return; }
        followPanOffset = new Vector2(
            smoothPos.x - target.position.x - followOffset.x,
            smoothPos.y - target.position.y - followOffset.y);
    }

    // =========================================================
    // GIZMOS
    // =========================================================

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!useBounds) return;
        Gizmos.color = Color.cyan;
        Vector3 tl = new Vector3(boundLeft,  boundTop,    0f);
        Vector3 tr = new Vector3(boundRight, boundTop,    0f);
        Vector3 bl = new Vector3(boundLeft,  boundBottom, 0f);
        Vector3 br = new Vector3(boundRight, boundBottom, 0f);
        Gizmos.DrawLine(tl, tr); Gizmos.DrawLine(tr, br);
        Gizmos.DrawLine(br, bl); Gizmos.DrawLine(bl, tl);
    }
#endif
}
