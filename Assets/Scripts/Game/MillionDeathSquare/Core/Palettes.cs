namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 색 팔레트 (기획서 3.3 / 12.1: 월드 5개 + 에디터 전용 3개 = 8종). 색은 0xRRGGBB.
    /// 같은 도형이 팔레트만 바뀌어 월드마다 다른 분위기를 낸다. Unity 쪽 MDSPaletteAsset이 이 값으로 초기화되며 덮어쓸 수 있다.
    /// </summary>
    public sealed class PaletteDef
    {
        public string Name;
        public uint Background;
        public uint Floor;
        public uint FloorAlt;
        public uint Wall;
        public uint Player;
        public uint Eye;
        public uint Obstacle;
        public uint Coin;
        public uint Goal;
        public uint Checkpoint;
        public uint Ice;
        public uint Conveyor;
        public uint Vanishing;
        public uint KeyRed;
        public uint KeyGreen;
        public uint KeyBlue;

        public uint KeyColorOf(KeyColor c)
        {
            switch (c)
            {
                case KeyColor.Red: return KeyRed;
                case KeyColor.Green: return KeyGreen;
                default: return KeyBlue;
            }
        }
    }

    public static class Palettes
    {
        private static readonly PaletteDef[] All =
        {
            new PaletteDef { Name = "파스텔", Background = 0xB9B3E6, Floor = 0xF7F4FF, FloorAlt = 0xE9E4FA, Wall = 0x8E86C9, Player = 0xFF6B6B, Eye = 0x2B2340, Obstacle = 0x4D7CFE, Coin = 0xFFD84D, Goal = 0x8BE08B, Checkpoint = 0xB8F0B8, Ice = 0xBFEFFF, Conveyor = 0xD6CCF5, Vanishing = 0xFFC9E0, KeyRed = 0xFF5E5E, KeyGreen = 0x3CC46B, KeyBlue = 0x4C8DFF },
            new PaletteDef { Name = "노을", Background = 0x5B2A4E, Floor = 0xFFE3C2, FloorAlt = 0xFFD2A6, Wall = 0xB04A5A, Player = 0xFF3D3D, Eye = 0x2A1020, Obstacle = 0x3E4CC9, Coin = 0xFFE066, Goal = 0x7BD88F, Checkpoint = 0xC9F2C1, Ice = 0xC8F0FF, Conveyor = 0xF2B38B, Vanishing = 0xFF9EC4, KeyRed = 0xE63946, KeyGreen = 0x2A9D8F, KeyBlue = 0x457B9D },
            new PaletteDef { Name = "숲", Background = 0x1F3B2D, Floor = 0xE4F1D9, FloorAlt = 0xD3E7C3, Wall = 0x4F7A4A, Player = 0xFF6F3C, Eye = 0x16241B, Obstacle = 0x3366CC, Coin = 0xF6D743, Goal = 0x9BE15D, Checkpoint = 0xC4ECA8, Ice = 0xBDE9F7, Conveyor = 0xBFD8A8, Vanishing = 0xF4C6A5, KeyRed = 0xD7263D, KeyGreen = 0x1B998B, KeyBlue = 0x2E86DE },
            new PaletteDef { Name = "겨울", Background = 0x2C3E63, Floor = 0xF4F8FF, FloorAlt = 0xE3ECFA, Wall = 0x7D93C2, Player = 0xFF4F6E, Eye = 0x18223A, Obstacle = 0x283593, Coin = 0xFFD54F, Goal = 0x81D4A1, Checkpoint = 0xC8EED7, Ice = 0xA9E4FF, Conveyor = 0xC5D2EE, Vanishing = 0xE6C8F2, KeyRed = 0xEF5350, KeyGreen = 0x43A047, KeyBlue = 0x1E88E5 },
            new PaletteDef { Name = "네온", Background = 0x0B0B1A, Floor = 0x1C1C33, FloorAlt = 0x24243F, Wall = 0x3A3A6B, Player = 0xFF2E88, Eye = 0xFFFFFF, Obstacle = 0x00E5FF, Coin = 0xFFEA00, Goal = 0x39FF14, Checkpoint = 0x1F6B3A, Ice = 0x2B5C8A, Conveyor = 0x3A2B6B, Vanishing = 0x6B2B5C, KeyRed = 0xFF1744, KeyGreen = 0x00E676, KeyBlue = 0x2979FF },
            new PaletteDef { Name = "모노", Background = 0x202020, Floor = 0xEDEDED, FloorAlt = 0xDADADA, Wall = 0x5A5A5A, Player = 0xE53935, Eye = 0x111111, Obstacle = 0x1E1E1E, Coin = 0xF2C230, Goal = 0x9E9E9E, Checkpoint = 0xC7C7C7, Ice = 0xCFE8F3, Conveyor = 0xBDBDBD, Vanishing = 0xA8A8A8, KeyRed = 0xC62828, KeyGreen = 0x2E7D32, KeyBlue = 0x1565C0 },
            new PaletteDef { Name = "사탕", Background = 0xFFB3D9, Floor = 0xFFF5FB, FloorAlt = 0xFFE6F3, Wall = 0xF06FB0, Player = 0x7C4DFF, Eye = 0x2D0A3D, Obstacle = 0xFF5252, Coin = 0xFFEB3B, Goal = 0x69F0AE, Checkpoint = 0xB9F6CA, Ice = 0xB3E5FC, Conveyor = 0xF8BBD0, Vanishing = 0xE1BEE7, KeyRed = 0xFF1744, KeyGreen = 0x00C853, KeyBlue = 0x2962FF },
            new PaletteDef { Name = "바다", Background = 0x03304A, Floor = 0xDDF3F5, FloorAlt = 0xC9EBEF, Wall = 0x16697A, Player = 0xFFB400, Eye = 0x062A33, Obstacle = 0xD7263D, Coin = 0xFFF275, Goal = 0x7AE582, Checkpoint = 0xBEEFC4, Ice = 0xA2E8FF, Conveyor = 0x9CC9CF, Vanishing = 0xF5B8A0, KeyRed = 0xE4572E, KeyGreen = 0x29BF12, KeyBlue = 0x2E86AB },
        };

        public static int Count { get { return All.Length; } }

        public static PaletteDef Get(int id)
        {
            if (id < 0 || id >= All.Length) id = 0;
            return All[id];
        }
    }
}
