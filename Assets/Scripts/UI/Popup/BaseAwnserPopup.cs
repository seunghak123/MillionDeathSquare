using System;
using UnityEngine;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 확인(예/아니요) → 결과(성공/실패) 3단계 팝업.
    /// 캐릭터 구매 확인용으로 쓰이지만 구조상 다른 확인+결과 플로우에도 재사용 가능.
    /// </summary>
    public class BaseAwnserPopup : BaseUI
    {
        [Header("확인 패널 (구매하시겠습니까?)")]
        [SerializeField] private GameObject questionPanel;

        [Header("성공 패널")]
        [SerializeField] private GameObject completePanel;

        [Header("실패 패널")]
        [SerializeField] private GameObject failPanel;

        private Action _onConfirm;
        private Action _onCancel;

        /// <summary>확인 패널을 띄운다. "예" 클릭 시 onConfirm 실행 — 그 결과에 따라 호출부가
        /// ShowComplete()/ShowFail()을 이어서 호출해 결과 패널로 전환해야 한다.</summary>
        public void OpenPopup(Action onConfirm, Action onCancel = null)
        {
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            ShowPanel(questionPanel);
        }

        public void ShowComplete() => ShowPanel(completePanel);
        public void ShowFail() => ShowPanel(failPanel);

        private void ShowPanel(GameObject panel)
        {
            if (questionPanel != null) questionPanel.SetActive(panel == questionPanel);
            if (completePanel != null) completePanel.SetActive(panel == completePanel);
            if (failPanel != null) failPanel.SetActive(panel == failPanel);
        }

        // 버튼 OnClick은 프리팹 Inspector에서 아래 메서드에 직접 연결되어 있음.
        public void OnClickQuestionOkay() => _onConfirm?.Invoke();

        public void OnClickQuestionNo()
        {
            _onCancel?.Invoke();
            ClosePopup();
        }

        /// <summary>성공/실패 패널의 확인 버튼 — 둘 다 이 메서드로 연결.</summary>
        public void OnClickResultOkay() => ClosePopup();

        private void ClosePopup()
        {
            gameObject.SetActive(false);
        }
    }
}
