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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using ICSharpCode.ILSpy.TreeNodes;
using dnlib.DotNet;

namespace ICSharpCode.ILSpy.AsmEditor.Field
{
	sealed class DeleteFieldDefCommand : IUndoCommand
	{
		const string CMD_NAME = "Delete Field";
		[ExportContextMenuEntry(Header = CMD_NAME,
								Icon = "Images/Delete.png",
								Category = "AsmEd",
								Order = 240)]//TODO: Update Order
		[ExportMainMenuCommand(MenuHeader = CMD_NAME,
							Menu = "_Edit",
							MenuIcon = "Images/Delete.png",
							MenuCategory = "AsmEd",
							MenuOrder = 2100)]//TODO: Set menu order
		sealed class MainMenuEntry : EditCommand
		{
			protected override bool CanExecuteInternal(ILSpyTreeNode[] nodes)
			{
				return DeleteFieldDefCommand.CanExecute(nodes);
			}

			protected override void ExecuteInternal(ILSpyTreeNode[] nodes)
			{
				DeleteFieldDefCommand.Execute(nodes);
			}

			protected override void Initialize(ILSpyTreeNode[] nodes, MenuItem menuItem)
			{
				if (nodes.Length == 1)
					menuItem.Header = string.Format("Delete {0}", nodes[0].Text);
				else
					menuItem.Header = string.Format("Delete {0} fields", nodes.Length);
			}
		}

		static bool CanExecute(ILSpyTreeNode[] nodes)
		{
			return nodes.Length > 0 &&
				nodes.All(n => n is FieldTreeNode);
		}

		static void Execute(ILSpyTreeNode[] nodes)
		{
			if (!CanExecute(nodes))
				return;

			var fieldNodes = nodes.Select(a => (FieldTreeNode)a).ToArray();
			UndoCommandManager.Instance.Add(new DeleteFieldDefCommand(fieldNodes));
		}

		public struct DeleteModelNodes
		{
			ModelInfo[] infos;

			struct ModelInfo
			{
				public readonly TypeDef OwnerType;
				public readonly int FieldIndex;

				public ModelInfo(FieldDef field)
				{
					this.OwnerType = field.DeclaringType;
					this.FieldIndex = this.OwnerType.Fields.IndexOf(field);
					Debug.Assert(this.FieldIndex >= 0);
				}
			}

			public void Delete(FieldTreeNode[] nodes)
			{
				Debug.Assert(infos == null);
				if (infos != null)
					throw new InvalidOperationException();

				infos = new ModelInfo[nodes.Length];

				for (int i = 0; i < infos.Length; i++) {
					var node = nodes[i];

					var info = new ModelInfo(node.FieldDefinition);
					infos[i] = info;
					info.OwnerType.Fields.RemoveAt(info.FieldIndex);
				}
			}

			public void Restore(FieldTreeNode[] nodes)
			{
				Debug.Assert(infos != null);
				if (infos == null)
					throw new InvalidOperationException();
				Debug.Assert(infos.Length == nodes.Length);
				if (infos.Length != nodes.Length)
					throw new InvalidOperationException();

				for (int i = infos.Length - 1; i >= 0; i--) {
					var node = nodes[i];
					var info = infos[i];
					info.OwnerType.Fields.Insert(info.FieldIndex, node.FieldDefinition);
				}

				infos = null;
			}
		}

		DeletableNodes<FieldTreeNode> nodes;
		DeleteModelNodes modelNodes;

		DeleteFieldDefCommand(FieldTreeNode[] fieldNodes)
		{
			this.nodes = new DeletableNodes<FieldTreeNode>(fieldNodes);
		}

		public string Description {
			get { return CMD_NAME; }
		}

		public void Execute()
		{
			nodes.Delete();
			modelNodes.Delete(nodes.Nodes);
		}

		public void Undo()
		{
			modelNodes.Restore(nodes.Nodes);
			nodes.Restore();
		}

