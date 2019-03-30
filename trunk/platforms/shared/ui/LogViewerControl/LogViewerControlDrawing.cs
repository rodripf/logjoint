using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LogJoint.UI.Presenters.LogViewer;
using LogJoint.Drawing;
using RectangleF = System.Drawing.RectangleF;
using Rectangle = System.Drawing.Rectangle;
using PointF = System.Drawing.PointF;
using Point = System.Drawing.Point;
using SizeF = System.Drawing.SizeF;
using Size = System.Drawing.Size;
using Color = System.Drawing.Color;


namespace LogJoint.UI
{
	internal class DrawingVisitor : MessageTextHandlingVisitor, IMessageVisitor
	{
		public int DisplayIndex;
		public int TextLineIdx;
		public bool IsBookmarked;
		public IHighlightingHandler SearchResultHighlightingHandler;
		public IHighlightingHandler SelectionHighlightingHandler;
		public IHighlightingHandler HighlightingFiltersHandler;
		public Presenters.LogViewer.CursorPosition? CursorPosition;

		public override void Visit(IMessage msg)
		{
			base.Visit(msg);

			DrawTime(msg);

			DrawStringWithInplaceHightlight(msg, ctx.InfoMessagesBrush, m.OffsetTextRect.Location);

			DrawCursorIfNeeded(msg);

			FillOutlineBackground();
			DrawContentOutline(msg);
		}

		protected override void HandleMessageText(IMessage msg, float textXPos)
		{
			DrawMessageBackground(msg, textXPos);
		}



		void DrawTime(IMessage msg)
		{
			if (ctx.ShowTime && TextLineIdx == 0)
			{
				ctx.Canvas.DrawString(msg.Time.ToUserFrendlyString(ctx.ShowMilliseconds),
					ctx.Font,
					ctx.InfoMessagesBrush,
					m.TimePos.X, m.TimePos.Y);
			}
		}

		void FillOutlineBackground()
		{
			ctx.Canvas.FillRectangle(ctx.DefaultBackgroundBrush, new Rectangle(0,
				m.MessageRect.Y, ctx.CollapseBoxesAreaSize, m.MessageRect.Height));
		}

		void DrawContentOutline(IMessage msg)
		{
			Image icon = null;
			Image icon2 = null;
			if (this.TextLineIdx == 0)
			{
				if (msg.Severity == SeverityFlag.Error)
					icon = ctx.ErrorIcon;
				else if (msg.Severity == SeverityFlag.Warning)
					icon = ctx.WarnIcon;
			}
			if (IsBookmarked)
				if (icon == null)
					icon = ctx.BookmarkIcon;
				else
					icon2 = ctx.BookmarkIcon;
			if (icon == null)
				return;
			int w = ctx.CollapseBoxesAreaSize;
			var iconSz = icon.GetSize(width: icon2 == null ? (icon == ctx.BookmarkIcon ? 12 : 10) : 9).Scale(ctx.DpiScale);
			ctx.Canvas.DrawImage(icon,
				icon2 == null ? (w - iconSz.Width) / 2 : 1,
				m.MessageRect.Y + (ctx.LineHeight - iconSz.Height) / 2,
				iconSz.Width,
				iconSz.Height
			);
			if (icon2 != null)
			{
				var icon2Sz = icon2.GetSize(width: 9).Scale(ctx.DpiScale);
				ctx.Canvas.DrawImage(icon2,
					iconSz.Width + 2,
					m.MessageRect.Y + (ctx.LineHeight - icon2Sz.Height) / 2,
					icon2Sz.Width,
					icon2Sz.Height
				);
			}
		}
		
