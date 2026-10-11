using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

namespace Seunghak.UIManager
{
    public class DownloadBundlePopup : MonoBehaviour
    {
        [SerializeField] private Button okayButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private TextMeshProUGUI downloadLabel;

        private Action okayAct = null;
        private Action exitAct = null;
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnClickExit();
            }
        }
        public void SetButtonAction(Action okayAction, Action exitAction,string downloadAmount)
        {
            okayAct = okayAction;
            exitAct = exitAction;
            downloadLabel.text = $"다운로드 용량 {downloadAmount}";
            SetActionToButton();
        }

        private void SetActionToButton()
        {
            okayButton.onClick.AddListener(()=> {
                if (okayAct != null)
                {
                    okayAct();
                    CloseUI();
                }
            });

            exitButton.onClick.AddListener(() => {
                if (exitAct != null)
                {
                    exitAct();
                    CloseUI();
                }
            });
        }
        private void OnClickExit()
        {
            if (exitAct != null)
            {
                exitAct();
            }
            CloseUI();
        }
        private void CloseUI()
        {
            this.gameObject.SetActive(false);
        }
    }
}