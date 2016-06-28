﻿/*
    Copyright (C) 2014-2016 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using dnSpy.Contracts.Text;
using dnSpy.Contracts.Text.Editor;
using dnSpy.Contracts.Text.Formatting;

namespace dnSpy.Text.Editor {
	sealed class DefaultTextViewMouseProcessor : DefaultMouseProcessor {
		readonly IWpfTextView wpfTextView;

		public DefaultTextViewMouseProcessor(IWpfTextView wpfTextView) {
			if (wpfTextView == null)
				throw new ArgumentNullException(nameof(wpfTextView));
			this.wpfTextView = wpfTextView;
		}

		MouseLocation GetLocation(MouseEventArgs e) => MouseLocation.Create(wpfTextView, e);

		public override void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
			e.Handled = true;
			var mouseLoc = GetLocation(e);
			wpfTextView.EditorOperations.MoveCaret(mouseLoc.TextViewLine, mouseLoc.Point.X, false);
			wpfTextView.Caret.EnsureVisible();
		}

		void UpdateSelectionMode() {
			if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
				wpfTextView.Selection.Mode = TextSelectionMode.Box;
			else if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
				wpfTextView.Selection.Mode = TextSelectionMode.Stream;
		}

		void SelectToMousePosition(MouseEventArgs e, bool extendSelection) =>
			SelectToMousePosition(GetLocation(e), extendSelection);
		void SelectToMousePosition(MouseLocation mouseLoc, bool extendSelection) {
			UpdateSelectionMode();
			wpfTextView.EditorOperations.MoveCaret(mouseLoc.TextViewLine, mouseLoc.Point.X, extendSelection);
		}

		public override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			e.Handled = true;
			var mouseLoc = GetLocation(e);
			switch (e.ClickCount) {
			default:
			case 1:
				SelectToMousePosition(mouseLoc, (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
				break;

			case 2:
				wpfTextView.EditorOperations.SelectCurrentWord();
				break;

			case 3:
				wpfTextView.EditorOperations.SelectLine(mouseLoc.TextViewLine, false);
				// Seems to match VS behavior
				var end = mouseLoc.TextViewLine.TextRight;
				if (mouseLoc.TextViewLine.IsLastTextViewLineForSnapshotLine)
					end += wpfTextView.FormattedLineSource.ColumnWidth;
				if (mouseLoc.Point.X < end)
					wpfTextView.Caret.MoveTo(mouseLoc.TextViewLine.Start);
				break;
			}
			wpfTextView.Caret.EnsureVisible();
			mouseLeftDownInfo = new MouseLeftDownInfo(GetSelectionOrCaretIfNoSelection(), mouseLoc.Point, e.ClickCount);
		}
		MouseLeftDownInfo? mouseLeftDownInfo;

		struct MouseLeftDownInfo {
			public VirtualSnapshotSpan Span { get; }
			public Point Point { get; }
			public int Clicks { get; }
			public MouseLeftDownInfo(VirtualSnapshotSpan span, Point point, int clicks) {
				Span = span;
				Point = point;
				Clicks = clicks;
			}
		}

		public override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
			mouseLeftDownInfo = null;
			if (mouseCaptured) {
				StopScrolling();
				wpfTextView.VisualElement.ReleaseMouseCapture();
				mouseCaptured = false;
				// We're always called, so don't mark it as handled
				// e.Handled = true;
				return;
			}
		}

		VirtualSnapshotSpan GetSelectionOrCaretIfNoSelection() {
			VirtualSnapshotPoint start, end;
			GetSelectionOrCaretIfNoSelection(out start, out end);
			return new VirtualSnapshotSpan(start, end);
		}

		void GetSelectionOrCaretIfNoSelection(out VirtualSnapshotPoint start, out VirtualSnapshotPoint end) {
			if (!wpfTextView.Selection.IsEmpty) {
				start = wpfTextView.Selection.Start;
				end = wpfTextView.Selection.End;
			}
			else {
				start = wpfTextView.Caret.Position.VirtualBufferPosition;
				end = wpfTextView.Caret.Position.VirtualBufferPosition;
			}
		}

		public override void OnMouseMove(object sender, MouseEventArgs e) {
			if (e.LeftButton == MouseButtonState.Pressed) {
				if (!mouseCaptured && mouseLeftDownInfo != null) {
					var mouseLoc = GetLocation(e);
					var dist = mouseLeftDownInfo.Value.Point - mouseLoc.Point;
					bool movedEnough = Math.Abs(dist.X) >= SystemParameters.MinimumHorizontalDragDistance ||
									   Math.Abs(dist.Y) >= SystemParameters.MinimumVerticalDragDistance;
					if (movedEnough && wpfTextView.VisualElement.CaptureMouse()) {
						mouseCaptured = true;
						e.Handled = true;
						return;
					}
				}
				else if (mouseCaptured) {
					e.Handled = true;
					Debug.Assert(mouseLeftDownInfo != null);
					if (mouseLeftDownInfo == null)
						StopScrolling();
					else if (mouseLeftDownInfo.Value.Clicks == 2 || mouseLeftDownInfo.Value.Clicks == 3) {
						Debug.Assert(dispatcherTimer == null);
						StopScrolling();

						var mouseLoc = GetLocation(e);
						wpfTextView.Caret.MoveTo(mouseLoc.Position);

						wpfTextView.Selection.Mode = TextSelectionMode.Stream;
						if (mouseLeftDownInfo.Value.Clicks == 2)
							wpfTextView.EditorOperations.SelectCurrentWord();
						else
							wpfTextView.EditorOperations.SelectLine(wpfTextView.Caret.ContainingTextViewLine, false);
						VirtualSnapshotPoint selStart, selEnd;
						GetSelectionOrCaretIfNoSelection(out selStart, out selEnd);

						VirtualSnapshotPoint anchorPoint, activePoint;
						if (selStart < mouseLeftDownInfo.Value.Span.Start) {
							activePoint = selStart;
							anchorPoint = mouseLeftDownInfo.Value.Span.End;
						}
						else {
							activePoint = selEnd;
							anchorPoint = mouseLeftDownInfo.Value.Span.Start;
						}
						wpfTextView.Selection.Select(anchorPoint, activePoint);
						wpfTextView.Caret.MoveTo(activePoint);
						wpfTextView.Caret.EnsureVisible();
					}
					else {
						SelectToMousePosition(e, true);
						UpdateScrolling(e);
					}
					return;
				}
			}
		}
		bool mouseCaptured;
		DispatcherTimer dispatcherTimer;

		void StopScrolling() {
			dispatcherTimer?.Stop();
			dispatcherTimer = null;
		}

		void UpdateScrolling(MouseEventArgs e) {
			StopScrolling();
			wpfTextView.Caret.EnsureVisible();

			var mouseLoc = GetLocation(e);
			TimeSpan interval;
			var scrollDir = GetScrollDirection(mouseLoc, out interval);
			if (scrollDir == null)
				return;

			dispatcherTimer = new DispatcherTimer(interval, DispatcherPriority.Normal, (s, e2) => OnScroll(scrollDir.Value, mouseLoc.Point.X), wpfTextView.VisualElement.Dispatcher);
			OnScroll(scrollDir.Value, mouseLoc.Point.X);
		}

		ScrollDirection? GetScrollDirection(MouseLocation mouseLoc, out TimeSpan interval) {
			// Give prio to scrolling up/down (more common than scrolling left/right)
			if (mouseLoc.Point.Y < wpfTextView.ViewportTop) {
				interval = GetVerticalInterval(mouseLoc.Point.Y - wpfTextView.ViewportTop);
				return ScrollDirection.Up;
			}
			if (mouseLoc.Point.Y >= wpfTextView.ViewportBottom) {
				interval = GetVerticalInterval(mouseLoc.Point.Y - wpfTextView.ViewportBottom);
				return ScrollDirection.Down;
			}
			if (mouseLoc.Point.X < wpfTextView.ViewportLeft) {
				interval = GetHorizontalInterval(mouseLoc.Point.X - wpfTextView.ViewportLeft);
				return ScrollDirection.Left;
			}
			if (mouseLoc.Point.X >= wpfTextView.ViewportRight) {
				interval = GetHorizontalInterval(mouseLoc.Point.X - wpfTextView.ViewportRight);
				return ScrollDirection.Right;
			}
			interval = TimeSpan.Zero;
			return null;
		}

		TimeSpan GetVerticalInterval(double dist) => GetInterval(dist, wpfTextView.LineHeight);
		TimeSpan GetHorizontalInterval(double dist) => GetInterval(dist, DefaultCharacterWidth);
		TimeSpan GetInterval(double dist, double length) {
			const double SCROLL_INTERVAL_MS = 250;
			if (Math.Abs(dist) < 2 * length)
				return TimeSpan.FromMilliseconds(SCROLL_INTERVAL_MS);
			return TimeSpan.FromMilliseconds(SCROLL_INTERVAL_MS / 16);
		}

		double DefaultCharacterWidth => wpfTextView.FormattedLineSource.ColumnWidth;

		enum ScrollDirection {
			Left,
			Right,
			Up,
			Down,
		}

		void OnScroll(ScrollDirection value, double xCoordinate) {
			ITextViewLine line;
			switch (value) {
			case ScrollDirection.Left:
				line = wpfTextView.Caret.ContainingTextViewLine;
				if (line.TextLeft >= wpfTextView.ViewportLeft)
					StopScrolling();
				else if (wpfTextView.Caret.InVirtualSpace || wpfTextView.Caret.Position.BufferPosition != line.Start)
					wpfTextView.EditorOperations.MoveToPreviousCharacter(true);
				else {
					wpfTextView.ViewportLeft = line.TextLeft;
					StopScrolling();
				}
				break;

			case ScrollDirection.Right:
				line = wpfTextView.Caret.ContainingTextViewLine;
				if (line.TextRight <= wpfTextView.ViewportRight)
					StopScrolling();
				else if (wpfTextView.Caret.InVirtualSpace || wpfTextView.Caret.Position.BufferPosition < line.End)
					wpfTextView.EditorOperations.MoveToNextCharacter(true);
				else {
					wpfTextView.ViewportLeft = Math.Max(0, line.TextRight - wpfTextView.ViewportWidth);
					StopScrolling();
				}
				break;

			case ScrollDirection.Up:
				line = wpfTextView.TextViewLines.FirstVisibleLine;
				if (line.VisibilityState == VisibilityState.FullyVisible && !line.IsFirstDocumentLine())
					line = wpfTextView.GetTextViewLineContainingBufferPosition(line.Start - 1);
				wpfTextView.DisplayTextLineContainingBufferPosition(line.Start, 0, ViewRelativePosition.Top);
				if (line.IsFirstDocumentLine())
					StopScrolling();
				wpfTextView.EditorOperations.MoveCaret(line, xCoordinate, true);
				break;

			case ScrollDirection.Down:
				line = wpfTextView.TextViewLines.LastVisibleLine;
				if (line.VisibilityState == VisibilityState.FullyVisible && !line.IsLastDocumentLine())
					line = wpfTextView.GetTextViewLineContainingBufferPosition(line.EndIncludingLineBreak);
				wpfTextView.DisplayTextLineContainingBufferPosition(line.Start, 0, ViewRelativePosition.Bottom);
				if (line.IsLastDocumentLine())
					StopScrolling();
				wpfTextView.EditorOperations.MoveCaret(line, xCoordinate, true);
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(value));
			}

			wpfTextView.Caret.EnsureVisible();
		}
	}
}