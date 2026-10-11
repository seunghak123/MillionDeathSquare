using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.UIManager
{
    public class BaseCanvas : UnitySingleton<BaseCanvas>
    {
        [SerializeField] public Transform windowUIParent;
        [SerializeField] public Transform popUpUIParent;
        [SerializeField] public Transform utilUIParent;
        [SerializeField] public Transform effectParent;

        [Header("세로로 긴 화면에서는 가로 기준으로 맞춰 배경이 화면을 가득 채우도록")]
        [SerializeField] private CanvasScaler canvasScaler;

        private float _lastAspect;

        private void Start()
        {
            if (canvasScaler == null) canvasScaler = GetComponent<CanvasScaler>();
            UpdateScalerMatch();
        }

        private void Update()
        {
            if (Screen.height <= 0) return;
            if (!Mathf.Approximately((float)Screen.width / Screen.height, _lastAspect))
            {
                UpdateScalerMatch();
            }
        }

        // 기준 해상도보다 세로로 길면 Width 기준(0), 가로로 넓으면 Height 기준(1).
        // 세로로 긴 폰에서 좌우가 잘리지 않고, 남는 세로 공간은 stretch 배경이 채운다.
        private void UpdateScalerMatch()
        {
            if (Screen.height <= 0) return;
            _lastAspect = (float)Screen.width / Screen.height;
            if (canvasScaler == null || canvasScaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return;

            Vector2 reference = canvasScaler.referenceResolution;
            float referenceAspect = reference.x / reference.y;
            canvasScaler.matchWidthOrHeight = _lastAspect < referenceAspect ? 0f : 1f;
        }

        public void CloseUI()
        {
            windowUIParent.gameObject.SetActive(false);
            popUpUIParent.gameObject.SetActive(false);
            utilUIParent.gameObject.SetActive(false);
        }

        public void OpenUI()
        {
            windowUIParent.gameObject.SetActive(true);
            popUpUIParent.gameObject.SetActive(true);
            utilUIParent.gameObject.SetActive(true);
        }

        public Transform GetEffectParent()
        {
            return effectParent;
        }
    }
}