		void DrawMessageBackground(IMessage msg, float textXPos)
		{
			DrawContext dc = ctx;
			Rectangle r = m.MessageRect;
			r.Offset(ctx.CollapseBoxesAreaSize, 0);
			Brush b = null;
			Brush tmpBrush = null;

			if (msg.Thread != null)
			{
				var coloring = dc.Coloring;
				if (coloring == Settings.Appearance.ColoringMode.None)
					b = dc.DefaultBackgroundBrush;
				else if (msg.Thread.IsDisposed)
					b = dc.DefaultBackgroundBrush;
				else if (coloring == Settings.Appearance.ColoringMode.Threads)
					b = tmpBrush = new Brush (msg.Thread.ThreadColor.ToColor ());
				else if (coloring == Settings.Appearance.ColoringMode.Sources) {
					if (msg.TryGetLogSource (out var ls))
						b = tmpBrush = new Brush (ls.Color.ToColor ());
					else
						b = dc.DefaultBackgroundBrush;
				}
			}
			if (b == null)
			{
				b = dc.DefaultBackgroundBrush;
			}
			dc.Canvas.FillRectangle(b, r);

			var normalizedSelection = dc.NormalizedSelection;
			if (!normalizedSelection.IsEmpty
			 && DisplayIndex >= normalizedSelection.First.DisplayIndex
			 && DisplayIndex <= normalizedSelection.Last.DisplayIndex)
			{
				int selectionStartIdx;
				int selectionEndIdx;
				var line = dc.GetTextToDisplay(msg).GetNthTextLine(TextLineIdx);
				if (DisplayIndex == normalizedSelection.First.DisplayIndex)
					selectionStartIdx = normalizedSelection.First.LineCharIndex;
				else
					selectionStartIdx = 0;
				if (DisplayIndex == normalizedSelection.Last.DisplayIndex)
					selectionEndIdx = normalizedSelection.Last.LineCharIndex;
				else
					selectionEndIdx = line.Length;
				if (selectionStartIdx < selectionEndIdx && selectionStartIdx >= 0 && selectionEndIdx <= line.Value.Length)
				{
					RectangleF tmp = DrawingUtils.GetLineSubstringBounds(
						line.Value, selectionStartIdx, selectionEndIdx,
						ctx.Canvas, dc.Font, ctx.TextFormat,
						m.MessageRect, m.OffsetTextRect.X + textXPos);
					dc.Canvas.FillRectangle(dc.SelectedBkBrush, tmp);
				}
			}

			if (ctx.ShowTime)
			{
				float x = ctx.CollapseBoxesAreaSize + ctx.TimeAreaSize - ctx.ScrollPos.X - 2;
				if (x > ctx.CollapseBoxesAreaSize)
					ctx.Canvas.DrawLine(ctx.TimeSeparatorLine, x, m.MessageRect.Y, x, m.MessageRect.Bottom);
			}

			if (tmpBrush != null)
				tmpBrush.Dispose();
		}

		void DrawCursorIfNeeded(IMessage msg)
		{
			if (!CursorPosition.HasValue)
				return;
			msg.Visit(new DrawCursorVisitor() { ctx = ctx, m = m, pos = CursorPosition.Value });
		}

		static void FillInplaceHightlightRectangle(DrawContext ctx, RectangleF rect, Brush brush)
		{
			ctx.Canvas.PushState();
			ctx.Canvas.EnableAntialiasing(true);
			ctx.Canvas.FillRoundRectangle(brush, RectangleF.Inflate(rect, 2, 0), 3);
			ctx.Canvas.PopState();
		}

		void DrawStringWithInplaceHightlight(IMessage msg, Brush brush, PointF location)
		{
			var textToDisplay = msg.GetDisplayText(ctx.ShowRawMessages);
			var text = textToDisplay.Text;
			var line = textToDisplay.GetNthTextLine(this.TextLineIdx);

			int lineBegin = line.StartIndex - text.StartIndex;
			int lineEnd = lineBegin + line.Length;
			DoInplaceHighlighting(msg, location, text, lineBegin, lineEnd, SearchResultHighlightingHandler, ctx.SearchResultHighlightingBackground);
			DoInplaceHighlighting(msg, location, text, lineBegin, lineEnd, SelectionHighlightingHandler, ctx.SelectionHighlightingBackground);
			DoInplaceHighlighting(msg, location, text, lineBegin, lineEnd, HighlightingFiltersHandler, null);

			ctx.Canvas.DrawString(line.Value, ctx.Font, brush, location, ctx.TextFormat);
		}

