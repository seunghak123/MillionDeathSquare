using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 입력 추상화 (기획서 8.1): 방향키·WASD, 게임패드 왼쪽 스틱·방향 패드(레거시 Input Manager 축), R = 처음부터.
    /// 게임 코드는 이 클래스만 읽는다 — 모바일판(터치 패드)은 VirtualMove로 같은 경로에 들어온다.
    /// 방향 패드 축(MDS_DPadX/Y)은 셋업 툴이 Input Manager에 추가한다 (없으면 건너뜀).
    /// </summary>
    public static class MDSInput
    {
        private const float STICK_DEADZONE = 0.5f;
        private static bool _dpadChecked;
        private static bool _hasDpad;

        /// <summary>화면 버튼/터치 패드 입력 (모바일판·테스트용). 매 프레임 채워 넣는다.</summary>
        public static InputBits VirtualMove;
        public static bool VirtualRestart;

        public static InputBits ReadMove()
        {
            InputBits bits = VirtualMove;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) bits |= InputBits.Up;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) bits |= InputBits.Down;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) bits |= InputBits.Left;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) bits |= InputBits.Right;

            // 게임패드 왼쪽 스틱 (기본 Horizontal/Vertical 축에 조이스틱 축이 포함됨). 키보드는 위에서 이미 처리.
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            AddAxis(ref bits, h, v);

            if (HasDpad())
            {
                AddAxis(ref bits, Input.GetAxisRaw("MDS_DPadX"), Input.GetAxisRaw("MDS_DPadY"));
            }

            // 반대 방향 동시 입력은 상쇄
            if ((bits & (InputBits.Left | InputBits.Right)) == (InputBits.Left | InputBits.Right)) bits &= ~(InputBits.Left | InputBits.Right);
            if ((bits & (InputBits.Up | InputBits.Down)) == (InputBits.Up | InputBits.Down)) bits &= ~(InputBits.Up | InputBits.Down);
            return bits;
        }

        private static void AddAxis(ref InputBits bits, float h, float v)
        {
            if (h > STICK_DEADZONE) bits |= InputBits.Right;
            else if (h < -STICK_DEADZONE) bits |= InputBits.Left;
            if (v > STICK_DEADZONE) bits |= InputBits.Up;
            else if (v < -STICK_DEADZONE) bits |= InputBits.Down;
        }

        private static bool HasDpad()
        {
            if (_dpadChecked) return _hasDpad;
            _dpadChecked = true;
            try
            {
                Input.GetAxisRaw("MDS_DPadX");
                Input.GetAxisRaw("MDS_DPadY");
                _hasDpad = true;
            }
            catch (System.ArgumentException)
            {
                _hasDpad = false;
            }
            return _hasDpad;
        }

        /// <summary>R 키 / 게임패드 Y(3번) — 처음부터</summary>
        public static bool RestartPressed()
        {
            bool v = VirtualRestart;
            VirtualRestart = false;
            return v || Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.JoystickButton3);
        }

        /// <summary>Esc / 게임패드 Start(7번) — 일시정지</summary>
        public static bool PausePressed()
        {
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.P);
        }

        /// <summary>결과 화면 등에서 "다음/확인" — Enter, Space, 게임패드 A(0번)</summary>
        public static bool ConfirmPressed()
        {
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
        }
    }
}
