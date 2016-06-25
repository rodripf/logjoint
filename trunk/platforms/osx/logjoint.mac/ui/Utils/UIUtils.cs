﻿using System;
using MonoMac.AppKit;
using System.Drawing;
using LJD = LogJoint.Drawing;

namespace LogJoint.UI
{
	public static class UIUtils
	{
		public static void MoveToPlaceholder(this NSView customControlView, NSView placeholder)
		{
			placeholder.AddSubview (customControlView);
			var placeholderSize = placeholder.Frame.Size;
			customControlView.Frame = new System.Drawing.RectangleF(0, 0, placeholderSize.Width, placeholderSize.Height);
			customControlView.AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;
		}

		public static void InvalidateCursorRects(this NSView view)
		{
			if (view.Window != null)
				view.Window.InvalidateCursorRectsForView(view);
		}

		public static PointF GetEventLocation(this NSView view, NSEvent e)
		{
			return view.ConvertPointFromView(e.LocationInWindow, null);
		}

		public static RectangleF FocusedItemMarkFrame
		{
			get { return new RectangleF(0f, -3.5f, 3.5f, 7f); }
		}

		public static void DrawDebugLine(LJD.Graphics g, float x, float y)
		{
			g.DrawLine(red, new PointF(x, y - 5), new PointF(x, y + 5));
		}

		public static void DrawFocusedItemMark(LJD.Graphics g, float x, float y, bool drawOuterFrame = false)
		{
			if (drawOuterFrame)
			{
				g.FillPolygon(white, MakeFocusedItemMarkPoints(x - 1f, y, new SizeF(
					FocusedItemMarkFrame.Size.Width + 2.3f, FocusedItemMarkFrame.Size.Height + 4.4f)));
			}
			g.FillPolygon(blue, MakeFocusedItemMarkPoints(x, y, FocusedItemMarkFrame.Size));
		}

		static PointF[] MakeFocusedItemMarkPoints(float x, float y, SizeF sz)
		{
			var focusedItemMarkPoints = new PointF[]
			{
				new PointF(x, y-sz.Height/2),
				new PointF(x+sz.Width, y),
				new PointF(x, y+sz.Height/2)
			};
			return focusedItemMarkPoints;
		}

		public static void DrawBookmark(LJD.Graphics g, float x, float y)
		{
			// todo: stub. impl properly.
			g.FillRoundRectangle(blue, new RectangleF(x, y - 3, 8, 6), 2);
		}

		static LJD.Brush blue = new LJD.Brush(Color.Blue);
		static LJD.Brush white = new LJD.Brush(Color.White);
		static LJD.Pen red = new LJD.Pen(Color.Red, 1);
	}
}

