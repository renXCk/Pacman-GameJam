using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // The look rules the HUD uses: battery cells and state colors.
    public class UiThemeTests
    {
        [Test]
        public void BatteryLightsCellsRoundingUpSoAnyChargeShows()
        {
            Assert.That(UiTheme.LitSegments(1f, 8), Is.EqualTo(8));
            Assert.That(UiTheme.LitSegments(0.5f, 8), Is.EqualTo(4));
            Assert.That(UiTheme.LitSegments(0.51f, 8), Is.EqualTo(5));
            Assert.That(UiTheme.LitSegments(0.01f, 8), Is.EqualTo(1), "an almost empty battery still shows one cell");
            Assert.That(UiTheme.LitSegments(0f, 8), Is.EqualTo(0));
            Assert.That(UiTheme.LitSegments(2f, 8), Is.EqualTo(8), "clamped");
            Assert.That(UiTheme.LitSegments(0.5f, 0), Is.EqualTo(0));
        }

        [Test]
        public void ReadyIsYellowAndDangerIsRed()
        {
            var theme = ScriptableObject.CreateInstance<UiTheme>();

            Assert.That(theme.ColorFor(GameState.Ready), Is.EqualTo(theme.Ready));
            Assert.That(theme.ColorFor(GameState.PlayerCaught), Is.EqualTo(theme.Accent));
            Assert.That(theme.ColorFor(GameState.GameOver), Is.EqualTo(theme.Accent));
            Assert.That(theme.BatteryColor(0.1f), Is.Not.EqualTo(theme.BatteryColor(0.9f)), "low battery turns red");

            Object.DestroyImmediate(theme);
        }
    }
}
