using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MapReroll {
	public static class MapRerollUtility {
		private static readonly Color DisabledColor = new Color(0.37f, 0.37f, 0.37f, 0.8f);

		public static string WithCostSuffix(string translationKey, PaidOperationType type, int desiredPreviewsPage = 0) {
			var cost = RerollToolbox.GetOperationCost(type, desiredPreviewsPage);
			var suffix = cost > 0 ? "Reroll2_costSuffix".Translate(cost.ToString("0.#")).ToString() : String.Empty;
			return translationKey.Translate(suffix);
		}

		public static IEnumerable<T> ExceptNull<T>(this IEnumerable<T> source) {
			foreach (var item in source) {
				if (item != null) yield return item;
			}
		}
		
		// button with improved disabled state. The stock button still reacts to mouse events when disabled.
		internal static bool DrawActiveButton(Rect btnRect, string label, bool active) {
			var pressed = false;
			if (active) {
				pressed = Widgets.ButtonText(btnRect, label);
			} else {
				DrawDisabledButton(btnRect, label);
			}
			return pressed;
		}

		private static void DrawDisabledButton(Rect btnRect, string label) {
			using (GUIStateContext.Set(DisabledColor, TextAnchor.MiddleCenter)) {
				Widgets.DrawAtlas(btnRect, Resources.Textures.ButtonAtlas);
				Widgets.Label(btnRect, label);					
			}
		}

		internal static int DrawIntSpinnerInput(Rect fullRect, int value, int minValue, int maxValue, 
			int increment, bool enabled, ref SpinnerInputState inputState) {
			var buttonSize = fullRect.height;
			DrawSpinnerIncrementButton(new Rect(fullRect.x, fullRect.y, 
				buttonSize, buttonSize), "-", -increment);
			DrawSpinnerIncrementButton(new Rect(fullRect.x + fullRect.width - buttonSize, fullRect.y, 
				buttonSize, buttonSize), "+", increment);
			DrawIntTextInput(new Rect(fullRect.x + buttonSize + 1, fullRect.y,
				fullRect.width - buttonSize * 2 - 2f, fullRect.height), ref inputState);
			return Mathf.Clamp(value, minValue, maxValue);
			
			void DrawSpinnerIncrementButton(Rect rect, string label, int delta) {
				if (DrawActiveButton(rect, label, enabled)) value += delta;
			}
			void DrawIntTextInput(Rect rect, ref SpinnerInputState state) {
				if (state == null) state = new SpinnerInputState {ControlName = $"{Rand.Int.ToString()}spin"};
				if (state.CurrentValue != value || state.StringValue == null) {
					state.CurrentValue = value;
					state.StringValue = value.ToString();
				}
				GUI.SetNextControlName(state.ControlName);
				GUI.enabled = enabled;
				string newStringValue;
				using (GUIStateContext.Set(enabled ? Color.white : DisabledColor)) {
					newStringValue = Widgets.TextField(rect, enabled ? state.StringValue : string.Empty);
				}
				GUI.enabled = true;
				if (newStringValue != state.StringValue) {
					state.StringValue = newStringValue;
					state.ApplyScheduled = true;
				}
				var focused = GUI.GetNameOfFocusedControl() == state.ControlName;
				var evt = Event.current;
				if (focused && evt.type == EventType.KeyUp
					&& (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)) {
					state.ApplyScheduled = true;
					focused = false;
				}
				if (!focused && state.ApplyScheduled) {
					state.ApplyScheduled = false;
					value = int.TryParse(state.StringValue, out var parsed) ? parsed : default;
					state.StringValue = null;
				}
			}
		}

		internal class SpinnerInputState {
			public int CurrentValue { get; set; }
			public string StringValue { get; set; }
			public string ControlName { get; set; }
			public bool ApplyScheduled { get; set; }
		}
	}
}