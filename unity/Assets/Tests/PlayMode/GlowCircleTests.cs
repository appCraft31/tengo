using System.Collections;
using NUnit.Framework;
using TenGo.Rendering;
using UnityEngine;
using UnityEngine.TestTools;

namespace TenGo.Tests
{
    /// Le cercle néon se crée en jeu avec le shader SDF et ses réglages.
    public class GlowCircleTests
    {
        [UnityTest]
        public IEnumerator CreatesRendererWithGlowShader()
        {
            var c = GlowCircle.Create("Test", null, Vector2.zero, Palette.Coral, 0.3f, glowRange: 0.2f);
            yield return null;
            var r = c.GetComponent<MeshRenderer>();
            Assert.That(r.sharedMaterial, Is.Not.Null);
            Assert.That(r.sharedMaterial.shader.name, Is.EqualTo("TenGo/GlowCircle"));
            Assert.That(r.localBounds.extents.x, Is.EqualTo(c.Extent).Within(1e-4f));
            Object.Destroy(c.gameObject);
        }
    }
}
