using System;
using System.IO;
using Game.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.EditorTools
{
    // Game > Systems > Generate UI Art. Draws the UI's textures in code (scanlines, vignette,
    // grain, a Pac-Man life icon, the classic ghost, a frame), builds the Press Start 2P font
    // asset and its neon material, and creates UiTheme. Never overwrites an existing file, so a
    // hand-edited texture or theme survives; delete a file to regenerate it.
    public static class UiArtGenerator
    {
        const string ArtFolder = "Assets/_Project/Art/UI";
        const string FontFile = "Assets/_Project/Art/Fonts/PressStart2P-Regular.ttf";
        const string FontAssetPath = ArtFolder + "/PressStart2P SDF.asset";
        const string NeonMaterialPath = ArtFolder + "/PressStart2P Neon.mat";
        const string ThemePath = "Assets/_Project/ScriptableObjects/Configs/UiTheme.asset";

        public const string Scanlines = ArtFolder + "/Scanlines.png";
        public const string Vignette = ArtFolder + "/Vignette.png";
        public const string Grain = ArtFolder + "/Grain.png";
        public const string LifeIcon = ArtFolder + "/LifeIcon.png";
        public const string GhostBody = ArtFolder + "/GhostBody.png";
        public const string GhostEyes = ArtFolder + "/GhostEyes.png";
        public const string Frame = ArtFolder + "/Frame.png";

        // The arcade ghost, 14 x 14. # = body (white, tinted per ghost).
        static readonly string[] GhostBodyPixels =
        {
            ".....####.....",
            "...########...",
            "..##########..",
            ".############.",
            ".############.",
            ".############.",
            "##############",
            "##############",
            "##############",
            "##############",
            "##############",
            "##############",
            "##.###..###.##",
            "#...##..##...#"
        };

        // Its eyes, drawn untinted on top: W = white, B = blue pupil (looking left).
        static readonly string[] GhostEyePixels =
        {
            "..............",
            "..............",
            "..............",
            "...WW....WW...",
            "..WWWW..WWWW..",
            "..BBWW..BBWW..",
            "..BBWW..BBWW..",
            "...WW....WW...",
            "..............",
            "..............",
            "..............",
            "..............",
            "..............",
            ".............."
        };

        [MenuItem("Game/Systems/Generate UI Art")]
        public static void GenerateMenu()
        {
            Generate();
            Debug.Log("UI art is in place.");
        }

        public static UiTheme Generate()
        {
            if (!AssetDatabase.IsValidFolder(ArtFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "UI");

            Texture(Scanlines, 4, 3, (x, y) => y == 2 ? Color.black : Color.clear, repeat: true, sprite: false);
            Texture(Vignette, 256, 256, VignettePixel, repeat: false, sprite: true, point: false);
            Texture(Grain, 128, 128, GrainPixel, repeat: true, sprite: false);
            Texture(LifeIcon, 16, 16, PacManPixel, repeat: false, sprite: true);
            Texture(GhostBody, 14, 14, (x, y) => Pixel(GhostBodyPixels, x, y) == '#' ? Color.white : Color.clear, repeat: false, sprite: true);
            Texture(GhostEyes, 14, 14, EyePixel, repeat: false, sprite: true);
            Texture(Frame, 12, 12, (x, y) => x < 2 || y < 2 || x > 9 || y > 9 ? Color.white : Color.clear,
                repeat: false, sprite: true, border: 3);

            TMP_FontAsset font = FontAsset();
            Material neon = NeonMaterial(font);

            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UiTheme>();
                theme.Assign(font, neon);
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            AssetDatabase.SaveAssets();
            return theme;
        }

        public static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        public static Texture2D LoadTexture(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        // A static atlas of printable ASCII, sampled at 64 pt (a multiple of the font's 8 px grid)
        // so the pixel edges land on whole texels. Static, so playing never dirties the asset.
        static TMP_FontAsset FontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
                return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>(FontFile);
            if (source == null)
                throw new InvalidOperationException($"{FontFile} is missing.");

            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, false);
            font.name = "PressStart2P SDF";

            var ascii = new char[95];
            for (int i = 0; i < ascii.Length; i++)
                ascii[i] = (char)(32 + i);
            if (!font.TryAddCharacters(new string(ascii), out string missing))
                Debug.LogWarning($"Press Start 2P has no glyph for: {missing}");

            font.atlasPopulationMode = AtlasPopulationMode.Static;

            // The font's capitals fill the whole em and it reports a line height of exactly 1 em,
            // so stacked lines (1UP over the score) would touch. Give lines half an em of air.
            var face = font.faceInfo;
            face.lineHeight = face.pointSize * 1.5f;
            font.faceInfo = face;

            // Narrows the SDF edge ramp from about 2 px to about 1, so the pixel font reads sharp.
            font.material.SetFloat(Shader.PropertyToID("_Sharpness"), 0.6f);

            AssetDatabase.CreateAsset(font, FontAssetPath);
            font.atlasTexture.name = "PressStart2P SDF Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            font.material.name = "PressStart2P SDF Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        // The same font with a soft blood-red underlay: a cheap neon glow the mobile SDF shader supports.
        static Material NeonMaterial(TMP_FontAsset font)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(NeonMaterialPath);
            if (existing != null)
                return existing;

            var neon = new Material(font.material) { name = "PressStart2P Neon" };
            neon.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            neon.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.9f, 0.05f, 0.02f, 0.6f));
            neon.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            neon.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
            neon.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.6f);
            neon.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.8f);
            AssetDatabase.CreateAsset(neon, NeonMaterialPath);
            return neon;
        }

        static Color VignettePixel(int x, int y)
        {
            float dx = (x + 0.5f) / 128f - 1f;
            float dy = (y + 0.5f) / 128f - 1f;
            float distance = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Sqrt(2f);
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, distance));
            return new Color(0f, 0f, 0f, alpha);
        }

        // Fixed seed, so regenerating gives the same file and no noise in git.
        static readonly System.Random GrainRandom = new(1980);

        static Color GrainPixel(int x, int y)
        {
            float v = (float)GrainRandom.NextDouble();
            return new Color(v, v, v, 1f);
        }

        // Pac-Man facing right: a disc with a 70 degree mouth.
        static Color PacManPixel(int x, int y)
        {
            float dx = x + 0.5f - 8f;
            float dy = y + 0.5f - 8f;
            if (dx * dx + dy * dy > 7.2f * 7.2f)
                return Color.clear;

            float angle = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            return angle < 35f && dx > 0f ? Color.clear : Color.white;
        }

        static Color EyePixel(int x, int y) => Pixel(GhostEyePixels, x, y) switch
        {
            'W' => Color.white,
            'B' => new Color(0.13f, 0.13f, 0.9f, 1f),
            _ => Color.clear
        };

        // Bitmaps are written top row first; textures count rows from the bottom.
        static char Pixel(string[] rows, int x, int y) => rows[rows.Length - 1 - y][x];

        static void Texture(string path, int width, int height, Func<int, int, Color> pixel,
            bool repeat, bool sprite, bool point = true, int border = 0)
        {
            if (File.Exists(path))
                return;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, pixel(x, y));

            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.spriteImportMode = sprite ? SpriteImportMode.Single : SpriteImportMode.None;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            if (border > 0)
                importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
        }
    }
}