		private void DoInplaceHighlighting(
			IMessage msg, 
			PointF location, 
			StringSlice text, 
			int lineBegin, int lineEnd,
			IHighlightingHandler handler,
			Brush brush)
		{
			if (handler != null)
			{
				foreach (var hlRange in handler.GetHighlightingRanges(msg, lineBegin, lineEnd))
				{
					int? hlBegin = null;
					int? hlEnd = null;
					if (hlRange.Item1 >= lineBegin && hlRange.Item1 <= lineEnd)
						hlBegin = hlRange.Item1;
					if (hlRange.Item2 >= lineBegin && hlRange.Item2 <= lineEnd)
						hlEnd = hlRange.Item2;
					if (hlBegin != null || hlEnd != null)
					{
						var tmp = DrawingUtils.GetLineSubstringBounds(
							text.SubString(lineBegin, lineEnd - lineBegin).Value, 
							hlBegin.GetValueOrDefault(lineBegin) - lineBegin,
							hlEnd.GetValueOrDefault(lineEnd) - lineBegin,
							ctx.Canvas,
							ctx.Font,
							ctx.TextFormat, 
							m.MessageRect, location.X);
						tmp.Inflate(0, -1);
						if (brush == null)
						{
							var cl = hlRange.Item3.GetBackgroundColor();
							if (cl != null)
							{
								using (var tmpBrush = new Brush(cl.Value.ToColor()))
								{
									FillInplaceHightlightRectangle(ctx, tmp, tmpBrush);
								}
							}
						}
						else
						{
							FillInplaceHightlightRectangle(ctx, tmp, brush);
						}
					}
				}
			}
		}
	};

	internal abstract class MessageTextHandlingVisitor : IMessageVisitor
	{
		public DrawContext ctx;
		public DrawingUtils.Metrics m;

		protected abstract void HandleMessageText(IMessage msg, float textXPos);

		public virtual void Visit(IMessage msg)
		{
			HandleMessageText(msg, 0);
		}
	};

	internal class DrawCursorVisitor : MessageTextHandlingVisitor
	{
		public Presenters.LogViewer.CursorPosition pos;

		protected override void HandleMessageText(IMessage msg, float textXPos)
		{
			DrawContext dc = ctx;

			var line = dc.GetTextToDisplay(msg).GetNthTextLine(pos.TextLineIndex);
			var lineCharIdx = pos.LineCharIndex;
			
			if (lineCharIdx > line.Value.Length)
				return; // defensive measure to avoid crash in UI thread

			RectangleF tmp = DrawingUtils.GetLineSubstringBounds(
				line.Value + '*', lineCharIdx, lineCharIdx + 1,
				dc.Canvas, dc.Font, ctx.TextFormat,
				m.MessageRect, m.OffsetTextRect.X + textXPos
			);

			dc.Canvas.DrawLine(dc.CursorPen, tmp.X, tmp.Top, tmp.X, tmp.Bottom);
		}
	};

	internal class HitTestingVisitor : MessageTextHandlingVisitor
	{
		public int TextLineIndex;
		public int ClickedPointX;
		public int LineTextPosition;

		public HitTestingVisitor(DrawContext dc, DrawingUtils.Metrics mtx, int clieckedPointX, int lineIndex)
		{
			ctx = dc;
			ClickedPointX = clieckedPointX;
			m = mtx;
			TextLineIndex = lineIndex;
		}

		protected override void HandleMessageText(IMessage msg, float textXPos)
		{
			DrawContext dc = ctx;
			LineTextPosition = DrawingUtils.ScreenPositionToMessageTextCharIndex(dc.Canvas, msg, dc.ShowRawMessages, TextLineIndex, dc.Font, dc.TextFormat,
				(int)(ClickedPointX - textXPos - m.OffsetTextRect.X));
		}
	};

	public class DrawContext
	{
		public IPresentationDataAccess Presenter;
		public SizeF CharSize;
		public double CharWidthDblPrecision;
		public int LineHeight;
		public int TimeAreaSize;
		public int CollapseBoxesAreaSize = 25;
		public float DpiScale = 1f;
		public Brush InfoMessagesBrush;
		public Font Font;
		public Brush CommentsBrush;
		public Brush DefaultBackgroundBrush;
		public Brush SelectedBkBrush;
		public Brush SelectedFocuslessBkBrush;
		public Brush SelectedTextBrush;
		public Brush SelectedFocuslessTextBrush;
		public Brush FocusedMessageBkBrush;
		public Image ErrorIcon, WarnIcon, BookmarkIcon, SmallBookmarkIcon, FocusedMessageIcon, FocusedMessageSlaveIcon;
		public Pen CursorPen;
		public Pen TimeSeparatorLine;
		public StringFormat TextFormat;
		public Brush SearchResultHighlightingBackground;
		public Brush SelectionHighlightingBackground;
		public Graphics Canvas;

