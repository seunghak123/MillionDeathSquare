using Cysharp.Threading.Tasks;
using Seunghak.UIManager;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingPopup : BaseUIPopup
{
    [SerializeField] private TextMeshProUGUI loadingPercentLabel = null;
    [SerializeField] private List<GameObject> animationList = new List<GameObject>();
    [SerializeField] private float fixedWaitingTime = 10.0f;
    [SerializeField] private Slider progressSlider = null;

    private float currentWaitingTime = 0.0f;
    private bool unitaskSetting = false;
    private UniTask updateUnitask ;
    private IEnumerator updateCourutine = null;
    private float taskPercent = 0.0f;
    public override void EnterWindow()
    {
        progressSlider.value = 0;
        loadingPercentLabel.text = $"0%";
        unitaskSetting = false;
        updateCourutine = null;
    }
    public void SetLoading(float percent = 0)
    {
        loadingPercentLabel.text = $"{percent.ToString("N0")}%";
    }
    public void SetTaskProgressPercent(float percent = 0)
    {
        taskPercent = percent;
    }
    public void SetUpdateFuction(UniTask updateTask)
    {
        unitaskSetting = true;
        updateUnitask = updateTask;
    }

    public void SetUpdateFuction(IEnumerator updateTask)
    {
        updateCourutine = updateTask;
    }
    public void RestartWaiting()
    {
        currentWaitingTime = 0.0f;

        for (int i = 0; i < animationList.Count; i++)
        {
            animationList[i].SetActive(false);
        }

        if (animationList.Count > 0)
        {
            animationList[Random.Range(0, animationList.Count)].SetActive(true);
        }
    }
    private void Update()
    {
        currentWaitingTime += Time.deltaTime;
    }
    public bool isEnd
    {
        get
        {
            //90%까지는 빠르게
            //90~100 은 천천히
            if(unitaskSetting || updateCourutine != null)
            {
                progressSlider.value = currentWaitingTime / fixedWaitingTime > taskPercent ? taskPercent : currentWaitingTime / fixedWaitingTime;
            }
            else
            {
                progressSlider.value = currentWaitingTime / fixedWaitingTime;
            }

            SetLoading(progressSlider.value * 100);
            if (currentWaitingTime > fixedWaitingTime)
            {
                unitaskSetting = false;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
