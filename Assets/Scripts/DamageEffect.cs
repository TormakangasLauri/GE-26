using UnityEngine;
using UnityEngine.Rendering;

public class DamageEffect : MonoBehaviour
{    
    public Volume pulseVolume;        // e.g. red vignette flash
    public Volume lowHealthVolume;    // e.g. desaturation + stronger vignette
    public float lowHealthThreshold = 0.35f;   // fraction of max health
    public float fadeSpeed = 2f;

    float lowTarget;

    public void OnPlayerDamaged(float current, float max)
    {
        pulseVolume.weight = 1f;
        Refresh(current, max);
    }

    public void OnPlayerHealed(float current, float max) { Refresh(current, max); }

    void Refresh(float current, float max)
    {
        float ratio = current / max;
        lowTarget = ratio < lowHealthThreshold ? 1f - ratio / lowHealthThreshold : 0f;
    }

    void Update()
    {
        pulseVolume.weight = Mathf.MoveTowards(pulseVolume.weight, 0f, fadeSpeed * Time.deltaTime);
        lowHealthVolume.weight = Mathf.MoveTowards(lowHealthVolume.weight, lowTarget, fadeSpeed * Time.deltaTime);
    }
}