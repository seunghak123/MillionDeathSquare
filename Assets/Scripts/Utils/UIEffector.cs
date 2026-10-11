using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

public enum E_UIEFFECT_DIR
{
    Left, Right, Top, Bottom
}

public static class UIEffector
{
    private static CancellationTokenSource cts;

    const float fadeTime = 0.5f;

    /// <summary>
    /// 기존 세팅된 위치로 살짝 이동하며 선명해짐 (투명도)
    /// </summary>
    /// <param name="dir">이동하는 방향</param>
    public static void OpenUIFadeMoveEffect(E_UIEFFECT_DIR moveDir)
    {

    }

    private static async UniTaskVoid IFadeMoveEffect(E_UIEFFECT_DIR moveDir)
    {
        float currTime = 0.0f;

        // 대기

        while(currTime < fadeTime)
        {
            currTime += Time.deltaTime;

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: cts.Token);
        }
    }
}
