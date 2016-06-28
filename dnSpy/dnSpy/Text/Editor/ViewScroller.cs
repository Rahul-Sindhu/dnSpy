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
using System.Linq;
using dnSpy.Contracts.Text;
using dnSpy.Contracts.Text.Editor;
using dnSpy.Contracts.Text.Formatting;

namespace dnSpy.Text.Editor {
	sealed class ViewScroller : IViewScroller {
		readonly ITextView textView;

		public ViewScroller(ITextView textView) {
			this.textView = textView;
		}

		public void EnsureSpanVisible(SnapshotSpan span) =>
			EnsureSpanVisible(new VirtualSnapshotSpan(span), EnsureSpanVisibleOptions.None);
		public void EnsureSpanVisible(SnapshotSpan span, EnsureSpanVisibleOptions options) =>
			EnsureSpanVisible(new VirtualSnapshotSpan(span), options);
		public void EnsureSpanVisible(VirtualSnapshotSpan span, EnsureSpanVisibleOptions options) {
			if (span.Snapshot != textView.TextSnapshot)
				throw new ArgumentException();
			throw new NotImplementedException();//TODO:
		}

		public void ScrollViewportHorizontallyByPixels(double distanceToScroll) =>
			textView.ViewportLeft += distanceToScroll;

		public void ScrollViewportVerticallyByPixels(double distanceToScroll) {
			var lines = textView.TextViewLines;
			if (lines == null)
				return;
			var line = distanceToScroll >= 0 ? lines.FirstVisibleLine : lines.LastVisibleLine;
			textView.DisplayTextLineContainingBufferPosition(line.Start, line.Top - textView.ViewportTop + distanceToScroll, ViewRelativePosition.Top);
		}

		public void ScrollViewportVerticallyByLine(ScrollDirection direction) => ScrollViewportVerticallyByLines(direction, 1);
		public void ScrollViewportVerticallyByLines(ScrollDirection direction, int count) {
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if (direction == ScrollDirection.Up) {
				double pixels = 0;
				var line = textView.TextViewLines.FirstVisibleLine;
				for (int i = 0; i < count; i++) {
					if (i == 0) {
						if (line.VisibilityState == VisibilityState.PartiallyVisible)
							pixels += textView.ViewportTop - line.Top;
						if (line.Start.Position != 0) {
							line = textView.GetTextViewLineContainingBufferPosition(line.Start - 1);
							pixels += line.Height;
						}
					}
					else
						pixels += line.Height;
					if (line.Start.Position == 0)
						break;
					line = textView.GetTextViewLineContainingBufferPosition(line.Start - 1);
				}
				if (pixels != 0)
					ScrollViewportVerticallyByPixels(pixels);
			}
			else {
				double pixels = 0;
				var line = textView.TextViewLines.FirstVisibleLine;
				for (int i = 0; i < count; i++) {
					if (line.IsLastDocumentLine())
						break;
					if (i == 0) {
						if (line.VisibilityState == VisibilityState.FullyVisible)
							pixels += line.Height;
						else {
							pixels += line.Bottom - textView.ViewportTop;
							line = textView.GetTextViewLineContainingBufferPosition(line.EndIncludingLineBreak);
							pixels += line.Height;
						}
					}
					else
						pixels += line.Height;
					if (line.IsLastDocumentLine())
						break;
					line = textView.GetTextViewLineContainingBufferPosition(line.EndIncludingLineBreak);
				}
				if (pixels != 0)
					ScrollViewportVerticallyByPixels(-pixels);
			}
		}

		public bool ScrollViewportVerticallyByPage(ScrollDirection direction) {
			bool hasFullyVisibleLines = textView.TextViewLines.Any(a => a.VisibilityState == VisibilityState.FullyVisible);

			if (direction == ScrollDirection.Up) {
				var firstVisibleLine = textView.TextViewLines.FirstVisibleLine;
				double top;
				if (firstVisibleLine.VisibilityState == VisibilityState.FullyVisible) {
					if (firstVisibleLine.IsFirstDocumentLine())
						return hasFullyVisibleLines;
					top = firstVisibleLine.Top;
				}
				else
					top = firstVisibleLine.Bottom; // Top of next line, which is possibly not in TextViewLines so we can't use its Top prop
				var line = firstVisibleLine;
				// Top is only valid if the line is in TextViewLines, so use this variable to track the correct line top value
				double lineTop = line.Top;
				var prevLine = line;
				while (lineTop + textView.ViewportHeight > top) {
					prevLine = line;
					if (line.IsFirstDocumentLine())
						break;
					line = textView.GetTextViewLineContainingBufferPosition(line.Start - 1);
					lineTop -= line.Height;
				}
				textView.DisplayTextLineContainingBufferPosition(prevLine.Start, 0, ViewRelativePosition.Top);
			}
			else {
				double pixels = textView.ViewportHeight;
				var lastVisibleLine = textView.TextViewLines.LastVisibleLine;
				if (lastVisibleLine.VisibilityState == VisibilityState.FullyVisible) {
					if (lastVisibleLine.IsLastDocumentLine())
						return hasFullyVisibleLines;
				}
				else
					pixels -= textView.ViewportBottom - lastVisibleLine.Top;
				ScrollViewportVerticallyByPixels(-pixels);
			}

			return hasFullyVisibleLines;
		}
	}
}