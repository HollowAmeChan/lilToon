#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;

using Object = UnityEngine.Object;

namespace lilToon
{
    public partial class lilToonInspector
    {
        //------------------------------------------------------------------------------------------------------------------------------
        // Bake
        #region
        private void TextureBake(Material material, int bakeType)
        {
            //bool shouldBake1st = (bakeType == 1 || bakeType == 4) && mainTex.textureValue != null;
            bool shouldNotBakeColor = (bakeType == 1 || bakeType == 4) && mainColor.colorValue == Color.white && mainTexHSVG.vectorValue == lilConstants.defaultHSVG && mainGradationStrength.floatValue == 0.0;
            bool cannotBake1st = mainTex.textureValue == null;
            bool shouldNotBake2nd = (bakeType == 2 || bakeType == 5) && useMain2ndTex.floatValue == 0.0;
            bool shouldNotBake3rd = (bakeType == 3 || bakeType == 6) && useMain3rdTex.floatValue == 0.0;
            bool shouldNotBakeAll = bakeType == 0 && mainColor.colorValue == Color.white && mainTexHSVG.vectorValue == lilConstants.defaultHSVG && mainGradationStrength.floatValue == 0.0 && useMain2ndTex.floatValue == 0.0 && useMain3rdTex.floatValue == 0.0;
            if(cannotBake1st)
            {
                EditorUtility.DisplayDialog(GetLoc("sDialogCannotBake"), GetLoc("sDialogSetMainTex"), GetLoc("sOK"));
            }
            else if(shouldNotBakeColor)
            {
                EditorUtility.DisplayDialog(GetLoc("sDialogNoNeedBake"), GetLoc("sDialogNoChange"), GetLoc("sOK"));
            }
            else if(shouldNotBake2nd)
            {
                EditorUtility.DisplayDialog(GetLoc("sDialogNoNeedBake"), GetLoc("sDialogNotUse2nd"), GetLoc("sOK"));
            }
            else if(shouldNotBake3rd)
            {
                EditorUtility.DisplayDialog(GetLoc("sDialogNoNeedBake"), GetLoc("sDialogNotUse3rd"), GetLoc("sOK"));
            }
            else if(shouldNotBakeAll)
            {
                EditorUtility.DisplayDialog(GetLoc("sDialogNoNeedBake"), GetLoc("sDialogNotUseAll"), GetLoc("sOK"));
            }
            else
            {
                bool bake2nd = (bakeType == 0 || bakeType == 2 || bakeType == 5) && useMain2ndTex.floatValue != 0.0;
                bool bake3rd = (bakeType == 0 || bakeType == 3 || bakeType == 6) && useMain3rdTex.floatValue != 0.0;
                // run bake
                var bufMainTexture = mainTex.textureValue as Texture2D;
                var hsvgMaterial = new Material(ltsbaker);

                string path;

                var srcTexture = new Texture2D(2, 2);
                var srcMain2 = new Texture2D(2, 2);
                var srcMain3 = new Texture2D(2, 2);
                var srcMask2 = new Texture2D(2, 2);
                var srcMask3 = new Texture2D(2, 2);

                hsvgMaterial.SetColor(mainColor.name,           mainColor.colorValue);
                hsvgMaterial.SetVector(mainTexHSVG.name,        mainTexHSVG.vectorValue);
                hsvgMaterial.SetFloat(mainGradationStrength.name, mainGradationStrength.floatValue);
                hsvgMaterial.SetTexture(mainGradationTex.name, mainGradationTex.textureValue);
                hsvgMaterial.SetTexture(mainColorAdjustMask.name, mainColorAdjustMask.textureValue);

                path = AssetDatabase.GetAssetPath(material.GetTexture(mainTex.name));
                if(!string.IsNullOrEmpty(path))
                {
                    lilTextureUtils.LoadTexture(ref srcTexture, path);
                    hsvgMaterial.SetTexture(mainTex.name, srcTexture);
                }
                else
                {
                    hsvgMaterial.SetTexture(mainTex.name, Texture2D.whiteTexture);
                }

                if(bake2nd)
                {
                    hsvgMaterial.SetFloat(useMain2ndTex.name,               useMain2ndTex.floatValue);
                    hsvgMaterial.SetColor(mainColor2nd.name,                mainColor2nd.colorValue);
                    hsvgMaterial.SetFloat(main2ndTexAngle.name,             main2ndTexAngle.floatValue);
                    hsvgMaterial.SetVector(main2ndTexDecalAnimation.name,   main2ndTexDecalAnimation.vectorValue);
                    hsvgMaterial.SetVector(main2ndTexDecalSubParam.name,    main2ndTexDecalSubParam.vectorValue);
                    hsvgMaterial.SetFloat(main2ndTexIsDecal.name,           main2ndTexIsDecal.floatValue);
                    hsvgMaterial.SetFloat(main2ndTexIsLeftOnly.name,        main2ndTexIsLeftOnly.floatValue);
                    hsvgMaterial.SetFloat(main2ndTexIsRightOnly.name,       main2ndTexIsRightOnly.floatValue);
                    hsvgMaterial.SetFloat(main2ndTexShouldCopy.name,        main2ndTexShouldCopy.floatValue);
                    hsvgMaterial.SetFloat(main2ndTexShouldFlipMirror.name,  main2ndTexShouldFlipMirror.floatValue);
                    hsvgMaterial.SetFloat(main2ndTexShouldFlipCopy.name,    main2ndTexShouldFlipCopy.floatValue);
                    hsvgMaterial.SetFloat(main2ndTexIsMSDF.name,            main2ndTexIsMSDF.floatValue);
                    hsvgMaterial.SetFloat(main2ndTexBlendMode.name,         main2ndTexBlendMode.floatValue);
                    hsvgMaterial.SetFloat(main2ndTexAlphaMode.name,         main2ndTexAlphaMode.floatValue);
                    hsvgMaterial.SetTextureOffset(main2ndTex.name,          material.GetTextureOffset(main2ndTex.name));
                    hsvgMaterial.SetTextureScale(main2ndTex.name,           material.GetTextureScale(main2ndTex.name));
                    hsvgMaterial.SetTextureOffset(main2ndBlendMask.name,    material.GetTextureOffset(main2ndBlendMask.name));
                    hsvgMaterial.SetTextureScale(main2ndBlendMask.name,     material.GetTextureScale(main2ndBlendMask.name));

                    path = AssetDatabase.GetAssetPath(material.GetTexture(main2ndTex.name));
                    if(!string.IsNullOrEmpty(path))
                    {
                        lilTextureUtils.LoadTexture(ref srcMain2, path);
                        hsvgMaterial.SetTexture(main2ndTex.name, srcMain2);
                    }
                    else
                    {
                        hsvgMaterial.SetTexture(main2ndTex.name, Texture2D.whiteTexture);
                    }

                    path = AssetDatabase.GetAssetPath(material.GetTexture(main2ndBlendMask.name));
                    if(!string.IsNullOrEmpty(path))
                    {
                        lilTextureUtils.LoadTexture(ref srcMask2, path);
                        hsvgMaterial.SetTexture(main2ndBlendMask.name, srcMask2);
                    }
                    else
                    {
                        hsvgMaterial.SetTexture(main2ndBlendMask.name, Texture2D.whiteTexture);
                    }
                }

                if(bake3rd)
                {
                    hsvgMaterial.SetFloat(useMain3rdTex.name,               useMain3rdTex.floatValue);
                    hsvgMaterial.SetColor(mainColor3rd.name,                mainColor3rd.colorValue);
                    hsvgMaterial.SetFloat(main3rdTexAngle.name,             main3rdTexAngle.floatValue);
                    hsvgMaterial.SetVector(main3rdTexDecalAnimation.name,   main3rdTexDecalAnimation.vectorValue);
                    hsvgMaterial.SetVector(main3rdTexDecalSubParam.name,    main3rdTexDecalSubParam.vectorValue);
                    hsvgMaterial.SetFloat(main3rdTexIsDecal.name,           main3rdTexIsDecal.floatValue);
                    hsvgMaterial.SetFloat(main3rdTexIsLeftOnly.name,        main3rdTexIsLeftOnly.floatValue);
                    hsvgMaterial.SetFloat(main3rdTexIsRightOnly.name,       main3rdTexIsRightOnly.floatValue);
                    hsvgMaterial.SetFloat(main3rdTexShouldCopy.name,        main3rdTexShouldCopy.floatValue);
                    hsvgMaterial.SetFloat(main3rdTexShouldFlipMirror.name,  main3rdTexShouldFlipMirror.floatValue);
                    hsvgMaterial.SetFloat(main3rdTexShouldFlipCopy.name,    main3rdTexShouldFlipCopy.floatValue);
                    hsvgMaterial.SetFloat(main3rdTexIsMSDF.name,            main3rdTexIsMSDF.floatValue);
                    hsvgMaterial.SetFloat(main3rdTexBlendMode.name,         main3rdTexBlendMode.floatValue);
                    hsvgMaterial.SetFloat(main3rdTexAlphaMode.name,         main3rdTexAlphaMode.floatValue);
                    hsvgMaterial.SetTextureOffset(main3rdTex.name,          material.GetTextureOffset(main3rdTex.name));
                    hsvgMaterial.SetTextureScale(main3rdTex.name,           material.GetTextureScale(main3rdTex.name));
                    hsvgMaterial.SetTextureOffset(main3rdBlendMask.name,    material.GetTextureOffset(main3rdBlendMask.name));
                    hsvgMaterial.SetTextureScale(main3rdBlendMask.name,     material.GetTextureScale(main3rdBlendMask.name));

                    path = AssetDatabase.GetAssetPath(material.GetTexture(main3rdTex.name));
                    if(!string.IsNullOrEmpty(path))
                    {
                        lilTextureUtils.LoadTexture(ref srcMain3, path);
                        hsvgMaterial.SetTexture(main3rdTex.name, srcMain3);
                    }
                    else
                    {
                        hsvgMaterial.SetTexture(main3rdTex.name, Texture2D.whiteTexture);
                    }

                    path = AssetDatabase.GetAssetPath(material.GetTexture(main3rdBlendMask.name));
                    if(!string.IsNullOrEmpty(path))
                    {
                        lilTextureUtils.LoadTexture(ref srcMask3, path);
                        hsvgMaterial.SetTexture(main3rdBlendMask.name, srcMask3);
                    }
                    else
                    {
                        hsvgMaterial.SetTexture(main3rdBlendMask.name, Texture2D.whiteTexture);
                    }
                }

                Texture2D outTexture = null;
                RunBake(ref outTexture, srcTexture, hsvgMaterial);

                outTexture = lilTextureUtils.SaveTextureToPng(material, outTexture, mainTex.name);
                if(outTexture != mainTex.textureValue)
                {
                    mainTexHSVG.vectorValue = lilConstants.defaultHSVG;
                    mainColor.colorValue = Color.white;
                    mainGradationStrength.floatValue = 0.0f;
                    mainGradationTex.textureValue = null;
                    if(bake2nd)
                    {
                        useMain2ndTex.floatValue = 0.0f;
                        main2ndTex.textureValue = null;
                    }
                    if(bake3rd)
                    {
                        useMain3rdTex.floatValue = 0.0f;
                        main3rdTex.textureValue = null;
                    }
                    CopyTextureSetting(bufMainTexture, outTexture);
                }

                material.SetTexture(mainTex.name, outTexture);

                Object.DestroyImmediate(hsvgMaterial);
                Object.DestroyImmediate(srcTexture);
                Object.DestroyImmediate(srcMain2);
                Object.DestroyImmediate(srcMain3);
                Object.DestroyImmediate(srcMask2);
                Object.DestroyImmediate(srcMask3);
            }
        }