		public bool ShowTime { get { return Presenter != null ? Presenter.ShowTime : false; } }
		public bool ShowMilliseconds { get { return Presenter != null ? Presenter.ShowMilliseconds : false; } }
		public bool ShowRawMessages { get { return Presenter != null ? Presenter.ShowRawMessages : false; } }
		public SelectionInfo NormalizedSelection { get { return Presenter != null ? Presenter.Selection.Normalize() : new SelectionInfo(); } }
		public Settings.Appearance.ColoringMode Coloring { get { return Presenter != null ? Presenter.Coloring : Settings.Appearance.ColoringMode.None; } }

		public bool CursorState;
		public Point ScrollPos;
		public int ViewWidth;

		public int SlaveMessagePositionAnimationStep;

		public Point GetTextOffset(int displayIndex)
		{
			int x = this.CollapseBoxesAreaSize - ScrollPos.X;
			if (ShowTime)
				x += TimeAreaSize;
			int y = displayIndex * LineHeight - ScrollPos.Y;
			return new Point(x, y);
		}
		public StringUtils.MultilineText GetTextToDisplay(IMessage msg)
		{
			return Presenter != null ? 
				msg.GetDisplayText(Presenter.ShowRawMessages) : msg.TextAsMultilineText;
		}
	};

	struct VisibleMessagesIndexes
	{
		public int begin;
		public int end;
		public int fullyVisibleBegin;
		public int fullyVisibleEnd;
	};

	static class DrawingUtils
	{
		public struct Metrics
		{
			public Rectangle MessageRect;
			public Point TimePos;
			public Rectangle OffsetTextRect;
		};

		public static Metrics GetMetrics(ViewLine line, DrawContext dc)
		{
			Point offset = dc.GetTextOffset(line.LineIndex);

			Metrics m;

			m.MessageRect = new Rectangle(
				0,
				offset.Y,
				dc.ViewWidth,
				dc.LineHeight
			);

			m.TimePos = new Point(
				dc.CollapseBoxesAreaSize - dc.ScrollPos.X,
				m.MessageRect.Y
			);

			int charCount = dc.GetTextToDisplay(line.Message).GetNthTextLine(line.TextLineIndex).Length;

			m.OffsetTextRect = new Rectangle(
				offset.X,
				m.MessageRect.Y,
				(int)((double)charCount * dc.CharWidthDblPrecision),
				m.MessageRect.Height
			);

			return m;
		}

		public static RectangleF GetLineSubstringBounds(
			string line, int lineSubstringBegin, int lineSubstringEnd, 
			Graphics g, Font font, StringFormat format, 
			RectangleF messageRect, float textDrawingXPosition
		)
		{
			var bounds = g.MeasureCharacterRange(line, font, format, new System.Drawing.CharacterRange(lineSubstringBegin, lineSubstringEnd - lineSubstringBegin));

			return new RectangleF(textDrawingXPosition + bounds.X, messageRect.Top, bounds.Width, messageRect.Height);
		}

		public static int ScreenPositionToMessageTextCharIndex(Graphics g, 
			IMessage msg, bool showRawMessages, int textLineIndex, Font font, StringFormat format, int screenPosition)
		{
			var textToDisplay = msg.GetDisplayText(showRawMessages);
			var txt = textToDisplay.Text;
			var line = textToDisplay.GetNthTextLine(textLineIndex);
			var lineValue = line.Value;
			int lineCharIdx = ListUtils.BinarySearch(new ListUtils.VirtualList<int>(lineValue.Length, i => i), 0, lineValue.Length, i =>
			{
				var charBounds = g.MeasureCharacterRange(lineValue, font, format, new System.Drawing.CharacterRange(i, 1));
				return ((charBounds.Left + charBounds.Right) / 2) < screenPosition;
			});
			//return (line.StartIndex + lineCharIdx) - txt.StartIndex;
			return lineCharIdx;
		}

		public static IEnumerable<ViewLine> GetVisibleMessagesIterator(DrawContext drawContext, IPresentationDataAccess presentationDataAccess, Rectangle viewRect)
		{
			var vl = DrawingUtils.GetVisibleMessages(drawContext, presentationDataAccess, viewRect);
			return presentationDataAccess.GetViewLines(vl.begin, vl.end);
		}

