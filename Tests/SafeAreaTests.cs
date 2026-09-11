#nullable enable
using Blind.UiMvvm.Elements;
using NUnit.Framework;
using UnityEngine;

namespace Blind.UiMvvm.Tests
{
	/// <summary>
	/// The device maths, which is the half worth asserting: Screen.safeArea is in device pixels with a
	/// bottom-left origin, while the layout is in reference pixels with a top-left one.
	/// </summary>
	public class SafeAreaTests
	{
		private static readonly Vector2Int Portrait1080 = new(1080, 1920);

		[Test]
		public void APhoneWithNoCutouts_NeedsNoInsets()
		{
			var insets = SafeArea.CalculateReferenceInsets(
				new Rect(0, 0, 1080, 1920), Portrait1080, 1080f);

			Assert.That(insets, Is.EqualTo(Vector4.zero));
		}

		/// <summary>
		/// The top inset is screen height minus yMax, not y. Reading y as the top is the mistake this
		/// pins: it would put the notch inset at the bottom of the screen.
		/// </summary>
		[Test]
		public void TheTopInset_ComesFromTheDistanceAboveYMax()
		{
			// 130 above, 72 below - a notch and a home indicator.
			var safeArea = new Rect(0, 72, 1080, 1920 - 72 - 130);

			var insets = SafeArea.CalculateReferenceInsets(safeArea, Portrait1080, 1080f);

			Assert.That(insets.y, Is.EqualTo(130f), "top");
			Assert.That(insets.w, Is.EqualTo(72f), "bottom");
		}

		[Test]
		public void InsetsAreScaled_FromDevicePixelsToReferencePixels()
		{
			var safeArea = new Rect(0, 72, 1080, 1920 - 72 - 130);

			var insets = SafeArea.CalculateReferenceInsets(safeArea, Portrait1080, 540f);

			Assert.That(insets.y, Is.EqualTo(65f), "top");
			Assert.That(insets.w, Is.EqualTo(36f), "bottom");
		}

		[Test]
		public void LandscapeCutouts_InsetTheSides()
		{
			var screen = new Vector2Int(1920, 1080);
			var safeArea = new Rect(130, 0, 1920 - 130 - 72, 1080);

			var insets = SafeArea.CalculateReferenceInsets(safeArea, screen, 1920f);

			Assert.That(insets.x, Is.EqualTo(130f), "left");
			Assert.That(insets.z, Is.EqualTo(72f), "right");
		}

		/// <summary>The first frame has no layout yet, and dividing by that width is a NaN inset.</summary>
		[TestCase(0f)]
		[TestCase(float.NaN)]
		public void APanelWithNoWidthYet_YieldsNoInsets(float panelWidth)
		{
			var insets = SafeArea.CalculateReferenceInsets(
				new Rect(0, 72, 1080, 1718), Portrait1080, panelWidth);

			Assert.That(insets, Is.EqualTo(Vector4.zero));
		}

		[Test]
		public void AScreenWithNoWidth_YieldsNoInsets()
		{
			var insets = SafeArea.CalculateReferenceInsets(
				new Rect(0, 0, 0, 0), new Vector2Int(0, 0), 1080f);

			Assert.That(insets, Is.EqualTo(Vector4.zero));
		}
	}
}