        private Texture AutoBakeAlphaMask(Material material)
        {
            // run bake
            var bufMainTexture = mainTex.textureValue as Texture2D;
            var hsvgMaterial = new Material(ltsbaker);

            string path;

            var srcTexture = new Texture2D(2, 2);
            var srcAlphaMask = new Texture2D(2, 2);

            hsvgMaterial.EnableKeyword("_ALPHAMASK");
            hsvgMaterial.SetColor(mainColor.name,           Color.white);
            hsvgMaterial.SetVector(mainTexHSVG.name,        lilConstants.defaultHSVG);
            hsvgMaterial.SetFloat(alphaMaskMode.name,       alphaMaskMode.floatValue);
            hsvgMaterial.SetFloat(alphaMaskScale.name,      alphaMaskScale.floatValue);
            hsvgMaterial.SetFloat(alphaMaskValue.name,      alphaMaskValue.floatValue);

            path = AssetDatabase.GetAssetPath(bufMainTexture);
            if(!string.IsNullOrEmpty(path))
            {
                lilTextureUtils.LoadTexture(ref srcTexture, path);
                hsvgMaterial.SetTexture(mainTex.name, srcTexture);
            }
            else
            {
                hsvgMaterial.SetTexture(mainTex.name, Texture2D.whiteTexture);
            }

            path = AssetDatabase.GetAssetPath(material.GetTexture(alphaMask.name));
            if(!string.IsNullOrEmpty(path))
            {
                lilTextureUtils.LoadTexture(ref srcAlphaMask, path);
                hsvgMaterial.SetTexture(alphaMask.name, srcAlphaMask);
            }
            else
            {
                return (Texture2D)mainTex.textureValue;
            }

            Texture2D outTexture = null;
            RunBake(ref outTexture, srcTexture, hsvgMaterial);

            outTexture = lilTextureUtils.SaveTextureToPng(outTexture, bufMainTexture);
            if(outTexture != bufMainTexture)
            {
                CopyTextureSetting(bufMainTexture, outTexture);
                string savePath = AssetDatabase.GetAssetPath(outTexture);
                var textureImporter = (TextureImporter)AssetImporter.GetAtPath(savePath);
                textureImporter.alphaIsTransparency = true;
                AssetDatabase.ImportAsset(savePath);
            }

            Object.DestroyImmediate(hsvgMaterial);
            Object.DestroyImmediate(srcTexture);

            return outTexture;
        }

