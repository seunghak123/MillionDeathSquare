using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 연출 (기획서 3.2 / 3.4): 죽음 = 조각이 펑 터짐 + 화면 살짝 흔들림, 동전 = 톡 튐, 골인 = 만세 + 축하, 체크포인트 = 반짝.
    /// 파티클 시스템 1개를 이벤트마다 다른 값으로 Emit 한다 (파티클 4종을 한 시스템으로).
    /// </summary>
    public sealed class MDSEffects : MonoBehaviour
    {
        private ParticleSystem _ps;
        private Camera _cam;
        private Vector3 _camBase;
        private float _shakeTime;
        private float _shakeAmp;
        private bool _shaking;

        public void Init(MDSSpriteLibrary sprites, Camera cam, int sortingOrder)
        {
            _cam = cam;
            var go = new GameObject("Particles");
            go.transform.SetParent(transform, false);
            _ps = go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 512;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.6f;
            main.gravityModifier = 0f;
            var emission = _ps.emission;
            emission.enabled = false;
            var shape = _ps.shape;
            shape.enabled = false;
            var size = _ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var rot = _ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sortingOrder = sortingOrder;
            r.material = new Material(Shader.Find("Sprites/Default"));
            if (sprites != null && sprites.particle != null)
            {
                var tsa = _ps.textureSheetAnimation;
                tsa.enabled = true;
                tsa.mode = ParticleSystemAnimationMode.Sprites;
                tsa.AddSprite(sprites.particle);
            }
        }

        public void SetCamera(Camera cam)
        {
            if (_shaking && _cam != null) _cam.transform.position = _camBase;
            _shaking = false;
            _cam = cam;
        }

        private void Emit(Vector3 pos, Color color, int count, float speed, float size, float life, float gravity)
        {
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float s = speed * Random.Range(0.4f, 1f);
                p.position = pos;
                p.velocity = new Vector3(Mathf.Cos(a) * s, Mathf.Sin(a) * s + gravity * 0.3f, 0f);
                p.startColor = color;
                p.startSize = size * Random.Range(0.6f, 1.2f);
                p.startLifetime = life * Random.Range(0.7f, 1.1f);
                p.rotation = Random.Range(0f, 360f);
                _ps.Emit(p, 1);
            }
            if (!_ps.isPlaying) _ps.Play();
        }

        /// <summary>죽음: 네모가 작은 조각으로 펑 + 화면 흔들림</summary>
        public void DeathBurst(Vector3 pos, Color color)
        {
            Emit(pos, color, 18, 7f, 0.18f, 0.5f, 0f);
            Shake(0.18f, 0.12f * MDSSettings.ShakeIntensity);
        }

        public void CoinPop(Vector3 pos, Color color) { Emit(pos, color, 8, 3.5f, 0.12f, 0.35f, 1f); }

        public void CheckpointSparkle(Vector3 pos, Color color) { Emit(pos, color, 12, 2.5f, 0.14f, 0.6f, 2f); }

        public void GoalConfetti(Vector3 pos, MDSPaletteColors pal)
        {
            Emit(pos, pal.coin, 14, 6f, 0.16f, 0.9f, 3f);
            Emit(pos, pal.goal, 14, 6f, 0.16f, 0.9f, 3f);
            Emit(pos, pal.player, 10, 6f, 0.16f, 0.9f, 3f);
        }

        public void Shake(float seconds, float amplitude)
        {
            if (_cam == null || amplitude <= 0f) return;
            if (!_shaking) _camBase = _cam.transform.position;
            _shaking = true;
            _shakeTime = seconds;
            _shakeAmp = amplitude;
        }

        private void LateUpdate()
        {
            if (!_shaking || _cam == null) return;
            _shakeTime -= Time.unscaledDeltaTime;
            if (_shakeTime <= 0f)
            {
                _cam.transform.position = _camBase;
                _shaking = false;
                return;
            }
            float a = _shakeAmp * Mathf.Clamp01(_shakeTime * 8f);
            _cam.transform.position = _camBase + new Vector3(Random.Range(-a, a), Random.Range(-a, a), 0f);
        }
    }
}