		public IEnumerable<ILSpyTreeNode> TreeNodes {
			get { return nodes.Nodes; }
		}

		public void Dispose()
		{
		}
	}

	sealed class CreateFieldDefCommand : IUndoCommand
	{
		const string CMD_NAME = "Create Field";
		[ExportContextMenuEntry(Header = CMD_NAME + "...",
								Icon = "Images/Class.png",
								Category = "AsmEd",
								Order = 240)]//TODO: Update Order
		[ExportMainMenuCommand(MenuHeader = CMD_NAME + "...",
							Menu = "_Edit",
							MenuIcon = "Images/Class.png",
							MenuCategory = "AsmEd",
							MenuOrder = 2100)]//TODO: Set menu order
		sealed class MainMenuEntry : EditCommand
		{
			protected override bool CanExecuteInternal(ILSpyTreeNode[] nodes)
			{
				return CreateFieldDefCommand.CanExecute(nodes);
			}

			protected override void ExecuteInternal(ILSpyTreeNode[] nodes)
			{
				CreateFieldDefCommand.Execute(nodes);
			}
		}

		static bool CanExecute(ILSpyTreeNode[] nodes)
		{
			return nodes.Length == 1 &&
				(nodes[0] is TypeTreeNode || nodes[0].Parent is TypeTreeNode);
		}

		static void Execute(ILSpyTreeNode[] nodes)
		{
			if (!CanExecute(nodes))
				return;

			var ownerNode = nodes[0];
			if (!(ownerNode is TypeTreeNode))
				ownerNode = (ILSpyTreeNode)ownerNode.Parent;
			var typeNode = ownerNode as TypeTreeNode;
			Debug.Assert(typeNode != null);
			if (typeNode == null)
				throw new InvalidOperationException();

			var module = ILSpyTreeNode.GetModule(typeNode);
			Debug.Assert(module != null);
			if (module == null)
				throw new InvalidOperationException();
			var options = FieldDefOptions.Create("MyField", new FieldSig(module.CorLibTypes.Int32));

			var data = new FieldOptionsVM(options, module, MainWindow.Instance.CurrentLanguage, typeNode.TypeDefinition);
			var win = new FieldOptionsDlg();
			win.Title = "Create Field";
			win.DataContext = data;
			win.Owner = MainWindow.Instance;
			if (win.ShowDialog() != true)
				return;

			UndoCommandManager.Instance.Add(new CreateFieldDefCommand((TypeTreeNode)ownerNode, data.CreateFieldDefOptions()));
		}

		readonly TypeTreeNode ownerNode;
		readonly FieldTreeNode fieldNode;

		CreateFieldDefCommand(TypeTreeNode ownerNode, FieldDefOptions options)
		{
			this.ownerNode = ownerNode;
			this.fieldNode = new FieldTreeNode(options.CreateFieldDef());
		}

		public string Description {
			get { return CMD_NAME; }
		}

		public void Execute()
		{
			ownerNode.EnsureChildrenFiltered();
			ownerNode.TypeDefinition.Fields.Add(fieldNode.FieldDefinition);
			ownerNode.AddToChildren(fieldNode);
		}

		public void Undo()
		{
			bool b = ownerNode.Children.Remove(fieldNode) &&
					ownerNode.TypeDefinition.Fields.Remove(fieldNode.FieldDefinition);
			Debug.Assert(b);
			if (!b)
				throw new InvalidOperationException();
		}

		public IEnumerable<ILSpyTreeNode> TreeNodes {
			get { yield return ownerNode; }
		}

		public void Dispose()
		{
		}
	}