        private Texture AutoBakeOutlineTexture(Material material)
        {
            bool shouldNotBakeOutline = outlineTex.textureValue == null || outlineTexHSVG.vectorValue == lilConstants.defaultHSVG;
            if(!shouldNotBakeOutline && EditorUtility.DisplayDialog(GetLoc("sDialogRunBake"), GetLoc("sDialogBakeOutline"), GetLoc("sYes"), GetLoc("sNo")))
            {
                // run bake
                var bufMainTexture = outlineTex.textureValue as Texture2D;
                var hsvgMaterial = new Material(ltsbaker);

                string path;

                var srcTexture = new Texture2D(2, 2);

                hsvgMaterial.SetColor(mainColor.name,                   Color.white);
                hsvgMaterial.SetVector(mainTexHSVG.name,                outlineTexHSVG.vectorValue);

                path = AssetDatabase.GetAssetPath(material.GetTexture(outlineTex.name));
                if(!string.IsNullOrEmpty(path))
                {
                    lilTextureUtils.LoadTexture(ref srcTexture, path);
                    hsvgMaterial.SetTexture(mainTex.name, srcTexture);
                }
                else
                {
                    hsvgMaterial.SetTexture(mainTex.name, Texture2D.whiteTexture);
                }

                Texture2D outTexture = null;
                RunBake(ref outTexture, srcTexture, hsvgMaterial);

                outTexture = lilTextureUtils.SaveTextureToPng(material, outTexture, mainTex.name);
                if(outTexture != mainTex.textureValue)
                {
                    CopyTextureSetting(bufMainTexture, outTexture);
                }

                Object.DestroyImmediate(hsvgMaterial);
                Object.DestroyImmediate(srcTexture);

                return outTexture;
            }
            else
            {
                return outlineTex.textureValue;
            }
        }

