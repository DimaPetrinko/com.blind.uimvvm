#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.Elements
{
	[UxmlElement]
	public partial class SafeArea : VisualElement
	{
		private const long PollIntervalMilliseconds = 250;

		private Rect mAppliedSafeArea;
		private Vector2Int mAppliedScreenSize;
		private bool mHasApplied;
		private VisualElement? mObservedPanelRoot;
		private IVisualElementScheduledItem? mPoll;

		[UxmlAttribute] public bool PadTop { get; set; } = true;
		[UxmlAttribute] public bool PadBottom { get; set; } = true;
		[UxmlAttribute] public bool PadLeft { get; set; } = true;
		[UxmlAttribute] public bool PadRight { get; set; } = true;

		public SafeArea()
		{
			RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
			RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
		}

		public static Vector4 CalculateReferenceInsets(Rect safeArea, Vector2Int screenSize, float panelWidthInReferencePixels)
		{
			if (panelWidthInReferencePixels <= 0f || float.IsNaN(panelWidthInReferencePixels) || screenSize.x <= 0)
				return Vector4.zero;

			var referencePixelsPerScreenPixel = panelWidthInReferencePixels / screenSize.x;

			return new Vector4(
				Mathf.Round(safeArea.xMin * referencePixelsPerScreenPixel),
				Mathf.Round((screenSize.y - safeArea.yMax) * referencePixelsPerScreenPixel),
				Mathf.Round((screenSize.x - safeArea.xMax) * referencePixelsPerScreenPixel),
				Mathf.Round(safeArea.yMin * referencePixelsPerScreenPixel));
		}

		private void OnAttachToPanel(AttachToPanelEvent attachEvent)
		{
			mObservedPanelRoot = panel?.visualTree;
			if (mObservedPanelRoot != null)
				mObservedPanelRoot.RegisterCallback<GeometryChangedEvent>(OnPanelGeometryChanged);

			mPoll = schedule.Execute(ApplyIfChanged).Every(PollIntervalMilliseconds);
			ApplyIfChanged();
		}

		private void OnDetachFromPanel(DetachFromPanelEvent detachEvent)
		{
			if (mObservedPanelRoot != null)
				mObservedPanelRoot.UnregisterCallback<GeometryChangedEvent>(OnPanelGeometryChanged);
			mObservedPanelRoot = null;

			mPoll?.Pause();
			mPoll = null;
			mHasApplied = false;
		}

		private void OnPanelGeometryChanged(GeometryChangedEvent geometryEvent) => ApplyIfChanged();

		private void ApplyIfChanged()
		{
			if (panel == null || panel.contextType != ContextType.Player)
				return;

			var panelWidth = panel.visualTree.layout.width;
			if (panelWidth <= 0f || float.IsNaN(panelWidth))
				return;

			var safeArea = Screen.safeArea;
			var screenSize = new Vector2Int(Screen.width, Screen.height);
			if (mHasApplied && safeArea == mAppliedSafeArea && screenSize == mAppliedScreenSize)
				return;

			var insets = CalculateReferenceInsets(safeArea, screenSize, panelWidth);

			style.paddingLeft = PadLeft ? insets.x : 0f;
			style.paddingTop = PadTop ? insets.y : 0f;
			style.paddingRight = PadRight ? insets.z : 0f;
			style.paddingBottom = PadBottom ? insets.w : 0f;

			mAppliedSafeArea = safeArea;
			mAppliedScreenSize = screenSize;
			mHasApplied = true;
		}
	}
}