	sealed class FieldDefSettingsCommand : IUndoCommand
	{
		const string CMD_NAME = "Field Settings";
		[ExportContextMenuEntry(Header = CMD_NAME + "...",
								Icon = "Images/Settings.png",
								Category = "AsmEd",
								Order = 240)]//TODO: Update Order
		[ExportMainMenuCommand(MenuHeader = CMD_NAME + "...",
							Menu = "_Edit",
							MenuIcon = "Images/Settings.png",
							MenuCategory = "AsmEd",
							MenuOrder = 2100)]//TODO: Set menu order
		sealed class MainMenuEntry : EditCommand
		{
			protected override bool CanExecuteInternal(ILSpyTreeNode[] nodes)
			{
				return FieldDefSettingsCommand.CanExecute(nodes);
			}

			protected override void ExecuteInternal(ILSpyTreeNode[] nodes)
			{
				FieldDefSettingsCommand.Execute(nodes);
			}
		}

		static bool CanExecute(ILSpyTreeNode[] nodes)
		{
			return nodes.Length == 1 &&
				nodes[0] is FieldTreeNode;
		}

		static void Execute(ILSpyTreeNode[] nodes)
		{
			if (!CanExecute(nodes))
				return;

			var fieldNode = (FieldTreeNode)nodes[0];

			var module = ILSpyTreeNode.GetModule(nodes[0]);
			Debug.Assert(module != null);
			if (module == null)
				throw new InvalidOperationException();

			var data = new FieldOptionsVM(new FieldDefOptions(fieldNode.FieldDefinition), module, MainWindow.Instance.CurrentLanguage, fieldNode.FieldDefinition.DeclaringType);
			var win = new FieldOptionsDlg();
			win.DataContext = data;
			win.Owner = MainWindow.Instance;
			if (win.ShowDialog() != true)
				return;

			UndoCommandManager.Instance.Add(new FieldDefSettingsCommand(fieldNode, data.CreateFieldDefOptions()));
		}

		readonly FieldTreeNode fieldNode;
		readonly FieldDefOptions newOptions;
		readonly FieldDefOptions origOptions;
		readonly ILSpyTreeNode origParentNode;
		readonly int origParentChildIndex;
		readonly bool nameChanged;

		FieldDefSettingsCommand(FieldTreeNode fieldNode, FieldDefOptions options)
		{
			this.fieldNode = fieldNode;
			this.newOptions = options;
			this.origOptions = new FieldDefOptions(fieldNode.FieldDefinition);

			this.origParentNode = (ILSpyTreeNode)fieldNode.Parent;
			this.origParentChildIndex = this.origParentNode.Children.IndexOf(fieldNode);
			Debug.Assert(this.origParentChildIndex >= 0);
			if (this.origParentChildIndex < 0)
				throw new InvalidOperationException();

			this.nameChanged = origOptions.Name != newOptions.Name;
		}

		public string Description {
			get { return CMD_NAME; }
		}

		public void Execute()
		{
			if (nameChanged) {
				bool b = origParentChildIndex < origParentNode.Children.Count && origParentNode.Children[origParentChildIndex] == fieldNode;
				Debug.Assert(b);
				if (!b)
					throw new InvalidOperationException();
				origParentNode.Children.RemoveAt(origParentChildIndex);
				newOptions.CopyTo(fieldNode.FieldDefinition);

				origParentNode.AddToChildren(fieldNode);
			}
			else
				newOptions.CopyTo(fieldNode.FieldDefinition);
			fieldNode.RaiseUIPropsChanged();
		}

		public void Undo()
		{
			if (nameChanged) {
				bool b = origParentNode.Children.Remove(fieldNode);
				Debug.Assert(b);
				if (!b)
					throw new InvalidOperationException();

				origOptions.CopyTo(fieldNode.FieldDefinition);
				origParentNode.Children.Insert(origParentChildIndex, fieldNode);
			}
			else
				origOptions.CopyTo(fieldNode.FieldDefinition);
			fieldNode.RaiseUIPropsChanged();
		}

		public IEnumerable<ILSpyTreeNode> TreeNodes {
			get { yield return fieldNode; }
		}

		public void Dispose()
		{
		}
	}
}