using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
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
}
