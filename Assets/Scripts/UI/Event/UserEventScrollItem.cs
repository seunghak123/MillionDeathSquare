using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Seunghak.UIManager;
using System;

public class UserEventScrollItem : OHScrollView.IInfiniteScrollSetup<UserEventScrollData>
{
    //이벤트 Window로 가는 게 있고, 해당 UI에서 바로 보여줄 게 잇고,
    [SerializeField] private Button eventButton;
    [SerializeField] private TextMeshProUGUI eventNameLabel;
    [SerializeField] private Image eventButtonImage;
    
    public void OnPostSetupItems()
    {
        
    }

    public void OnUpdateItem( GameObject obj, UserEventScrollData infos)
    {

    }

    public void SetAction(Action<UserEventScrollData> action)
    {

    }
}
