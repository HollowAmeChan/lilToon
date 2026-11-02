using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace lilToon.Tests
{
    public sealed class HoMaterialManagerTests
    {
        private static readonly Assembly EditorAssembly = typeof(lilToonInspector).Assembly;
        private static readonly Type PaneType = EditorAssembly.GetType("lilToon.lilMaterialManagerPropertyPane", true);
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly List<Material> materials = new List<Material>();
        private object pane;

        [TearDown]
        public void TearDown()
        {
            if(pane != null) PaneType.GetMethod("Dispose", InstanceMembers).Invoke(pane, null);
            pane = null;
            foreach(var material in materials) Object.DestroyImmediate(material);
            materials.Clear();
        }

        private Material CreateMaterial(string shaderName = "lilToon")
        {
            var shader = Shader.Find(shaderName);
            Assert.IsNotNull(shader, shaderName);
            var material = new Material(shader) { name = "HoMaterialManagerTest" };
            materials.Add(material);
            return material;
        }

        private void Select(params Material[] selection)
        {
            var entryType = EditorAssembly.GetType("lilToon.lilMaterialEntry", true);
            var entries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));
            foreach(var material in selection)
            {
                var entry = Activator.CreateInstance(entryType, true);
                entryType.GetField("material", InstanceMembers).SetValue(entry, material);
                entries.Add(entry);
            }
            pane = Activator.CreateInstance(PaneType, true);
            PaneType.GetMethod("SetSelection", InstanceMembers).Invoke(pane, new object[] { entries });
        }

        private Dictionary<string, List<string>> DisplayedProperties()
        {
            var result = new Dictionary<string, List<string>>();
            var groups = (IEnumerable)PaneType.GetField("groups", InstanceMembers).GetValue(pane);
            foreach(var group in groups)
            {
                var buckets = (IEnumerable)group.GetType().GetField("buckets", InstanceMembers).GetValue(group);
                foreach(var bucket in buckets)
                {
                    string bucketName = (string)bucket.GetType().GetField("name", InstanceMembers).GetValue(bucket);
                    var properties = (IEnumerable)bucket.GetType().GetField("properties", InstanceMembers).GetValue(bucket);
                    foreach(MaterialProperty property in properties)
                    {
                        if(!result.TryGetValue(property.name, out var names))
                            result[property.name] = names = new List<string>();
                        names.Add(bucketName);
                    }
                }
            }
            return result;
        }

        [TestCase("lilToon")]
        [TestCase("Hidden/lilToon/Tests/ManagerInputs")]
        public void EveryVisibleInputIsDisplayedExactlyOnce(string shaderName)
        {
            var material = CreateMaterial(shaderName);
            Select(material);
            var expected = MaterialEditor.GetMaterialProperties(new Object[] { material })
                .Where(property => (property.propertyFlags & ShaderPropertyFlags.HideInInspector) == 0)
                .Where(property => !lilPropertyNameChecker.IsDummyProperty(property.name))
                .Select(property => property.name).ToArray();
            var displayed = DisplayedProperties();
            CollectionAssert.AreEquivalent(expected, displayed.Keys);
            foreach(var property in displayed)
                Assert.AreEqual(1, property.Value.Count, property.Key);
        }

        [TestCase("_HoRPShadowMode", "照明")]
        [TestCase("_MultiLightIntensity", "照明")]
        [TestCase("_MultiLightCastShadowStrength", "照明")]
        [TestCase("_AODarkStrength", "阴影")]
        [TestCase("_UseHair", "头发")]
        [TestCase("_HairLobe1Strength", "头发")]
        [TestCase("_HoSurfaceThickness", "Ho 表面语义")]
        [TestCase("_HoSemanticMaskOn", "Ho 表面语义")]
        [TestCase("_HoCharacterCaptureOpacity", "Ho 表面语义")]
        [TestCase("_UsePlanarReflection", "平面反射")]
        [TestCase("_PlanarReflectionStrength", "平面反射")]
        [TestCase("_HoTesionEnabled", "张力")]
        [TestCase("_HoTesionStretchStrength", "张力")]
        public void HoInputsUseTheirInspectorSections(string name, string expectedBucket)
        {
            // Hair may be stripped by the project's global shader feature settings.
            // Use a fixed input-only fixture to exercise its section independently.
            bool hairInput = name == "_UseHair" || name.StartsWith("_Hair", StringComparison.Ordinal);
            var material = CreateMaterial(hairInput ? "Hidden/lilToon/Tests/ManagerInputs" : "lilToon");
            Assert.IsTrue(material.HasProperty(name), name);
            Select(material);
            var properties = DisplayedProperties();
            Assert.IsTrue(properties.ContainsKey(name), name + " is missing from the panel.");
            CollectionAssert.AreEqual(new[] { expectedBucket }, properties[name]);
        }

        [Test]
        public void HiddenMetadataStaysHidden()
        {
            Select(CreateMaterial());
            var properties = DisplayedProperties();
            Assert.IsFalse(properties.ContainsKey("_HoTesionMaps"));
            Assert.IsFalse(properties.ContainsKey("_LILPBRPlanarReflectionTexture"));
            Assert.IsFalse(properties.ContainsKey("_BaseColor"));
            Assert.IsFalse(properties.ContainsKey("_DummyProperty"));
        }

        [Test]
        public void MissingDisplayOrderEntryDoesNotHideItsSection()
        {
            var order = (string[])PaneType.GetField("BucketOrder", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            int index = Array.IndexOf(order, "头发");
            Assert.GreaterOrEqual(index, 0);
            string previous = order[index];
            try
            {
                order[index] = "__MissingSectionForRegressionTest";
                Select(CreateMaterial("Hidden/lilToon/Tests/ManagerInputs"));
                CollectionAssert.AreEqual(new[] { "头发" }, DisplayedProperties()["_UseHair"]);
            }
            finally { order[index] = previous; }
        }

        [Test]
        public void ShadowModeBroadcastsAcrossShaderGroupsWithoutChangingLighting()
        {
            var first = CreateMaterial();
            var second = CreateMaterial("Hidden/lilToon/Tests/ManagerInputs");
            first.SetFloat("_HoRPShadowMode", 1);
            second.SetFloat("_HoRPShadowMode", 2);
            first.SetFloat("_MultiLightIntensity", .25f);
            second.SetFloat("_MultiLightIntensity", 3.75f);
            Select(first, second);
            var groups = (IList)PaneType.GetField("groups", InstanceMembers).GetValue(pane);
            Assert.Greater(groups.Count, 1, "Exercise the cross-group write path.");
            var source = MaterialEditor.GetMaterialProperty(new Object[] { first }, "_HoRPShadowMode");
            source.floatValue = 2;
            int applied = (int)PaneType.GetMethod("WriteToAllSelected", InstanceMembers)
                .Invoke(pane, new object[] { source, false, false });
            Assert.AreEqual(2, applied);
            Assert.AreEqual(2, first.GetFloat("_HoRPShadowMode"));
            Assert.AreEqual(2, second.GetFloat("_HoRPShadowMode"));
            Assert.AreEqual(.25f, first.GetFloat("_MultiLightIntensity"));
            Assert.AreEqual(3.75f, second.GetFloat("_MultiLightIntensity"));
        }

        [TestCase("_HoTesionEnabled", "_HO_GD_TENSION")]
        [TestCase("_HoSemanticMaskOn", "_HO_SEMANTIC_MASK")]
        public void HoToggleBroadcastsAlsoSynchronizeKeywords(string propertyName, string keyword)
        {
            var first = CreateMaterial();
            var second = CreateMaterial("Hidden/lilToonOutline");
            Select(first, second);
            foreach(var material in materials)
            {
                Assert.IsTrue(material.shader.keywordSpace.FindKeyword(keyword).isValid, keyword);
                material.SetFloat(propertyName, 0);
                material.DisableKeyword(keyword);
            }
            var source = MaterialEditor.GetMaterialProperty(new Object[] { first }, propertyName);
            foreach(float value in new[] { 1f, 0f })
            {
                source.floatValue = value;
                PaneType.GetMethod("WriteToAllSelected", InstanceMembers)
                    .Invoke(pane, new object[] { source, false, false });
                foreach(var material in materials)
                {
                    Assert.AreEqual(value, material.GetFloat(propertyName));
                    Assert.AreEqual(value > .5f, material.IsKeywordEnabled(keyword), material.shader.name);
                }
            }
        }

        [Test]
        public void ReloadingAnExistingWindowRemovesThePreviewTitle()
        {
            var windowType = EditorAssembly.GetType("lilToon.lilMaterialManagerWindow", true);
            var window = (EditorWindow)ScriptableObject.CreateInstance(windowType);
            try
            {
                window.titleContent = new GUIContent("[测试版] lilToon 材质管理器");
                windowType.GetMethod("OnEnable", InstanceMembers).Invoke(window, null);
                Assert.AreEqual("lilToon 材质管理器", window.titleContent.text);
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
