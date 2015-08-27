﻿/*
    Copyright (C) 2014-2015 de4dot@gmail.com

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

using System.Collections.Generic;
using dndbg.Engine.COM.CorDebug;

namespace dndbg.Engine {
	public sealed class CurrentDebuggerState {
		/// <summary>
		/// Current process or null
		/// </summary>
		public readonly DnProcess Process;

		/// <summary>
		/// Current AppDomain or null
		/// </summary>
		public readonly DnAppDomain AppDomain;

		/// <summary>
		/// Current thread or null
		/// </summary>
		public readonly DnThread Thread;

		internal ICorDebugController Controller {
			get {
				ICorDebugController controller = null;
				if (controller == null && AppDomain != null)
					controller = AppDomain.RawObject;
				if (controller == null && Process != null)
					controller = Process.RawObject;
				return controller;
			}
		}

		public DebuggerStopState[] StopStates {
			get { return stopStates; }
			internal set { stopStates = value ?? new DebuggerStopState[0]; }
		}
		DebuggerStopState[] stopStates;

		public CurrentDebuggerState()
			: this(null, null, null) {
		}

		public CurrentDebuggerState(DnProcess process, DnAppDomain appDomain, DnThread thread) {
			this.StopStates = null;
			this.Process = process;
			this.AppDomain = appDomain;
			this.Thread = thread;
		}

		public DebuggerStopState GetStopState(DebuggerStopReason reason) {
			foreach (var state in stopStates) {
				if (state.Reason == reason)
					return state;
			}
			return null;
		}

		public IEnumerable<DebuggerStopState> GetStopStates(DebuggerStopReason reason) {
			foreach (var state in stopStates) {
				if (state.Reason == reason)
					yield return state;
			}
		}
	}
}
