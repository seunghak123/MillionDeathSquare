using System;
using UnityEngine;

namespace Seunghak.SceneManager
{
    public abstract class SceneDeliverData
    {
    }
    public abstract class SceneController : MonoBehaviour
    {
        protected SceneDeliverData sceneLoadData;
        protected virtual void Awake()
        {
            RegistSceneController();
        }
        public virtual void SetSceneData(SceneDeliverData info)
        {
            sceneLoadData = info;
        }
        //현재 씬 컨트롤러 초기화 함수
        public virtual void InitSceneController() { }
        // 이 컨트롤러가 담당하는 씬 타입 반환 (단일 씬 아키텍처용)
        protected virtual E_SCENE_TYPE GetSceneType() => E_SCENE_TYPE.LOBBY;
        public E_SCENE_TYPE SceneType => GetSceneType();
        //씬 로드 시 씬 매니저에 현재 컨트롤러 등록하는 함수
        public virtual void RegistSceneController()
        {
            SceneManager.Instance.RegistSceneController(this, GetSceneType());
        }
        public virtual void RegistSceneStepAction(E_SCENESTEP_TYPE actionType, Action playAction)
        {
            SceneManager.Instance.AddStepAction(actionType, playAction);
        }
    }
}
