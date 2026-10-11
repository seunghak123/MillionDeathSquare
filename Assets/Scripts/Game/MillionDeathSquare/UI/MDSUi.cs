using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>화면 스크립트 공용 도우미 (null 안전)</summary>
    public static class MDSUi
    {
        public static void SetText(TMP_Text t, string s)
        {
            if (t != null && t.text != s) t.text = s;
        }

        public static void Bind(Button b, UnityAction a)
        {
            if (b == null) return;
            b.onClick.RemoveListener(a);
            b.onClick.AddListener(a);
        }

        public static void SetActive(Component c, bool on)
        {
            if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
        }

        public static void SetActive(GameObject g, bool on)
        {
            if (g != null && g.activeSelf != on) g.SetActive(on);
        }

        public static void SetInteractable(Button b, bool on)
        {
            if (b != null) b.interactable = on;
        }

        public static void SetLabel(Button b, string s)
        {
            if (b == null) return;
            TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
            SetText(t, s);
        }

        public static void Tint(Button b, Color c)
        {
            if (b == null) return;
            Image img = b.targetGraphic as Image;
            if (img != null) img.color = c;
        }

        /// <summary>틱 → "12.34초" / "1:02.50"</summary>
        public static string FormatTicks(int ticks)
        {
            if (ticks < 0) return "-";
            int centis = ticks * 100 / Fixed.TICKS_PER_SECOND;
            int sec = centis / 100;
            int cs = centis % 100;
            if (sec < 60) return sec + "." + cs.ToString("00") + "초";
            return (sec / 60) + ":" + (sec % 60).ToString("00") + "." + cs.ToString("00");
        }

        public static void CopyToClipboard(string s) { GUIUtility.systemCopyBuffer = s ?? ""; }

        public static string ReadClipboard() { return GUIUtility.systemCopyBuffer ?? ""; }

        public static readonly Color Selected = new Color(1f, 0.82f, 0.3f);
        public static readonly Color Normal = new Color(0.25f, 0.25f, 0.35f);
        public static readonly Color Locked = new Color(0.4f, 0.4f, 0.4f, 0.6f);
    }

    /// <summary>맵 미리보기 텍스처 (칸당 픽셀 몇 개). 멀티방·맵 상세·창작마당 미리보기 이미지에 쓴다 — 메뉴에서만 생성.</summary>
    public static class MDSMapPreview
    {
        public static Texture2D Render(MapData map, MDSPaletteColors pal, int pixelsPerCell = 8)
        {
            int w = map.Width * pixelsPerCell, h = map.Height * pixelsPerCell;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var px = new Color32[w * h];
            for (int cy = 0; cy < map.Height; cy++)
            {
                for (int cx = 0; cx < map.Width; cx++)
                {
                    Color c;
                    switch (map.GetTile(cx, cy))
                    {
                        case TileType.Empty: c = pal.background; break;
                        case TileType.Wall: c = pal.wall; break;
                        case TileType.Ice: c = pal.ice; break;
                        case TileType.Vanishing: c = pal.vanishing; break;
                        case TileType.Floor: c = ((cx + cy) & 1) == 0 ? pal.floor : pal.floorAlt; break;
                        default: c = pal.conveyor; break;
                    }
                    Fill(px, w, h, cx, cy, pixelsPerCell, c, 0);
                }
            }
            foreach (CellPos g in map.Goals) Fill(px, w, h, g.X, g.Y, pixelsPerCell, pal.goal, 0);
            foreach (CellPos c in map.Checkpoints) Fill(px, w, h, c.X, c.Y, pixelsPerCell, pal.checkpoint, 0);
            foreach (CellPos c in map.Coins) Fill(px, w, h, c.X, c.Y, pixelsPerCell, pal.coin, pixelsPerCell / 4);
            foreach (KeyItem k in map.Keys) Fill(px, w, h, k.Cell.X, k.Cell.Y, pixelsPerCell, pal.Key(k.Color), pixelsPerCell / 4);
            foreach (DoorItem d in map.Doors) Fill(px, w, h, d.Cell.X, d.Cell.Y, pixelsPerCell, pal.Key(d.Color), 0);
            Fill(px, w, h, map.Start.X, map.Start.Y, pixelsPerCell, pal.player, pixelsPerCell / 5);
            foreach (ObstacleDef o in map.Obstacles)
            {
                if (o.Points.Length == 0) continue;
                int ox, oy;
                ObstacleMotion.Evaluate(o, 0, out ox, out oy);
                Fill(px, w, h, Fixed.CellOf(ox), Fixed.CellOf(oy), pixelsPerCell, pal.obstacle, pixelsPerCell / 4);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        private static void Fill(Color32[] px, int w, int h, int cx, int cy, int s, Color c, int inset)
        {
            Color32 col = c;
            for (int y = inset; y < s - inset; y++)
            {
                int py = h - 1 - (cy * s + y); // 텍스처는 아래가 0
                if (py < 0 || py >= h) continue;
                for (int x = inset; x < s - inset; x++)
                {
                    int pxx = cx * s + x;
                    if (pxx < 0 || pxx >= w) continue;
                    px[py * w + pxx] = col;
                }
            }
        }
    }
}
