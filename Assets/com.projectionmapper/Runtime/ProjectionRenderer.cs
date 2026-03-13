using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectionMapper
{
    public static class ProjectionRenderer
    {
        public static void RenderWarpedSurfaces(
            List<ProjectionSurface> surfaces, int displayIndex, Shader warpShader)
        {
            if (warpShader == null) return;
            for (int i = 0; i < surfaces.Count; i++)
            {
                var s = surfaces[i];
                if (!s.enabled || s.targetDisplay != displayIndex) continue;
                RenderTexture tex = s.GetActiveTexture();
                if (tex == null) continue;

                if (s.warpMaterial == null)
                {
                    s.warpMaterial = new Material(warpShader);
                    s.warpMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
                s.UpdateMaterial(s.warpMaterial);

                GL.PushMatrix();
                GL.LoadOrtho();
                s.warpMaterial.SetPass(0);
                GL.Begin(GL.QUADS);
                GL.TexCoord2(s.corners[3].x, s.corners[3].y); GL.Vertex3(s.corners[3].x, s.corners[3].y, 0f);
                GL.TexCoord2(s.corners[2].x, s.corners[2].y); GL.Vertex3(s.corners[2].x, s.corners[2].y, 0f);
                GL.TexCoord2(s.corners[1].x, s.corners[1].y); GL.Vertex3(s.corners[1].x, s.corners[1].y, 0f);
                GL.TexCoord2(s.corners[0].x, s.corners[0].y); GL.Vertex3(s.corners[0].x, s.corners[0].y, 0f);
                GL.End();
                GL.PopMatrix();
            }
        }

        public static void RenderDebugView(
            List<ProjectionSurface> surfaces, int displayIndex, Shader debugShader)
        {
            if (debugShader == null) return;
            var list = new List<ProjectionSurface>();
            foreach (var s in surfaces)
                if (s.targetDisplay == displayIndex && s.enabled) list.Add(s);
            if (list.Count == 0) return;

            int cols = Mathf.CeilToInt(Mathf.Sqrt(list.Count));
            int rows = Mathf.CeilToInt((float)list.Count / cols);
            float cw = 1f / cols, ch = 1f / rows;

            GL.PushMatrix();
            GL.LoadOrtho();
            for (int i = 0; i < list.Count; i++)
            {
                RenderTexture tex = list[i].GetActiveTexture();
                if (tex == null) continue;
                var m = new Material(debugShader) { hideFlags = HideFlags.HideAndDontSave };
                m.SetTexture("_MainTex", tex);
                m.SetFloat("_GridIndex", i);
                m.SetFloat("_GridTotal", list.Count);
                m.SetFloat("_GridCols", cols);
                int c = i % cols, r = i / cols;
                float x0 = c * cw, y0 = 1f - (r + 1) * ch, x1 = x0 + cw, y1 = y0 + ch;
                m.SetPass(0);
                GL.Begin(GL.QUADS);
                GL.TexCoord2(0, 0); GL.Vertex3(x0, y0, 0);
                GL.TexCoord2(1, 0); GL.Vertex3(x1, y0, 0);
                GL.TexCoord2(1, 1); GL.Vertex3(x1, y1, 0);
                GL.TexCoord2(0, 1); GL.Vertex3(x0, y1, 0);
                GL.End();
                Object.DestroyImmediate(m);
            }
            GL.PopMatrix();
        }

        public static void RenderEditOverlays(
            List<ProjectionSurface> surfaces, int displayIndex,
            int selectedIndex, int heldCorner)
        {
            if (surfaces.Count == 0) return;
            int si = Mathf.Clamp(selectedIndex, 0, surfaces.Count - 1);
            var surf = surfaces[si];
            if (surf.targetDisplay != displayIndex) return;

            Shader cs = Shader.Find("Hidden/Internal-Colored");
            if (cs == null) return;
            var mat = new Material(cs) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_Cull", (int)CullMode.Off);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_ZTest", (int)CompareFunction.Always);

            GL.PushMatrix();
            GL.LoadOrtho();
            mat.SetPass(0);

            Color[] cc = {
                new Color(1,.2f,.2f,.9f), new Color(.2f,1,.2f,.9f),
                new Color(.2f,.2f,1,.9f), new Color(1,1,.2f,.9f)
            };

            // Quad outline
            GL.Begin(GL.LINES);
            GL.Color(new Color(1,1,1,.6f));
            for (int e = 0; e < 4; e++)
            {
                int n = (e + 1) % 4;
                GL.Vertex3(surf.corners[e].x, surf.corners[e].y, 0);
                GL.Vertex3(surf.corners[n].x, surf.corners[n].y, 0);
            }
            GL.End();

            // Corner handles
            float ar = Screen.width > 0 ? (float)Screen.width / Screen.height : 1f;
            for (int c = 0; c < 4; c++)
            {
                Vector2 p = surf.corners[c];
                Color col = heldCorner == c ? Color.white : cc[c];
                float hs = heldCorner == c ? .018f : .012f;
                GL.Begin(GL.TRIANGLES);
                GL.Color(col);
                for (int seg = 0; seg < 16; seg++)
                {
                    float a0 = seg / 16f * Mathf.PI * 2f;
                    float a1 = (seg + 1) / 16f * Mathf.PI * 2f;
                    GL.Vertex3(p.x, p.y, 0);
                    GL.Vertex3(p.x + Mathf.Cos(a0) * hs / ar, p.y + Mathf.Sin(a0) * hs, 0);
                    GL.Vertex3(p.x + Mathf.Cos(a1) * hs / ar, p.y + Mathf.Sin(a1) * hs, 0);
                }
                GL.End();
            }
            GL.PopMatrix();
            Object.DestroyImmediate(mat);
        }
    }
}