		public static VisibleMessagesIndexes GetVisibleMessages(DrawContext drawContext, IPresentationDataAccess presentationDataAccess, Rectangle viewRect)
		{
			VisibleMessagesIndexes rv;

			viewRect.Offset(0, drawContext.ScrollPos.Y);

			rv.begin = viewRect.Y / drawContext.LineHeight;
			rv.fullyVisibleBegin = rv.begin;
			if ((viewRect.Y % drawContext.LineHeight) != 0)
				++rv.fullyVisibleBegin;

			rv.end = viewRect.Bottom / drawContext.LineHeight;
			rv.fullyVisibleEnd = rv.end;
			--rv.fullyVisibleEnd;
			if ((viewRect.Bottom % drawContext.LineHeight) != 0)
				++rv.end;

			int availableLines = presentationDataAccess.ViewLinesCount;
			rv.begin = Math.Min(availableLines, rv.begin);
			rv.end = Math.Min(availableLines, rv.end);
			rv.fullyVisibleEnd = Math.Min(availableLines, rv.fullyVisibleEnd);

			return rv;
		}

		public static void PaintControl(DrawContext drawContext, IPresentationDataAccess presentationDataAccess, 
			SelectionInfo selection, bool controlIsFocused, Rectangle dirtyRect, out int maxRight)
		{
			var drawingVisitor = new DrawingVisitor();
			drawingVisitor.ctx = drawContext;
			drawingVisitor.SearchResultHighlightingHandler = presentationDataAccess.SearchResultHighlightingHandler; 
			drawingVisitor.SelectionHighlightingHandler = presentationDataAccess.SelectionHighlightingHandler;
			drawingVisitor.HighlightingFiltersHandler = presentationDataAccess.HighlightingFiltersHandler;

			maxRight = 0;
			var sel = selection;
			bool needToDrawCursor = drawContext.CursorState == true && controlIsFocused && sel.First.Message != null;

			var messagesToDraw = DrawingUtils.GetVisibleMessages(drawContext, presentationDataAccess, dirtyRect);

			var displayLinesEnum = presentationDataAccess.GetViewLines(messagesToDraw.begin, messagesToDraw.end);
			foreach (var il in displayLinesEnum)
			{
				drawingVisitor.DisplayIndex = il.LineIndex;
				drawingVisitor.TextLineIdx = il.TextLineIndex;
				drawingVisitor.IsBookmarked = il.IsBookmarked;
				DrawingUtils.Metrics m = DrawingUtils.GetMetrics(il, drawContext);
				drawingVisitor.m = m;
				if (needToDrawCursor && sel.First.DisplayIndex == il.LineIndex)
					drawingVisitor.CursorPosition = sel.First;
				else
					drawingVisitor.CursorPosition = null;

				il.Message.Visit(drawingVisitor);

				maxRight = Math.Max(maxRight, m.OffsetTextRect.Right);
			}

			DrawFocusedMessageMark(drawContext, presentationDataAccess, messagesToDraw);
		}

		public static void DrawFocusedMessageMark(DrawContext drawContext, 
			IPresentationDataAccess presentationDataAccess, VisibleMessagesIndexes messagesToDraw)
		{
			var dc = drawContext;
			Image focusedMessageMark = null;
			SizeF focusedMessageSz = new SizeF();
			float markYPos = 0;
			if (presentationDataAccess.FocusedMessageDisplayMode == FocusedMessageDisplayModes.Master)
			{
				var sel = presentationDataAccess.Selection;
				if (sel.First.Message != null)
				{
					focusedMessageMark = dc.FocusedMessageIcon;
					focusedMessageSz = focusedMessageMark.GetSize(height: 14);
					markYPos = dc.GetTextOffset(sel.First.DisplayIndex).Y + (dc.LineHeight - focusedMessageSz.Height) / 2;
				}
			}
			else
			{
				if (presentationDataAccess.ViewLinesCount != 0)
				{
					var slaveModeFocusInfo = presentationDataAccess.FindSlaveModeFocusedMessagePosition(
						Math.Max(messagesToDraw.begin - 4, 0),
						Math.Min(messagesToDraw.end + 4, presentationDataAccess.ViewLinesCount));
					if (slaveModeFocusInfo != null)
					{
						focusedMessageMark = dc.FocusedMessageIcon;
						focusedMessageSz = focusedMessageMark.GetSize(height: 9);
						float yOffset = slaveModeFocusInfo.Item1 != slaveModeFocusInfo.Item2 ?
							(dc.LineHeight - focusedMessageSz.Height) / 2 : -focusedMessageSz.Height / 2;
						markYPos = dc.GetTextOffset(slaveModeFocusInfo.Item1).Y + yOffset;
					}
				}
			}
			if (focusedMessageMark != null)
			{
				var canvas = drawContext.Canvas;
				canvas.PushState();
				canvas.TranslateTransform(
					drawContext.CollapseBoxesAreaSize - focusedMessageSz.Width / 2 + 1,
					markYPos + focusedMessageSz.Height / 2);
				if (dc.SlaveMessagePositionAnimationStep > 0)
				{
					focusedMessageSz = focusedMessageMark.GetSize(height: 10);
					var factors = new float[] { .81f, 1f, 0.9f, .72f, .54f, .36f, .18f, .09f };
					float factor = 1f + 1.4f * factors[dc.SlaveMessagePositionAnimationStep-1];
					canvas.ScaleTransform(factor, factor);
				}
				dc.Canvas.DrawImage(
					focusedMessageMark, new RectangleF(
						-focusedMessageSz.Width/2,
						-focusedMessageSz.Height/2,
						focusedMessageSz.Width,
						focusedMessageSz.Height
					));
				canvas.PopState();
			}
		}


