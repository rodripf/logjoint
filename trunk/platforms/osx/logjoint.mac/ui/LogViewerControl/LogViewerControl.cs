﻿﻿﻿
using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using AppKit;
using System.Drawing;
using LogJoint.UI.Presenters.LogViewer;
using ObjCRuntime;
using CoreGraphics;
using LogJoint.Drawing;
                  
namespace LogJoint.UI
{
	public partial class LogViewerControl : AppKit.NSView
	{
		#region Constructors

		// Called when created from unmanaged code
		public LogViewerControl(IntPtr handle)
			: base(handle)
		{
			Initialize();
		}
		
		// Called when created directly from a XIB file
		[Export("initWithCoder:")]
		public LogViewerControl(NSCoder coder)
			: base(coder)
		{
			Initialize();
		}
		
		// Shared initialization code
		void Initialize()
		{
		}

		#endregion

		public void Init(LogViewerControlAdapter owner)
		{
			this.owner = owner;
		}

		public override void DrawRect(CGRect dirtyRect)
		{
			owner.OnPaint(dirtyRect.ToRectangle());
		}

		public override bool IsFlipped
		{
			get
			{
				return true;
			}
		}

		public override bool AcceptsFirstResponder()
		{
			return true;
		}

		public override void KeyDown(NSEvent theEvent)
		{
			this.InterpretKeyEvents(new [] { theEvent });
		}

		public override bool BecomeFirstResponder()
		{
			owner.isFocused = true;
			owner.viewModel?.ChangeNotification?.Post ();
			return base.BecomeFirstResponder();
		}

		public override bool ResignFirstResponder()
		{
			owner.isFocused = false;
			owner.viewModel?.ChangeNotification?.Post ();
			return base.ResignFirstResponder();
		}

		public override CGRect AdjustScroll (CGRect newVisible)
		{
			newVisible.Y = 0;
			return newVisible;
		}

		[Export ("insertText:")]
		void OnInsertText (NSObject theEvent)
		{
			var s = theEvent.ToString();
			if (s == "b" || s == "B")
			{
				owner.viewModel.OnKeyPressed(Key.BookmarkShortcut);
			}

			if (s == "[")
			{
				owner.viewModel.OnKeyPressed(Key.PageUp);
			}
			else if (s == "]")
			{
				owner.viewModel.OnKeyPressed(Key.PageDown);
			}
			else if (s == "h")
			{
				owner.viewModel.OnKeyPressed(Key.BeginOfLine);
			}
			else if (s == "H")
			{
				owner.viewModel.OnKeyPressed(Key.BeginOfDocument);
			}
			else if (s == "e")
			{
				owner.viewModel.OnKeyPressed(Key.EndOfLine);
			}
			else if (s == "E")
			{
				owner.viewModel.OnKeyPressed(Key.EndOfDocument);
			}
			else if (s == "w" || s == "W")
			{
				owner.viewModel.OnKeyPressed(Key.Up | Key.AlternativeModeModifier);
			}
			else if (s == "s" || s == "S")
			{
				owner.viewModel.OnKeyPressed(Key.Down | Key.AlternativeModeModifier);
			}
			else if (s == "<")
			{
				owner.viewModel.OnKeyPressed(Key.PrevHighlightedMessage);
			}
			else if (s == ">")
			{
				owner.viewModel.OnKeyPressed(Key.NextHighlightedMessage);
			}
		}


		[Export ("moveUp:")]
		void OnMoveUp (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.Up | GetModifiers());
		}

