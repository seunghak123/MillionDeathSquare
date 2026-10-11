using UnityEngine;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// 기획서(Fossil Merge) 14장 밸런스 초기값. 모두 Inspector에서 조정 가능한 시작점이다.
    /// </summary>
    [System.Serializable]
    public class SuikaRules
    {
        [Header("모드")]
        public float startTime = 120f;
        public int startLives = 3;

        [Header("시간 연장 마일스톤 (누적 점수 → 시간 보너스)")]
        public int[] milestoneScores = { 500, 1500, 3000 };
        public float[] milestoneBonuses = { 20f, 15f, 10f };
        [Tooltip("정해진 마일스톤 이후: 이전 간격 × 이 값씩 증가")]
        public float milestoneGapGrowth = 1.5f;
        [Tooltip("정해진 마일스톤 이후 보너스 (초)")]
        public float milestoneLateBonus = 5f;

        [Header("이어하기 (판당 1회, 보상형 광고)")]
        public float continueTimeBonus = 30f;
        public int continueLives = 1;

        [Header("합체 / 연쇄")]
        [Tooltip("합체로 생긴 화석이 이 시간(초) 안에 또 합체하면 연쇄")]
        public float chainWindow = 2f;
        [Tooltip("연쇄 배율: [1연쇄(일반), 2연쇄, 3연쇄, 4연쇄 이상]")]
        public float[] chainMultipliers = { 1f, 1.2f, 1.5f, 2f };

        [Header("공룡 부활 (최종 단계 완성)")]
        public int dinoBonusScore = 2000;
        public float dinoBonusTime = 15f;
        [Tooltip("공룡 부활 시 날려버리는 화석 최대 단계 (0부터, 2 = 1~3단계)")]
        public int dinoClearMaxTier = 2;
        [Tooltip("부활 연출 시간 (타이머 정지)")]
        public float dinoRevivalDuration = 1.5f;

        [Header("경험치 / 레벨업")]
        public float xpBase = 300f;
        public float xpGrowth = 1.6f;

        [Header("스킬")]
        public int maxHeldSkills = 4;
        [Tooltip("발굴 브러시가 쓸어내는 최대 단계 (0부터, 1 = 1~2단계)")]
        public int brushMaxTier = 1;
        public float earthquakeDuration = 3f;
        public float earthquakeTiltDeg = 8f;
        public float magnetDuration = 1.2f;
        public float magnetAcceleration = 30f;

        [Header("지각 변동 모드")]
        public float tectonicRewardMultiplier = 1.5f;
        [Tooltip("이 각도 이상 기울인 상태에서 합체하면 굴리기 보너스")]
        public float rollingTiltDeg = 3f;
        public float rollingMultiplier = 1.5f;
        public int tectonicUnlockScore = 5000;

        [Header("경제")]
        [Tooltip("코인 = 점수 ÷ 이 값 (지각 변동은 × tectonicRewardMultiplier)")]
        public float coinDivisor = 50f;

        [Header("성능")]
        [Tooltip("동시에 발굴대 위에 있을 수 있는 화석 수 상한 (넘으면 드롭 대기)")]
        public int maxFossils = 40;

        /// <summary>n단계(1부터)가 되는 합체 점수 = n × (n+1) ÷ 2 × 10</summary>
        public static int MergeScore(int tierIndex)
        {
            int n = tierIndex + 1;
            return n * (n + 1) / 2 * 10;
        }

        public float ChainMultiplier(int chain)
        {
            if (chainMultipliers == null || chainMultipliers.Length == 0) return 1f;
            int i = Mathf.Clamp(chain - 1, 0, chainMultipliers.Length - 1);
            return chainMultipliers[i];
        }

        /// <summary>레벨 lv(1부터)에서 다음 레벨까지 필요한 경험치</summary>
        public float XpToNext(int level) => xpBase * Mathf.Pow(xpGrowth, Mathf.Max(0, level - 1));

        /// <summary>i번째(0부터) 마일스톤 점수와 보너스</summary>
        public void GetMilestone(int index, out int score, out float bonus)
        {
            int fixedCount = Mathf.Min(milestoneScores.Length, milestoneBonuses.Length);
            if (index < fixedCount)
            {
                score = milestoneScores[index];
                bonus = milestoneBonuses[index];
                return;
            }

            // 고정 구간 이후: 마지막 간격 × growth 씩 늘려 간다
            int last = fixedCount > 0 ? milestoneScores[fixedCount - 1] : 0;
            int prev = fixedCount > 1 ? milestoneScores[fixedCount - 2] : 0;
            float gap = Mathf.Max(1, last - prev);
            float s = last;
            for (int i = fixedCount; i <= index; i++)
            {
                gap *= milestoneGapGrowth;
                s += gap;
            }
            score = Mathf.RoundToInt(s);
            bonus = milestoneLateBonus;
        }
    }
}
