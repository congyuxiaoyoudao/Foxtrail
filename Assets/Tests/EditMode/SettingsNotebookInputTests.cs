using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hiking.UIUX.Tests
{
    public class SettingsNotebookInputTests
    {
        [Test]
        public void EverySliderHasAnInteractiveRaycastTarget()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UIUX/SettingsNotebook.prefab");
            Assert.That(prefab, Is.Not.Null);

            Slider[] sliders = prefab.GetComponentsInChildren<Slider>(true);
            Assert.That(sliders, Has.Length.EqualTo(4));
            foreach (Slider slider in sliders)
            {
                Assert.That(slider.GetComponentsInChildren<Graphic>(true)
                    .Any(graphic => graphic.raycastTarget), Is.True,
                    slider.name + " cannot receive pointer input");
            }
        }

        [Test]
        public void NotebookTextUsesNishikiTekiWithAReadableRegularTitle()
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/Arts/UIUX/Fonts/nishiki-teki.ttf");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UIUX/SettingsNotebook.prefab");
            Assert.That(font, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);

            Text[] labels = prefab.GetComponentsInChildren<Text>(true);
            Assert.That(labels, Is.Not.Empty);
            Text title = labels.SingleOrDefault(label => label.name == "设置");
            Assert.That(title, Is.Not.Null);
            Assert.That(title.font, Is.EqualTo(font));
            Assert.That(title.fontStyle, Is.EqualTo(FontStyle.Normal));
            Assert.That(title.fontSize, Is.EqualTo(42));
        }

        [Test]
        public void NishikiTekiAppliesToAllNotebookLabels()
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/Arts/UIUX/Fonts/nishiki-teki.ttf");
            Assert.That(font, Is.Not.Null);

            GameObject contents = PrefabUtility.LoadPrefabContents(
                "Assets/Prefabs/UIUX/SettingsNotebook.prefab");
            try
            {
                SettingsNotebookController controller = contents.GetComponent<SettingsNotebookController>();
                controller.ApplyTypography();
                Text[] labels = contents.GetComponentsInChildren<Text>(true);
                Assert.That(labels, Is.Not.Empty);
                Assert.That(labels.All(label => label.font == font), Is.True);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