		public static void MouseDownHelper(
			IPresentationDataAccess presentationDataAccess,
			DrawContext drawContext,
			Rectangle clientRectangle,
			IViewEvents viewEvents,
			Point pt,
			MessageMouseEventFlag flags,
			out bool captureTheMouse
		)
		{
			captureTheMouse = true;

			if (presentationDataAccess != null)
			{
				foreach (var i in DrawingUtils.GetVisibleMessagesIterator(drawContext, presentationDataAccess, clientRectangle))
				{
					DrawingUtils.Metrics mtx = DrawingUtils.GetMetrics(i, drawContext);

					// if user clicked line area
					if (mtx.MessageRect.Contains(pt.X, pt.Y))
					{
						var hitTester = new HitTestingVisitor(drawContext, mtx, pt.X, i.TextLineIndex);
						i.Message.Visit(hitTester);
						if ((flags & MessageMouseEventFlag.DblClick) != 0)
						{
							captureTheMouse = false;
						}
						if (pt.X < drawContext.CollapseBoxesAreaSize)
						{
							flags |= MessageMouseEventFlag.OulineBoxesArea;
						}
						viewEvents.OnMessageMouseEvent(i, hitTester.LineTextPosition, flags, pt);
						break;
					}
				}
			}
		}

		public enum CursorType
		{
			Arrow,
			RightToLeftArrow,
			IBeam
		};

		public static void MouseMoveHelper(
			IPresentationDataAccess presentationDataAccess,
			DrawContext drawContext,
			Rectangle clientRectangle,
			IViewEvents viewEvents,
			Point pt,
			bool isLeftDrag,
			out CursorType newCursor
		)
		{
			newCursor = CursorType.Arrow;

			if (presentationDataAccess != null)
			{
				foreach (var i in DrawingUtils.GetVisibleMessagesIterator(drawContext, presentationDataAccess, clientRectangle))
				{
					DrawingUtils.Metrics mtx = DrawingUtils.GetMetrics(i, drawContext);

					if (pt.Y >= mtx.MessageRect.Top && pt.Y < mtx.MessageRect.Bottom)
					{
						if (isLeftDrag)
						{
							var hitTester = new HitTestingVisitor(drawContext, mtx, pt.X, i.TextLineIndex);
							i.Message.Visit(hitTester);
							MessageMouseEventFlag flags = MessageMouseEventFlag.ShiftIsHeld 
								| MessageMouseEventFlag.CapturedMouseMove;
							if (pt.X < drawContext.CollapseBoxesAreaSize)
								flags |= MessageMouseEventFlag.OulineBoxesArea;
							viewEvents.OnMessageMouseEvent(i, hitTester.LineTextPosition, flags, pt);
						}
						if (pt.X < drawContext.CollapseBoxesAreaSize)
							newCursor = CursorType.RightToLeftArrow;
						else if (pt.X >= drawContext.GetTextOffset(0).X)
							newCursor = CursorType.IBeam;
						else
							newCursor = CursorType.Arrow;
					}
				}
			}
		}
	};
}
