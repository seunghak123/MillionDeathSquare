using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// SceneManager에 두는게 아니라 Util쪽에 enum으로 씬 종류 나열
public enum E_SCENE_TYPE
{
    INTRO,
    TITLE,
    LOBBY,
    INGAME,
    END,
}
public enum E_SCENESTEP_TYPE
{
    NONE,
    EXITSTEP,
    LOADSTEP,
    OPENSTEP,
    END,
}

namespace Seunghak.SceneManager
{
    public class SceneManager : UnitySingleton<SceneManager>
    {
        LoadingPopup loadingPopupUI; // 임시

        // ------------------------테스트-------------------------------- 
        [SerializeField] private E_SCENE_TYPE testSceneType = E_SCENE_TYPE.LOBBY;
        [SerializeField] private bool isWatingSuccessSequence = false;
        // ------------------------테스트-------------------------------- 
        #region Property
        private Dictionary<E_SCENESTEP_TYPE, Action> addactionList = new Dictionary<E_SCENESTEP_TYPE, Action>();
        private Dictionary<E_SCENESTEP_TYPE, Action> baseactionList = new Dictionary<E_SCENESTEP_TYPE, Action>();
        // 단일 씬 아키텍처: 씬 타입별 컨트롤러 캐시
        private Dictionary<E_SCENE_TYPE, SceneController> controllerCache = new Dictionary<E_SCENE_TYPE, SceneController>();
        private E_SCENESTEP_TYPE currentStepType = E_SCENESTEP_TYPE.NONE;
        private E_SCENE_TYPE currentSceneType = E_SCENE_TYPE.INTRO;
        private E_SCENE_TYPE nextSceneType = E_SCENE_TYPE.INTRO;
        private SceneController currentSceneController = null;
        private SceneDeliverData currentDeliverData = null;

        public SceneDeliverData CurDeliverData
        {
            get { return currentDeliverData; }
            set 
            { 
                if(value!=null)
                {
                    currentDeliverData = value;
                }
            }
        }
        #endregion Property

