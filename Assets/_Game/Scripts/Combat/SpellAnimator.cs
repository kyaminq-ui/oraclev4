using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Lecture optionnelle de VFX avant résolution du sort ; sans composant ou durée = 0, les effets s'appliquent immédiatement.
/// À attacher au même GO qu'un TacticalCharacter ou enfant.
/// </summary>
public class SpellAnimator : MonoBehaviour
{
    public float resolvedDelaySeconds = 0.35f;

    /// <summary>Attente VFX puis résolution (callback = SpellResolver.Resolve).</summary>
    public IEnumerator PlayThenResolve(SpellData spell, Action resolveAction)
    {
        float t = Mathf.Max(0f, resolvedDelaySeconds);
        if (t > 0f)
            yield return new WaitForSeconds(t);
        resolveAction?.Invoke();
    }
}
