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

    /// <summary>
    /// 도형 스프라이트 묶음 (셋업 툴이 PNG로 생성해 연결). 비어 있는 칸은 MDSShapes가 런타임에 만들어 채운다.
    /// 그림 리소스 최소화 (기획서 3장): 도형 + 색 + 눈 표정만.
    /// </summary>
    [CreateAssetMenu(menuName = "Million Death Square/Sprite Library", fileName = "MDSSprites")]
    public sealed class MDSSpriteLibrary : ScriptableObject
    {
        public Sprite square;
        public Sprite roundedSquare;
        public Sprite circle;
        public Sprite ring;
        public Sprite eyeWhite;
        public Sprite pupil;
        public Sprite eyeHappy;
        public Sprite eyeClosed;
        public Sprite brow;
        public Sprite arrow;
        public Sprite key;
        public Sprite lockIcon;
        public Sprite flag;
        public Sprite glow;
        public Sprite sparkle;
        public Sprite iceShine;
        public Sprite particle;

        /// <summary>비어 있는 스프라이트를 런타임 생성본으로 채운 사본 (에셋 자체는 바꾸지 않음)</summary>
        public static MDSSpriteLibrary ResolveOrCreate(MDSSpriteLibrary source)
        {
            MDSSpriteLibrary lib = source != null ? Instantiate(source) : CreateInstance<MDSSpriteLibrary>();
            if (lib.square == null) lib.square = MDSShapes.CreateSprite(MDSShapes.Shape.Square);
            if (lib.roundedSquare == null) lib.roundedSquare = MDSShapes.CreateSprite(MDSShapes.Shape.RoundedSquare);
            if (lib.circle == null) lib.circle = MDSShapes.CreateSprite(MDSShapes.Shape.Circle);
            if (lib.ring == null) lib.ring = MDSShapes.CreateSprite(MDSShapes.Shape.Ring);
            if (lib.eyeWhite == null) lib.eyeWhite = MDSShapes.CreateSprite(MDSShapes.Shape.Circle);
            if (lib.pupil == null) lib.pupil = MDSShapes.CreateSprite(MDSShapes.Shape.Circle);
            if (lib.eyeHappy == null) lib.eyeHappy = MDSShapes.CreateSprite(MDSShapes.Shape.Arc);
            if (lib.eyeClosed == null) lib.eyeClosed = MDSShapes.CreateSprite(MDSShapes.Shape.Bar);
            if (lib.brow == null) lib.brow = MDSShapes.CreateSprite(MDSShapes.Shape.Bar);
            if (lib.arrow == null) lib.arrow = MDSShapes.CreateSprite(MDSShapes.Shape.Arrow);
            if (lib.key == null) lib.key = MDSShapes.CreateSprite(MDSShapes.Shape.Key);
            if (lib.lockIcon == null) lib.lockIcon = MDSShapes.CreateSprite(MDSShapes.Shape.Lock);
            if (lib.flag == null) lib.flag = MDSShapes.CreateSprite(MDSShapes.Shape.Flag);
            if (lib.glow == null) lib.glow = MDSShapes.CreateSprite(MDSShapes.Shape.Glow);
            if (lib.sparkle == null) lib.sparkle = MDSShapes.CreateSprite(MDSShapes.Shape.Sparkle);
            if (lib.iceShine == null) lib.iceShine = MDSShapes.CreateSprite(MDSShapes.Shape.IceShine);
            if (lib.particle == null) lib.particle = MDSShapes.CreateSprite(MDSShapes.Shape.Square);
            return lib;
        }
    }

    /// <summary>캠페인 스테이지 덮어쓰기 (에디터로 만든 맵의 공유 코드를 넣으면 코어 기본 데이터 대신 사용)</summary>
    [CreateAssetMenu(menuName = "Million Death Square/Campaign", fileName = "MDSCampaign")]
    public sealed class MDSCampaignAsset : ScriptableObject
    {
        [Serializable]
        public struct StageOverride
        {
            public string stageId;     // "2-3"
            [TextArea(2, 6)] public string shareCode;
        }

        [Tooltip("비어 있으면 코어(Campaign) 기본 스테이지를 쓴다")]
        public StageOverride[] overrides = new StageOverride[0];

        public CampaignStage GetStage(int world, int stage)
        {
            CampaignStage s = Campaign.Get(world, stage);
            string id = Campaign.StageId(world, stage);
            if (overrides != null)
            {
                for (int i = 0; i < overrides.Length; i++)
                {
                    if (overrides[i].stageId != id || string.IsNullOrEmpty(overrides[i].shareCode)) continue;
                    MapData map;
                    string error;
                    if (MapSerializer.TryParseShareCode(overrides[i].shareCode, out map, out error) && MapValidator.Validate(map).IsValid)
                        return new CampaignStage { World = world, Stage = stage, Map = map, IsPlaceholder = false };
                    Debug.LogWarning("[MDSCampaignAsset] " + id + " 덮어쓰기 공유 코드가 잘못되어 기본 맵을 씁니다: " + error);
                }
            }
            return s;
        }
    }
}
