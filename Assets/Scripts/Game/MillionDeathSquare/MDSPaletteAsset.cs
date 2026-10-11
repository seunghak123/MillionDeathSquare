using System;
using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>팔레트 하나의 Unity 색 (코어 PaletteDef에서 생성, 에셋에서 덮어쓰기 가능)</summary>
    [Serializable]
    public struct MDSPaletteColors
    {
        public string name;
        public Color background, floor, floorAlt, wall, player, eye, obstacle, coin, goal, checkpoint, ice, conveyor, vanishing, keyRed, keyGreen, keyBlue;

        public static Color ToColor(uint rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        public static MDSPaletteColors FromCore(PaletteDef p)
        {
            return new MDSPaletteColors
            {
                name = p.Name,
                background = ToColor(p.Background),
                floor = ToColor(p.Floor),
                floorAlt = ToColor(p.FloorAlt),
                wall = ToColor(p.Wall),
                player = ToColor(p.Player),
                eye = ToColor(p.Eye),
                obstacle = ToColor(p.Obstacle),
                coin = ToColor(p.Coin),
                goal = ToColor(p.Goal),
                checkpoint = ToColor(p.Checkpoint),
                ice = ToColor(p.Ice),
                conveyor = ToColor(p.Conveyor),
                vanishing = ToColor(p.Vanishing),
                keyRed = ToColor(p.KeyRed),
                keyGreen = ToColor(p.KeyGreen),
                keyBlue = ToColor(p.KeyBlue),
            };
        }

        public Color Key(KeyColor c)
        {
            switch (c)
            {
                case KeyColor.Red: return keyRed;
                case KeyColor.Green: return keyGreen;
                default: return keyBlue;
            }
        }
    }

    /// <summary>8종 팔레트 에셋 (셋업 툴이 코어 값으로 생성: Assets/BaseSources/MillionDeathSquare/MDSPalettes.asset)</summary>
    [CreateAssetMenu(menuName = "Million Death Square/Palette Set", fileName = "MDSPalettes")]
    public sealed class MDSPaletteAsset : ScriptableObject
    {
        public MDSPaletteColors[] palettes = new MDSPaletteColors[0];

        public MDSPaletteColors Get(int id)
        {
            if (palettes != null && id >= 0 && id < palettes.Length) return palettes[id];
            return MDSPaletteColors.FromCore(Palettes.Get(id));
        }

        public void ResetToCore()
        {
            palettes = new MDSPaletteColors[Palettes.Count];
            for (int i = 0; i < palettes.Length; i++) palettes[i] = MDSPaletteColors.FromCore(Palettes.Get(i));
        }
    }
}
