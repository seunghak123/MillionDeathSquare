using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Seunghak.Common
{
    public class TimeManager : UnitySingleton<TimeManager>
    {
        //서버에서 시간을 받아와서, 로컬 시간을 따로 계산해, 현재 시간을 추정해주는 매니저
        private DateTime currentDateTime;
        private DateTime serverDateTime;
        private long gapTime;

        public DateTime ServerTime
        {
            get { return  DateTime.Now.Add(TimeSpan.FromMilliseconds(serverDateTime.Ticks - currentDateTime.Ticks)); }
        }

    }
}