        public static void RunBake(ref Texture2D outTexture, Texture2D srcTexture, Material material, Texture2D referenceTexture = null)
        {
            int width = 4096;
            int height = 4096;
            if(referenceTexture != null)
            {
                width = referenceTexture.width;
                height = referenceTexture.height;
            }
            else if(srcTexture != null)
            {
                width = srcTexture.width;
                height = srcTexture.height;
            }
            outTexture = new Texture2D(width, height);

            var bufRT = RenderTexture.active;
            var dstTexture = RenderTexture.GetTemporary(width, height);
            Graphics.Blit(srcTexture, dstTexture, material);
            RenderTexture.active = dstTexture;
            outTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            outTexture.Apply();
            RenderTexture.active = bufRT;
            RenderTexture.ReleaseTemporary(dstTexture);
        }

        private void CopyTextureSetting(Texture2D fromTexture, Texture2D toTexture)
        {
            if(fromTexture == null || toTexture == null) return;
            string fromPath = AssetDatabase.GetAssetPath(fromTexture);
            string toPath = AssetDatabase.GetAssetPath(toTexture);
            var fromTextureImporter = (TextureImporter)AssetImporter.GetAtPath(fromPath);
            var toTextureImporter = (TextureImporter)AssetImporter.GetAtPath(toPath);
            if(fromTextureImporter == null || toTextureImporter == null) return;

            var fromTextureImporterSettings = new TextureImporterSettings();
            fromTextureImporter.ReadTextureSettings(fromTextureImporterSettings);
            toTextureImporter.SetTextureSettings(fromTextureImporterSettings);
            toTextureImporter.SetPlatformTextureSettings(fromTextureImporter.GetDefaultPlatformTextureSettings());
            AssetDatabase.ImportAsset(toPath);
        }
        #endregion

    }
}
#endif
