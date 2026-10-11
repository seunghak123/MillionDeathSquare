using Seunghak.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Seunghak.UIManager
{
    public class UserEventPopup : BaseUIPopup
    {
        private List<UserEventScrollData> userEvent = new List<UserEventScrollData>();
        //유저 이벤트 관련 팝업
        public override void EnterWindow()
        {
            base.EnterWindow();

            UpdateEvent();
            //현재 이벤트 내역 거르고

        }
        private void UpdateEvent()
        {
            //아래와 같이 JsonDataManager에서 데이터를 가져와서 ScrollData를 만들어 줄것
            //JsonDataManager.Instance.GetUnitDatas()
        }

        public override void ExitWindow()
        {
            base.ExitWindow();
        }
        public override void RestoreWindow()
        {
            base.RestoreWindow();
        }
        public override void StartWindow()
        {
            base.StartWindow();
        }
        public override void RegistEvent()
        {
            base.RegistEvent();
        }
        public override void DeleteRegistedEvent()
        {
            base.DeleteRegistedEvent();
        }
    }

    public class UserEventScrollData : CommonScrollItemData
    {
        public DateTime eventTime;
        public string eventText;


    }
}