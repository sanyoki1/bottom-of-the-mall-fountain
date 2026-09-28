// Camera post effect for the built-in pipeline: bloom + light grade. Uses Hidden/WE/Bloom.
using UnityEngine;

namespace WishExtractor.View
{
    [RequireComponent(typeof(Camera))]
    public sealed class BloomFX : MonoBehaviour
    {
        public float Threshold = 1.15f;
        public float SoftKnee = 0.45f;
        public float Intensity = 0.45f;
        public int Iterations = 5;
        public float Saturation = 1.12f;
        public float Contrast = 1.07f;
        public float Vignette = 0.32f;
        public bool Enabled = true;

        Material mat;
        readonly RenderTexture[] chain = new RenderTexture[16];

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null)
            {
                var sh = Shader.Find("Hidden/WE/Bloom");
                if (sh == null || !sh.isSupported) { Graphics.Blit(src, dst); return; }
                mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }
            float knee = Threshold * SoftKnee + 1e-5f;
            mat.SetVector("_Filter", new Vector4(Threshold, Threshold - knee, 2f * knee, 0.25f / knee));
            mat.SetFloat("_Intensity", Enabled ? Intensity : 0f);
            mat.SetFloat("_Saturation", Saturation);
            mat.SetFloat("_Contrast", Contrast);
            mat.SetFloat("_Vignette", Vignette);

            if (!Enabled) { Graphics.Blit(src, dst); return; }
            int w = src.width / 2, h = src.height / 2;
            var fmt = src.format;
            int i = 0;
            {
                var cur = chain[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(src, cur, mat, 0);
                var last = cur;
                for (i = 1; i < Iterations; i++)
                {
                    w /= 2; h /= 2;
                    if (h < 2) break;
                    cur = chain[i] = RenderTexture.GetTemporary(w, h, 0, fmt);
                    Graphics.Blit(last, cur, mat, 1);
                    last = cur;
                }
                for (i -= 2; i >= 0; i--)
                {
                    cur = chain[i];
                    chain[i] = null;
                    Graphics.Blit(last, cur, mat, 2);
                    RenderTexture.ReleaseTemporary(last);
                    last = cur;
                }
                mat.SetTexture("_SourceTex", src);
                Graphics.Blit(last, dst, mat, 3);
                RenderTexture.ReleaseTemporary(last);
            }
        }
    }
}
