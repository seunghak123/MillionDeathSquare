using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundPlayer : MonoBehaviour
{
    [SerializeField] public AnimationCurve curveFadeTime;
    [SerializeField] public float delayTime = 0.0f;
    [SerializeField] public float fadeTime = 1.0f;
    [SerializeField] private AudioSource audioSource;

    private void Awake()
    {
        StartCoroutine(PlayAudioSource(delayTime));
    }

    private IEnumerator PlayAudioSource(float delayTime)
    {

        yield return WaitTimeManager.WaitForEndFrame();

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        yield return WaitTimeManager.WaitForTimeSeconds(delayTime);

        if (audioSource != null)
        {
            audioSource.Play();

            if (fadeTime > 0)
            {
                StartCoroutine(PlayAudioFadeInOut(audioSource));
            }
            else
            {
                audioSource.volume = 1;
            }
        }
    }
    private IEnumerator PlayAudioFadeInOut(AudioSource source)
    {
        float currentTime = 0.0f;
        while(currentTime< fadeTime)
        {
            yield return WaitTimeManager.WaitForRealTimeSeconds(0.02f);
            currentTime += 0.02f;
            curveFadeTime.Evaluate(currentTime / fadeTime);
        }

        source.volume = 1;
        yield return null;
    }
}
