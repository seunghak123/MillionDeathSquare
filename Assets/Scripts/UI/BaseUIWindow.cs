using System.Collections;
using System.Collections.Generic;
using Seunghak.Platform;
using UnityEngine;

namespace Seunghak.UIManager
{
    public class BaseUIWindow : BaseUI
    {
        // 이 창에 이미 적용된 플랫폼 여백 (플랫폼 생성 전에 열린 창도 다음 진입 때 차이만큼 보정)
        private SafeAreaInset _appliedSafeArea = SafeAreaInset.Zero;

        public override void EnterWindow()
        {
            base.EnterWindow();
            _appliedSafeArea = PlatformSafeArea.Apply(transform, _appliedSafeArea);
        }
    }
}