        protected override void InitSingleton()
        {
            InitSceneManager();
        }
        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                ChangeScene(testSceneType);
            }
        }
        // ------------------------테스트--------------------------------//
        #region UtilityLogic
        public void RegistSceneController(SceneController addedSceneController, E_SCENE_TYPE sceneType)
        {
            controllerCache[sceneType] = addedSceneController;
            currentSceneController = addedSceneController;
        }
        public void RegistSceneController(SceneController addedSceneController)
        {
            currentSceneController = addedSceneController;
        }
        public void AddStepAction(E_SCENESTEP_TYPE addStepType, Action addAction)
        {
            if (addactionList.ContainsKey(addStepType))
            {
                addactionList[addStepType] = addAction;
            }
            else
            {
                addactionList.Add(addStepType, addAction);
            }
        }
        //공용으로 사용할 씬 나갈때 스텝 
        public void ChangeScene(E_SCENE_TYPE nextScene)
        {
            if (currentStepType != E_SCENESTEP_TYPE.NONE) return;
            if (controllerCache.Count == 0)
            {
                var all = FindObjectsByType<SceneController>(FindObjectsSortMode.None);
                foreach (var ctrl in all)
                    controllerCache[ctrl.SceneType] = ctrl;
            }
            nextSceneType = nextScene;
            NextStep(E_SCENESTEP_TYPE.EXITSTEP);
        }
        //베이스가 되는 씬 기반으로 돌아가는 함수
        public static void GoToBase()
        {
            //로비 씬 로드 하고 기본 베이스 화면으로 돌아가는 작업
        }
        #endregion UtilityLogic
        #region BasicLogic
        private void InitSceneManager()
        {
            baseactionList.Add(E_SCENESTEP_TYPE.EXITSTEP, BaseExitStep);
            baseactionList.Add(E_SCENESTEP_TYPE.LOADSTEP, BaseLoadStep);
            baseactionList.Add(E_SCENESTEP_TYPE.OPENSTEP, BaseOpenStep);
        }
        private void Start()
        {
            // 비활성화된 SceneController는 Awake가 호출되지 않아 controllerCache에 등록되지 않음.
            // Start 시점에 씬 내 전체 컨트롤러를 스캔해 누락된 타입을 채운다.
            var all = FindObjectsByType<SceneController>(FindObjectsSortMode.None);
            foreach (var ctrl in all)
                if (!controllerCache.ContainsKey(ctrl.SceneType))
                    controllerCache[ctrl.SceneType] = ctrl;
        }
        private void BaseExitStep()
        {
            currentSceneController = null;
            ExecuteAddStepAcion();
            NextStep(E_SCENESTEP_TYPE.LOADSTEP);
        }
        private void BaseLoadStep()
        {
            StartCoroutine(LoadData());
        }
        private void BaseOpenStep()
        {
            ExecuteAddStepAcion();
            NextStep(E_SCENESTEP_TYPE.END);
        }
        private WaitForEndOfFrame waitframe = new WaitForEndOfFrame();
        private IEnumerator LoadData()
        {
            Debug.Log($"[SceneManager] LoadData 시작. nextSceneType={nextSceneType}");
            loadingPopupUI = UIManager.UIManager.Instance.CreateLoadingUI(UIManager.UI_TYPE.LoadingPopup) as LoadingPopup;
            if (loadingPopupUI != null)
            {
                loadingPopupUI.RestartWaiting();
            }
            ExecuteAddStepAcion();

            // 단일 씬 아키텍처: 캐시된 컨트롤러 재사용, LoadSceneAsync 스킵
            if (controllerCache.TryGetValue(nextSceneType, out SceneController cached))
            {
                Debug.Log($"[SceneManager] controllerCache 히트: {nextSceneType} → {cached.GetType().Name}");
                currentSceneController = cached;
            }
            else
            {
                Debug.LogWarning($"[SceneManager] controllerCache 미스: {nextSceneType}. Unity 씬 로드 시도");
                // 레거시: 실제 Unity 씬 로드 시도
                AsyncOperation taskLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(nextSceneType.ToString());
                if (taskLoad != null)
                {
                    while (true)
                    {
                        if ((taskLoad.isDone || isWatingSuccessSequence) && (loadingPopupUI == null || loadingPopupUI.isEnd))
                            break;
                        yield return waitframe;
                    }
                    while (currentSceneController == null)
                    {
                        yield return waitframe;
                    }
                }
            }

            GC.Collect();
            NextStep(E_SCENESTEP_TYPE.OPENSTEP);
            yield break;
        }
        private void ExecuteStepAction()
        {
            if(baseactionList.Count == 0)
            {
                InitSceneManager();
            }

            if (baseactionList.ContainsKey(currentStepType))
            {
                baseactionList[currentStepType]();
            }
        }
        private void ExecuteAddStepAcion()
        {
            if (addactionList.ContainsKey(currentStepType))
            {
                addactionList[currentStepType]();

                addactionList.Remove(currentStepType);
            }
        }
        private void NextStep(E_SCENESTEP_TYPE nextStepType)
        {
            if (nextStepType == E_SCENESTEP_TYPE.END)
            {
                //모든 씬 로드 액션 끝
                currentStepType = E_SCENESTEP_TYPE.NONE;
                currentSceneType = nextSceneType;
                //로딩 화면 종료
                if(loadingPopupUI!=null)
                {
                    loadingPopupUI.CloseUI();
                }
                if (currentSceneController == null)
                {
                    Debug.LogError($"[SceneManager] currentSceneController가 null입니다. '{nextSceneType}' 컨트롤러가 등록되지 않았습니다. RegistSceneController를 확인하세요.");
                    return;
                }
                Debug.Log($"[SceneManager] InitSceneController 호출: {currentSceneController.GetType().Name}");
                currentSceneController.SetSceneData(currentDeliverData);
                currentSceneController.InitSceneController();
                //각 씬 컨트롤러 여기서 초기화 함수 호출 씬 컨트롤러는 BaseSceneController 상속 받는다.
                // 각 씬에서 반드시 해줘야 할 행동 리스트들을 나열해놓은 인터페이스를 상속받는다
                //인터페이스 상속 받은것을 찾아서 실행만 시켜주면 된다.
                return;
            }
            currentStepType = nextStepType;
            ExecuteStepAction();
        }
        #endregion BasicLogic
    }
}