		[Export ("moveDown:")]
		void OnMoveDown (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.Down | GetModifiers());
		}

		[Export ("moveRight:")]
		void OnMoveRight (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.Right | GetModifiers());
		}

		[Export ("moveLeft:")]
		void OnMoveLeft (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.Left | GetModifiers());
		}

		[Export ("moveLeftAndModifySelection:")]
		void OnMoveLeftAndModifySelection (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.Left | Key.ModifySelectionModifier);
		}

		[Export ("moveRightAndModifySelection:")]
		void OnMoveRightAndModifySelection (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.Right | Key.ModifySelectionModifier);
		}

		[Export ("moveUpAndModifySelection:")]
		void OnMoveUpAndModifySelection (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.Up | Key.ModifySelectionModifier);
		}

		[Export ("moveDownAndModifySelection:")]
		void OnMoveDownAndModifySelection (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.Down | Key.ModifySelectionModifier);
		}

		[Export ("moveToBeginningOfLine:")]
		void OnMoveToBeginningOfLine (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.BeginOfLine);
		}

		[Export ("moveToBeginningOfLineAndModifySelection:")]
		void OnMoveToBeginningOfLineAndModifySelection (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.BeginOfLine | Key.ModifySelectionModifier);
		}

		[Export ("moveToEndOfLine:")]
		void OnMoveToEndOfLine (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.EndOfLine);
		}

		[Export ("moveToEndOfLineAndModifySelection:")]
		void OnMoveToEndOfLineAndModifySelection (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.EndOfLine  | Key.ModifySelectionModifier);
		}

		[Export ("pageDown:")]
		void OnPageDown (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.PageDown);
		}

		[Export ("pageDownAndModifySelection:")]
		void OnPageDownAndModifySelection (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.PageDown | Key.ModifySelectionModifier);
		}

		[Export ("pageUp:")]
		void OnPageUp (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.PageUp);
		}

		[Export ("pageUpAndModifySelection:")]
		void OnPageUpAndModifySelection (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.PageUp | Key.ModifySelectionModifier);
		}


		#region: scrollXXX actions that are deliberately implemented to modify selection

		[Export ("scrollPageUp:")]
		void OnScrollPageUp (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.PageUp | GetModifiers());
		}
			
		[Export ("scrollPageDown:")]
		void OnScrollPageDown (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.PageDown | GetModifiers());
		}

		[Export ("scrollToBeginningOfDocument:")]
		void OnScrollToBeginningOfDocument (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.BeginOfLine | GetModifiers());
		}

		[Export ("scrollToEndOfDocument:")]
		void OnScrollToEndOfDocument (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.EndOfLine | GetModifiers());
		}

		[Export ("moveToBeginningOfDocument:")]
		void OnMoveToBeginningOfDocument (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.BeginOfDocument);
		}

		[Export ("moveToEndOfDocument:")]
		void OnMoveToEndOfDocument (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.EndOfDocument);
		}

		#endregion


		[Export ("validateMenuItem:")]
		bool OnValidateMenuItem (NSMenuItem item)
		{
			return true;
		}

		[Export ("copy:")]
		void OnCopy (NSObject item)
		{
			owner.viewModel.OnKeyPressed(Key.Copy | GetModifiers());
		}

		[Export ("insertNewline:")]
		void OnInsertNewline (NSObject theEvent)
		{
			owner.viewModel.OnKeyPressed(Key.Enter | GetModifiers());
		}

		public override void ScrollWheel(NSEvent theEvent)
		{
			owner.OnScrollWheel(theEvent);
		}

		public override void MouseDown(NSEvent theEvent)
		{
			owner.OnMouseDown(theEvent);
			base.MouseDown(theEvent);
		}

		public override void MouseMoved(NSEvent theEvent)
		{
			owner.OnMouseMove(theEvent, false);
			base.MouseMoved(theEvent);
		}

		public override void MouseDragged(NSEvent theEvent)
		{
			owner.OnMouseMove(theEvent, true);
			base.MouseDragged(theEvent);
		}

		public override void ResetCursorRects()
		{
			if (owner?.EnableCursor == true && owner?.ViewDrawing != null)
			{
				var r = Bounds; 
				r.Offset(owner.ViewDrawing.ServiceInformationAreaSize, 0);
				var visiblePart = this.ConvertRectFromView(Superview.Bounds, Superview);
				r.Intersect(visiblePart);
				AddCursorRect(r, NSCursor.IBeamCursor);
			}
		}

		public override void DiscardCursorRects()
		{
			base.DiscardCursorRects();
		}

		static Key GetModifiers()
		{
			var ret = Key.None;
			var theEvent = NSApplication.SharedApplication.CurrentEvent;
			if (theEvent != null)
			{
				if ((theEvent.ModifierFlags & NSEventModifierMask.AlternateKeyMask) != 0)
					ret |= Key.AlternativeModeModifier;
				if ((theEvent.ModifierFlags & NSEventModifierMask.ShiftKeyMask) != 0)
					ret |= Key.ModifySelectionModifier;
				if ((theEvent.ModifierFlags & NSEventModifierMask.ControlKeyMask) != 0)
					ret |= Key.JumpOverWordsModifier;
			}
			return ret; 
		}

		LogViewerControlAdapter owner;
	}
}

