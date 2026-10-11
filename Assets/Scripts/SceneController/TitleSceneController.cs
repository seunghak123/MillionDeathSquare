using System;
using UnityEngine;

namespace Seunghak.SceneManager
{
    using Seunghak.UIManager;
    public class TitleSceneController : SceneController
    {
        protected override E_SCENE_TYPE GetSceneType() => E_SCENE_TYPE.TITLE;

        protected override void Awake()
        {
            base.Awake();
        }
        //���� �� ��Ʈ�ѷ� �ʱ�ȭ �Լ�
        public override void InitSceneController()
        {
            UIManager.Instance.OpenUI();
            UIManager.Instance.PushUI(UI_TYPE.MDSMainWindow);
        }
        //�� �ε� �� �� �Ŵ����� ���� ��Ʈ�ѷ� ����ϴ� �Լ�
        public override void RegistSceneController()
        {
            base.RegistSceneController();
        }
        public override void RegistSceneStepAction(E_SCENESTEP_TYPE actionType, Action playAction)
        {
            base.RegistSceneStepAction(actionType, playAction);
        }
    }
}
