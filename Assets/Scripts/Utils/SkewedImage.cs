using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

public class SkewedImage : Image
{
    //����ũ���� ���� ���� �ʿ�
    public Vector2 skewVec2 = new Vector2(20, 0);
    public Mask skewMask = null;

    //��Ʋ�󽺿� ���������� ó�� �ʿ�
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        base.OnPopulateMesh(vh);

        var r = GetPixelAdjustedRect();
        var v = new Vector4(r.x, r.y, r.x + r.width, r.y + r.height);

        Color32 color32 = color;
        
        // 아틀라스에서 UV 좌표 가져오기
        Vector4 outer = overrideSprite != null ? DataUtility.GetOuterUV(overrideSprite) : Vector4.zero;
        Vector2 uv0 = new Vector2(outer.x, outer.y);
        Vector2 uv1 = new Vector2(outer.x, outer.w);
        Vector2 uv2 = new Vector2(outer.z, outer.w);
        Vector2 uv3 = new Vector2(outer.z, outer.y);

        vh.Clear();

        // 마스크 기울기 적용
        if (skewMask != null)
        {
            if (skewMask.gameObject == this.gameObject)
            {
                // 현재 오브젝트가 마스크인 경우, RectTransform을 수정하여 기울기 적용
                RectTransform rectTransform = skewMask.rectTransform;
                Vector3[] vertices = new Vector3[4];
                rectTransform.GetLocalCorners(vertices);
                
                // 새로운 메시 생성
                Mesh mesh = new Mesh();
                mesh.vertices = new Vector3[] {
                    vertices[0] + new Vector3(-skewVec2.x, -skewVec2.y, 0),  // 좌하단
                    vertices[1] + new Vector3(skewVec2.x, 0, 0),             // 좌상단
                    vertices[2] + new Vector3(skewVec2.x, skewVec2.y, 0),    // 우상단
                    vertices[3] + new Vector3(-skewVec2.x, 0, 0)             // 우하단
                };
                mesh.triangles = new int[] { 0, 1, 2, 2, 3, 0 };
                
                // 마스크 컴포넌트의 RectMask2D 업데이트
                if (skewMask.GetComponent<RectMask2D>() != null)
                {
                    skewMask.GetComponent<RectMask2D>().padding = 
                        new Vector4(-skewVec2.x, 0, skewVec2.x, skewVec2.y);
                }
            }
            else
            {
                // 다른 오브젝트인 경우 회전으로 처리
                skewMask.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(skewVec2.x, r.height) * Mathf.Rad2Deg);
            }
        }

        vh.AddVert(new Vector3(v.x - skewVec2.x, v.y - skewVec2.y), color32, uv0);
        vh.AddVert(new Vector3(v.x + skewVec2.x, v.w), color32, uv1);
        vh.AddVert(new Vector3(v.z + skewVec2.x, v.w + skewVec2.y), color32, uv2);
        vh.AddVert(new Vector3(v.z - skewVec2.x, v.y), color32, uv3);

        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }
}