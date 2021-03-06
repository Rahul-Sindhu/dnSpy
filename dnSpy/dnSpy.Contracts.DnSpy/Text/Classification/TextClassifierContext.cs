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
using System.Threading;
using Microsoft.VisualStudio.Utilities;

namespace dnSpy.Contracts.Text.Classification {
	/// <summary>
	/// <see cref="ITextClassifier"/> context
	/// </summary>
	public class TextClassifierContext : IPropertyOwner {
		/// <summary>
		/// Gets the properties
		/// </summary>
		public PropertyCollection Properties {
			get {
				if (properties == null)
					Interlocked.CompareExchange(ref properties, new PropertyCollection(), null);
				return properties;
			}
		}
		PropertyCollection properties;

		/// <summary>
		/// Gets the text to classify
		/// </summary>
		public string Text { get; }

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="text">Text to classify</param>
		public TextClassifierContext(string text) {
			if (text == null)
				throw new ArgumentNullException(nameof(text));
			Text = text;
		}
	}
}
