using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Seunghak.UIManager
{
    using Seunghak.Common;
    public class UIManager : UnitySingleton<UIManager>
    {
        [SerializeField] private BaseCanvas baseCanvasObject;
        private Stack<string> windowStack = new Stack<string>();
        private Stack<string> popupStack = new Stack<string>();
        private Dictionary<string,BaseUI> addedWindowLists = new Dictionary<string, BaseUI>();
        private string currentUIString = string.Empty;
        private BaseUI currentUI = null;
        private BaseUI currentLoadingPopup = null;

        #region UI_FlowLogic
        public void PopupWindow()
        {
            //윈도우 뺴주는 작업 연계되어있는 것들을 다뺸다.
            while (windowStack.Count > 0)
            {
                BaseUI popUI = GetUI(windowStack.Peek());
                if (popUI is BaseUIWindow)
                {
                    //if (popUI is LobbyWindow)
                    //{
                    //    break;
                    //}
                    windowStack.Pop();
                    popUI.CloseUI();
                    if (windowStack.Count > 0)
                    {
                        currentUI = GetUI(windowStack.Peek());
                        currentUIString = currentUI.CUR_UI_TYPE.ToString();
                        // GetUI()는 캐시된 창을 SetActive(true)만 시킬 뿐 데이터를 다시 읽지 않는다.
                        // 상점에서 광고보고 아이템 사고 닫아도 로비가 그대로 있던 화면(SetActive(false)
                        // 되기 전 상태)을 그대로 보여줘서 아이템 개수 등이 갱신 안 된 것처럼 보였다.
                        if (currentUI is BaseUIWindow)
                        {
                            currentUI.RestoreWindow();
                        }
                    }
                    break;
                }
            }


            if (windowStack.Count == 0)
            {
                PushUI(UI_TYPE.LobbyWindow);
            }
        }
        public void PopPopup()
        {
            while (popupStack.Count > 0)
            {
                BaseUI popUI = GetUI(popupStack.Pop());
                if (popUI is BaseUIPopup)
                {
                    popUI.CloseUI();
                    if (popupStack.Count > 0)
                    {
                        currentUI = GetUI(popupStack.Peek());
                    }
                    else if (windowStack.Count > 0)
                    {
                        currentUI = GetUI(windowStack.Peek());
                        // PopupWindow()와 동일한 이유로 필요 — GetUI()는 SetActive(true)만 하고
                        // 데이터를 다시 읽지 않는다. 팝업(설정창 등)을 닫아 윈도우가 드러날 때도
                        // RestoreWindow()를 호출해야 BGM 재생/데이터가 새로고침된다. 이게 없어서
                        // 설정창을 열었다 닫으면 로비 BGM이 갱신되지 않거나 어긋나는 문제가 있었다.
                        if (currentUI is BaseUIWindow)
                        {
                            currentUI.RestoreWindow();
                        }
                    }
                    if (currentUI != null)
                        currentUIString = currentUI.CUR_UI_TYPE.ToString();
                    break;
                }
            }
        }
        public void PopUI()
        {
            if (windowStack.Count > 0)
            {
                if (currentUI == null)
                {
                    currentUI = GetUI(currentUIString);
                }
                if(currentUI is BaseUIWindow)
                {
                    PopupWindow();
                }
                else if(currentUI is BaseUIPopup)
                {
                    PopPopup();
                }
            }
        }
        public void PopAllWindow()
        {
            while (windowStack.Count > 0)
            {
                PopUI();
            }
        }
        public void RestoreWindow()
        {
            while (windowStack.Count > 0)
            {
                BaseUI popuserUI = GetUI(windowStack.Peek());
                if (popuserUI is BaseUIWindow)
                {
                    popuserUI.RestoreWindow();
                    break;
                }
                else
                {
                    PopUI();
                }
                popuserUI.RestoreWindow();
            }
        }
        public BaseUI CreateLoadingUI(UI_TYPE uiType)
        {
            string targetUIName = uiType.ToString();
            BaseUI uicomponent = null;

            if (addedWindowLists.ContainsKey(targetUIName))
            {
                uicomponent = addedWindowLists[targetUIName];
            }

            if (currentLoadingPopup != null && currentLoadingPopup == uicomponent)
            {
                currentLoadingPopup.gameObject.SetActive(true);
                currentLoadingPopup.EnterWindow();
                return currentLoadingPopup;
            }

            if(currentLoadingPopup != null)
            {
                currentLoadingPopup.gameObject.SetActive(false);
            }

            if (uicomponent == null)
            {
                addedWindowLists.Remove(targetUIName);
                GameObject targetUI = GameResourceManager.Instance.SpawnObject(targetUIName);

                if (targetUI == null)
                {
                    Debug.Log("Error : TargetUI Object is Empty");
                    return null;
                }
                targetUI.SetActive(true);
                uicomponent = targetUI.GetComponent<BaseUI>();
                uicomponent.StartWindow();
                addedWindowLists.Add(targetUIName, uicomponent);
            }

            if (uicomponent != null)
            {
                uicomponent.CUR_UI_TYPE = uiType;
                int layerSortingOrder = 0;
                if (uicomponent.GetComponent<BaseUIWindow>() != null)
                {
                    layerSortingOrder = 9999;

                    BaseUIWindow targetUIWindow = uicomponent.GetComponent<BaseUIWindow>();
                    uicomponent.transform.SetParent(UIManager.Instance.baseCanvasObject.windowUIParent, false);
                }
                else if (uicomponent.GetComponent<BaseUIPopup>() != null)
                {
                    layerSortingOrder = 9999;
                    uicomponent.transform.SetParent(UIManager.Instance.baseCanvasObject.popUpUIParent, false);
                    BaseUIPopup targetPopupWindow = uicomponent.GetComponent<BaseUIPopup>();
                }
                else
                {
                    layerSortingOrder = 9999;
                    uicomponent.transform.parent = UIManager.Instance.baseCanvasObject.utilUIParent;
                }
                uicomponent.transform.position = Vector3.zero;
                Canvas targetUICanvas = uicomponent.GetComponent<Canvas>();
                if (targetUICanvas != null)
                {
                    targetUICanvas.sortingOrder = layerSortingOrder;
                }
                else
                {
                    Debug.Log("Cavas is Null ");
                }
                FitToParentLayer(uicomponent);
                uicomponent.gameObject.SetActive(true);
            }
            if (uicomponent != null)
            {
                currentLoadingPopup = uicomponent;
                uicomponent.gameObject.SetActive(true);
                uicomponent.EnterWindow();
            }
            return uicomponent;
        }
        public BaseUI PushUI(UI_TYPE uiType)
        {
            string targetUIName = uiType.ToString();
            BaseUI uicomponent = null;

            if (addedWindowLists.ContainsKey(targetUIName))
            {
                uicomponent = addedWindowLists[targetUIName];
            }

            // StartWindow()는 최초 생성 시 한 번만 돈다. 이미 캐시된 창을 다시 PushUI로 보여줄 때
            // (예: 인게임→로비 씬 전환 시 PushUI(GameStartWindow))는 EnterWindow()만 호출되는데,
            // BGM 재생/데이터 새로고침 같은 로직은 StartWindow()/RestoreWindow()에만 있어서
            // 두 번째 방문부터는 전혀 갱신되지 않는 문제가 있었다 (로비 복귀 시 BGM 전환 안 됨 등).
            bool wasAlreadyCached = uicomponent != null;

            if (uicomponent == null)
            {
                addedWindowLists.Remove(targetUIName);
                GameObject targetUI = GameResourceManager.Instance.SpawnObject(targetUIName);

                if (targetUI == null)
                {
                    Debug.Log("Error : TargetUI Object is Empty");
                    return null;
                }
                targetUI.SetActive(true);
                uicomponent = targetUI.GetComponent<BaseUI>();
                uicomponent.StartWindow();
                addedWindowLists.Add(targetUIName, uicomponent);
            }

            if (uicomponent != null)
            {
                uicomponent.CUR_UI_TYPE = uiType;
                int layerSortingOrder = 0;
                if (uicomponent.GetComponent<BaseUIWindow>() != null)
                {
                    layerSortingOrder = 10;

                    BaseUIWindow targetUIWindow = uicomponent.GetComponent<BaseUIWindow>();
                    uicomponent.transform.SetParent(UIManager.Instance.baseCanvasObject.windowUIParent, false);
                    if (currentUI is BaseUIPopup)
                    {
                        currentUI.ExitWindow();
                        if (windowStack.Count > 0)
                        {
                            GetUI(windowStack.Peek()).gameObject.SetActive(false);
                        }
                        currentUI.gameObject.SetActive(false);
                    }
                    else if (currentUI is BaseUIWindow)
                    {
                        currentUI.gameObject.SetActive(false);
                    }
                    currentUI = targetUIWindow;
                    currentUIString = targetUIName;
                    popupStack.Clear();
                }
                else if (uicomponent.GetComponent<BaseUIPopup>() != null)
                {
                    layerSortingOrder = 100;
                    uicomponent.transform.SetParent(UIManager.Instance.baseCanvasObject.popUpUIParent, false);
                    BaseUIPopup targetPopupWindow = uicomponent.GetComponent<BaseUIPopup>();

                    if (currentUI is BaseUIPopup)
                    {
                        currentUI.ExitWindow();
                    }
                    currentUI = targetPopupWindow;
                    currentUIString = targetUIName;
                }
                else
                {
                    layerSortingOrder = 5000;
                    uicomponent.transform.parent = UIManager.Instance.baseCanvasObject.utilUIParent;
                }
                uicomponent.transform.position = Vector3.zero;
                Canvas targetUICanvas = uicomponent.GetComponent<Canvas>();
                if (targetUICanvas != null)
                {
                    targetUICanvas.sortingOrder = layerSortingOrder;
                }
                else
                {
                    Debug.Log("Cavas is Null ");
                }
                FitToParentLayer(uicomponent);
                uicomponent.gameObject.SetActive(true);
            }
            if (uiType == UI_TYPE.LobbyWindow || uiType == UI_TYPE.GameStartWindow)
            {
                windowStack.Clear();
                popupStack.Clear();
            }
            if (uicomponent != null)
            {
                if (uicomponent is BaseUIWindow)
                {
                    if (!windowStack.Contains(targetUIName))
                    {
                        windowStack.Push(targetUIName);
                    }
                    else
                    {
                        while (windowStack.Count > 0 && windowStack.Peek() != targetUIName)
                        {
                            GetUI(windowStack.Pop()).ExitWindow();
                        }
                    }
                }
                else if (uicomponent is BaseUIPopup)
                {
                    if (!popupStack.Contains(targetUIName))
                    {
                        popupStack.Push(targetUIName);
                    }
                }
                uicomponent.EnterWindow();

                // 이미 만들어져 있던 창을 재사용하는 경우(StartWindow()는 못 탐) EnterWindow()만으로는
                // BGM/데이터 갱신이 안 되므로 RestoreWindow()도 함께 호출해 최신 상태로 되돌린다.
                if (wasAlreadyCached && uicomponent is BaseUIWindow)
                {
                    uicomponent.RestoreWindow();
                }
            }
            return uicomponent;
        }
        public BaseUI GetUI(string uiType)
        {
            string targetUIName = uiType;
            BaseUI uicomponent = null;

            if (addedWindowLists.ContainsKey(targetUIName))
            {
                uicomponent = addedWindowLists[targetUIName];
            }
            if (uicomponent == null)
            {
                GameObject targetUI = GameResourceManager.Instance.SpawnObject(targetUIName);

                if (targetUI == null)
                {
                    Debug.Log("Error : TargetUI Object is Empty");
                    return null;
                }
                targetUI.SetActive(true);
                uicomponent = targetUI.GetComponent<BaseUI>();

                int layerSortingOrder = 0;

                if (targetUI.GetComponent<BaseUIWindow>() != null)
                {
                    layerSortingOrder = 10;
                    targetUI.transform.parent = UIManager.Instance.baseCanvasObject.windowUIParent;  
                }
                else if (targetUI.GetComponent<BaseUIPopup>() != null)
                {
                    layerSortingOrder = 100;
                    targetUI.transform.parent = UIManager.Instance.baseCanvasObject.popUpUIParent;
                }
                else
                {
                    layerSortingOrder = 5000;
                    targetUI.transform.parent = UIManager.Instance.baseCanvasObject.utilUIParent;
                }
                targetUI.transform.position = Vector3.zero;
                Canvas targetUICanvas = targetUI.GetComponent<Canvas>();
                if (targetUICanvas != null)
                {
                    targetUICanvas.sortingLayerID = layerSortingOrder;
                }
                else
                {
                    Debug.Log("Cavas is Null ");
                }
                if (targetUI.GetComponent<RectTransform>() != null)
                {
                    targetUI.GetComponent<RectTransform>().localPosition = Vector2.zero;
                }

                addedWindowLists.Add(targetUIName, uicomponent);
            }
            else
            {
                uicomponent.gameObject.SetActive(true);
            }
            return uicomponent;
        }
        #endregion UI_FlowLogic
        #region InGame_Logic
        public BaseUIWindow GetCurWindow()
        {
            if (windowStack.Count == 0) return null;
            return GetUI(windowStack.Peek()) as BaseUIWindow;
        }
        public void CloseUI()
        {
            baseCanvasObject.CloseUI();
        }
        public void OpenUI()
        {
            baseCanvasObject.OpenUI();
        }

        /// <summary>
        /// 창/팝업 루트를 부모 레이어에 꽉 채운다 (스케일 1, 앵커 stretch, 오프셋 0).
        /// 루트에 Canvas가 붙은 프리팹(예: GameOverPopup)은 생성 직후 루트 캔버스로서 화면 해상도에 맞춘
        /// 스케일/크기가 강제로 잡히는데, SetParent(.., false)가 그 값을 유지해 부모 캔버스 스케일과
        /// 중복 적용됐다 → 고해상도 기기에서 UI가 거대해지고 화면 일부만 덮음 (에디터는 기준 해상도에 가까워 안 드러남).
        /// </summary>
        private static void FitToParentLayer(BaseUI ui)
        {
            RectTransform rect = ui.GetComponent<RectTransform>();
            if (rect == null) return;

            rect.localPosition = Vector2.zero;
            if (ui is BaseUIWindow || ui is BaseUIPopup)
            {
                rect.localScale = Vector3.one;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }
        public Transform GetEffectParent()
        {
            return baseCanvasObject.GetEffectParent();
        }
        #endregion InGame_Logic
        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // 팝업만 ESC로 닫기 — 메인 윈도우·GameOverPopup은 ESC로 닫지 않음
                // GameOverPopup을 ESC로 닫으면 GAME_OVER 상태에서 UI만 사라져 게임이 멈추므로 제외
                if (currentUI is BaseUIPopup && !(currentUI is GameOverPopup))
                {
                    currentUI.ExitWindow();
                }
            }
        }
    }
}