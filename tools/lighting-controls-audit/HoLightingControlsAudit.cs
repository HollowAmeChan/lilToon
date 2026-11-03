using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Characterization of the 2026-11-03 implementation, including known broken controls.
// Passing these tests reproduces the audit; it does not approve the current lighting contract.
public sealed class HoLightingControlsAudit
{
    private Material material;
    private RenderTexture target;
    private Texture2D result;

    [SetUp]
    public void SetUp()
    {
        Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType);
        var shader = Shader.Find("Hidden/HoRP/Audit/LightingControls");
        Assert.IsNotNull(shader);
        Assert.IsTrue(shader.isSupported);
        material = new Material(shader);
        target = new RenderTexture(2, 2, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        result = new Texture2D(2, 2, TextureFormat.RGBAFloat, false, true);
        material.SetMatrix("_AuditObjectMatrix", Matrix4x4.identity);
    }

    [TearDown]
    public void TearDown()
    {
        if(target != null) target.Release();
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(result);
        Object.DestroyImmediate(material);
    }

    private Color Read()
    {
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(Texture2D.whiteTexture, target, material);
            RenderTexture.active = target;
            result.ReadPixels(new Rect(0, 0, 2, 2), 0, 0);
            result.Apply();
            return result.GetPixel(0, 0);
        }
        finally { RenderTexture.active = previous; }
    }

    private static void Expect(Color actual, float r, float g, float b)
    {
        Assert.That(actual.r, Is.EqualTo(r).Within(.002f));
        Assert.That(actual.g, Is.EqualTo(g).Within(.002f));
        Assert.That(actual.b, Is.EqualTo(b).Within(.002f));
    }

    [Test]
    public void ClampIsPerRGBChannel()
    {
        material.SetVector("_AuditLightColor", new Vector4(.01f, 2, .3f, 1));
        material.SetFloat("_LightMinLimit", .05f);
        material.SetFloat("_LightMaxLimit", 1);
        Expect(Read(), .05f, 1, .3f);
    }

    [Test]
    public void MonochromeUsesArithmeticMean()
    {
        material.SetVector("_AuditLightColor", new Vector4(.9f, .3f, 0, 1));
        material.SetFloat("_MonochromeLighting", 1);
        Expect(Read(), .4f, .4f, .4f);
    }

    [Test]
    public void AdditionalLightIntensityControlsDirectColor()
    {
        material.SetFloat("_AuditOperation", 1);
        material.SetVector("_AuditExtraColor", new Vector4(.1f, .2f, .3f, 1));
        material.SetFloat("_HoAdditionalLightIntensity", 2);
        Expect(Read(), .2f, .4f, .6f);
    }

    [TestCase(1f, .5f, .25f, .125f)]
    [TestCase(.25f, .125f, .0625f, .03125f)]
    public void AdditionalLightUsesNativeVisibilityExactlyOnce(float visibility, float r, float g, float b)
    {
        material.SetFloat("_AuditOperation", 1);
        material.SetVector("_AuditExtraColor", new Vector4(.5f, .25f, .125f, 1));
        material.SetFloat("_AuditCast", visibility);
        Expect(Read(), r, g, b);
    }

    [Test]
    public void AdditionalLightNoLongerUsesTheRemovedGlobalHdrPool()
    {
        material.SetFloat("_AuditOperation", 1);
        material.SetVector("_AuditExtraColor", new Vector4(.5f, .25f, .125f, 1));
        material.SetFloat("_HoAdditionalLightIntensity", 1);
        material.SetFloat("_HoAdditionalLightShadowStrength", 1);
        Expect(Read(), .5f, .25f, .125f);
    }

    [TestCase(0, 0)]
    [TestCase(30, 0)]
    [TestCase(60, 0)]
    public void DirectionDeviationDoesNotChangeCastVisibility(int degrees, float expected)
    {
        material.SetFloat("_AuditOperation", 2);
        material.SetFloat("_AuditCast", 0);
        float angle = degrees * Mathf.Deg2Rad;
        material.SetVector("_AuditDirection", new Vector4(Mathf.Sin(angle), Mathf.Cos(angle), 0, 0));
        Expect(Read(), expected, expected, expected);
    }

    [TestCase(.35f)]
    [TestCase(.75f)]
    public void DirectionDeviationPreservesPartialCastVisibility(float providerVisibility)
    {
        material.SetFloat("_AuditOperation", 2);
        material.SetFloat("_AuditCast", providerVisibility);
        float angle = 60 * Mathf.Deg2Rad;
        material.SetVector("_AuditDirection", new Vector4(Mathf.Sin(angle), Mathf.Cos(angle), 0, 0));
        Expect(Read(), providerVisibility, providerVisibility, providerVisibility);
    }

    [TestCase(0, 0, 1)]
    [TestCase(1, -1, 0)]
    public void ObjectFollowingControlsTheOverrideSpace(int follow, float x, float y)
    {
        material.SetFloat("_AuditOperation", 3);
        material.SetMatrix("_AuditObjectMatrix", Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90)));
        material.SetVector("_AuditOverride", new Vector4(0, 1, 0, follow));
        Expect(Read(), x, y, 0);
    }
}
