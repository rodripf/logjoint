using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LogJoint.UI.Presenters.Timeline;
using LogJoint.UI.Timeline;
using LJD = LogJoint.Drawing;

namespace LogJoint.UI
{
	public partial class TimeLineControl : Control, IView
	{
		#region Data

		IViewModel viewModel;

		ControlDrawing drawing;
		Lazy<int> datesSize;
		int minMarkHeight;
		int containersHeaderAreaHeight;
		int containerControlSize;
		Point? dragPoint;
		TimeLineDragForm dragForm;


		Point? lastToolTipPoint;
		bool toolTipVisible = false;

		readonly UIUtils.FocuslessMouseWheelMessagingFilter focuslessMouseWheelMessagingFilter;

		#endregion

		public TimeLineControl()
		{
			InitializeComponent();
			
			this.SetStyle(ControlStyles.ResizeRedraw, true);
			this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);

			this.focuslessMouseWheelMessagingFilter = new UIUtils.FocuslessMouseWheelMessagingFilter(this);

			this.minMarkHeight = UI.UIUtils.Dpi.ScaleUp(25, 120);

			Func<int, int> makeEven = x => x % 2 != 0 ? (x + 1) : x;
			this.containersHeaderAreaHeight = makeEven(UI.UIUtils.Dpi.ScaleUp(12, 120));
			this.containerControlSize = makeEven(UI.UIUtils.Dpi.ScaleUp(9, 120));

			contextMenu.Opened += delegate(object sender, EventArgs e)
			{
				Invalidate();
			};
			contextMenu.Closed += delegate(object sender, ToolStripDropDownClosedEventArgs e)
			{
				Invalidate();
			};
			this.Disposed += (sender, e) => focuslessMouseWheelMessagingFilter.Dispose();
		}

		#region IView

		void IView.SetViewModel(IViewModel viewModel)
		{
			this.viewModel = viewModel;

			this.drawing = new ControlDrawing(new GraphicsResources(viewModel,
				"Tahoma", Font.Size, 6, new LogJoint.Drawing.Image(this.bookmarkPictureBox.Image)));

			this.datesSize = new Lazy<int>(() =>
			{
				using (LJD.Graphics g = new LJD.Graphics(this.CreateGraphics(), true))
					return drawing.MeasureDatesAreaHeight(g);
			});
		}

		void IView.Invalidate()
		{
			base.Invalidate();
		}

		PresentationMetrics IView.GetPresentationMetrics()
		{
			return ToPresentationMetrics(GetMetrics());
		}

		HitTestResult IView.HitTest(int x, int y)
		{
			return GetMetrics().HitTest(new Point(x, y));
		}

		void IView.TryBeginDrag(int x, int y)
		{
			dragPoint = new Point(x, y);
			viewModel.OnBeginTimeRangeDrag();
		}

		void IView.ResetToolTipPoint(int x, int y)
		{
			if (lastToolTipPoint == null
			 || (Math.Abs(lastToolTipPoint.Value.X - x) + Math.Abs(lastToolTipPoint.Value.Y - y)) > 4)
			{
				OnResetToolTip();
			}
		}

		void IView.UpdateDragViewPositionDuringAnimation(int y, bool topView)
		{
			bool animateDragForm = dragForm != null && dragForm.Visible;
			if (animateDragForm)
			{
				if (topView)
				{
					dragForm.Top = this.PointToScreen(new Point(0, y)).Y - dragForm.Height;
				}
				else
				{
					dragForm.Top = this.PointToScreen(new Point(0, y)).Y;
				}
			}
		}

		void IView.RepaintNow()
		{
			this.Refresh();
		}

		void IView.InterruptDrag()
		{
			StopDragging(false);
		}

		void IView.SetHScoll(bool isVisible, int innerViewWidth)
		{
		}

		#endregion

		#region Control overrides

		protected override void OnPaint(PaintEventArgs pe)
		{
			if (viewModel == null)
				return;
			using (var g = new LJD.Graphics(pe.Graphics))
			{
				drawing.FillBackground(g, LJD.Extensions.ToRectangleF(pe.ClipRectangle));

				Metrics m = GetMetrics();

				var drawInfo = viewModel.OnDraw();
				if (drawInfo == null)
					return;

				drawing.DrawSources(g, drawInfo);
				drawing.DrawContainerControls(g, drawInfo);
				drawing.DrawRulers(g, m, drawInfo);
				drawing.DrawDragAreas(g, m, drawInfo);
				drawing.DrawBookmarks(g, m, drawInfo);
				drawing.DrawCurrentViewTime(g, m, drawInfo);
				drawing.DrawHotTrackRange(g, m, drawInfo);
				drawing.DrawHotTrackDate(g, m, drawInfo);

				DrawFocusRect(pe.Graphics, drawInfo);
			}

			base.OnPaint(pe);
		}

		protected override void OnGotFocus(EventArgs e)
		{
			Invalidate();
			base.OnGotFocus(e);
		}

		protected override void OnLostFocus(EventArgs e)
		{
			Invalidate();
			base.OnLostFocus(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			this.Focus();

			if (e.Button == MouseButtons.Left)
				viewModel.OnLeftMouseDown(e.X, e.Y);
		}

		protected override void OnDoubleClick(EventArgs e)
		{
			Point pt = this.PointToClient(Control.MousePosition);
			viewModel.OnMouseDblClick(pt.X, pt.Y);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (!this.Capture)
				StopDragging(false);

			if (viewModel == null)
			{
				this.Cursor = Cursors.Default;
				return;
			}

			Metrics m = GetMetrics();
			if (dragPoint.HasValue)
			{
				Point mousePt = this.PointToScreen(new Point(dragPoint.Value.X, e.Y));

				if (dragForm == null)
					dragForm = new TimeLineDragForm(this);

				ViewArea area = m.TopDrag.Contains(dragPoint.Value) ? ViewArea.TopDrag : ViewArea.BottomDrag;
				dragForm.Area = area;

				var rslt = viewModel.OnDragging(
					area,
					e.Y - dragPoint.Value.Y +
						(area == ViewArea.TopDrag ? m.TimeLine.Top : m.TimeLine.Bottom)
				);
				DateTime d = rslt.D;
				dragForm.Date = d;

				Point pt1 = this.PointToScreen(new Point());
				Point pt2 = this.PointToScreen(new Point(ClientSize.Width, 0));
				int formHeight = datesSize.Value + StaticMetrics.DragAreaHeight;
				dragForm.SetBounds(
					pt1.X,
					pt1.Y + rslt.Y +
						(area == ViewArea.TopDrag ? -formHeight : 0),
					pt2.X - pt1.X,
					formHeight
				);

				if (!dragForm.Visible)
				{
					dragForm.Visible = true;
					this.Focus();
				}
			}
			else
			{
				var cursor = viewModel.OnMouseMove(e.X, e.Y);

				if (cursor == CursorShape.SizeNS)
					this.Cursor = Cursors.SizeNS;
				else if (cursor == CursorShape.Wait)
					this.Cursor = Cursors.WaitCursor;
				else
					this.Cursor = Cursors.Arrow;
			}
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			if (Control.ModifierKeys == Keys.Control)
				viewModel.OnMouseWheel(e.X, e.Y, -(double)e.Delta / 400, true);
			else
				viewModel.OnMouseWheel(e.X, e.Y, -(double)e.Delta / (2d * (double)Height), false);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			StopDragging(true);
			base.OnMouseUp(e);
		}

		protected override void OnMouseCaptureChanged(EventArgs e)
		{
			StopDragging(false);
			base.OnMouseCaptureChanged(e);
		}

		protected override bool IsInputKey(Keys keyData)
		{
			if (keyData == Keys.Escape)
				return false;
			return base.IsInputKey(keyData);
		}

		protected override bool ProcessDialogKey(Keys keyData)
		{
			return base.ProcessDialogKey(keyData);
		}

		protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, Keys keyData)
		{
			if (keyData == Keys.Escape)
			{
				if (dragPoint.HasValue)
				{
					StopDragging(false);
				}
			}
			return base.ProcessCmdKey(ref msg, keyData);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			viewModel.OnMouseLeave();
			base.OnMouseLeave(e);
		}

		#endregion

		#region Control's event handlers

		private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
		{
			var pt = PointToClient(Control.MousePosition);
			var menuData = viewModel.OnContextMenu(pt.X, pt.Y);

			if (menuData == null)
			{
				e.Cancel = true;
				return;
			}

			resetTimeLineMenuItem.Enabled = menuData.ResetTimeLineMenuItemEnabled;

			zoomToMenuItem.Text = menuData.ZoomToMenuItemText ?? "";
			zoomToMenuItem.Visible = menuData.ZoomToMenuItemText != null;
			zoomToMenuItem.Tag = menuData.ZoomToMenuItemData;
		}

		private void contextMenuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{
			if (e.ClickedItem == resetTimeLineMenuItem)
			{
				viewModel.OnResetTimeLineMenuItemClicked();
			}
			else if (e.ClickedItem == zoomToMenuItem)
			{
				viewModel.OnZoomToMenuItemClicked(zoomToMenuItem.Tag);
			}
		}

		private void toolTipTimer_Tick(object sender, EventArgs e)
		{
			ShowToolTip();
			toolTipTimer.Stop();
		}

		private void contextMenu_Closed(object sender, ToolStripDropDownClosedEventArgs e)
		{
			viewModel.OnContextMenuClosed();
		}

		#endregion

		#region Implementation

		public void DrawDragArea(Graphics g, DateTime timestamp, int x1, int x2, int y)
		{
			using (var gg = new LJD.Graphics(g))
				drawing?.DrawDragArea(gg, viewModel.OnDrawDragArea(timestamp), x1, x2, y);
		}

		void DrawFocusRect(Graphics g, DrawInfo di)
		{
			if (Focused && di.FocusRectIsRequired)
				ControlPaint.DrawFocusRectangle(g, this.ClientRectangle);
		}

		static PresentationMetrics ToPresentationMetrics(Metrics m)
		{
			return m.ToPresentationMetrics();
		}

		void StopDragging(bool accept)
		{
			if (dragPoint.HasValue)
			{
				DateTime? date = null;
				bool isFromTopDragArea = false;
				if (accept && dragForm != null && dragForm.Visible)
				{
					if (dragForm.Area == ViewArea.TopDrag)
					{
						date = dragForm.Date;
						isFromTopDragArea = true;
					}
					else
					{
						date = dragForm.Date;
						isFromTopDragArea = false;
					}
				}
				dragPoint = new Point?();
				viewModel.OnEndTimeRangeDrag(date, isFromTopDragArea);
			}
			if (dragForm != null && dragForm.Visible)
			{
				dragForm.Visible = false;
			}
		}

		Metrics GetMetrics()
		{
			return new Metrics(this.ClientRectangle, datesSize.Value, StaticMetrics.DragAreaHeight, 
				minMarkHeight, containersHeaderAreaHeight, containerControlSize);
		}

		void HideToolTip()
		{
			if (!toolTipVisible)
				return;
			toolTip.Hide(this);
			toolTipVisible = false;
			lastToolTipPoint = null;
		}

		void ShowToolTip()
		{
			if (toolTipVisible)
				return;
			if (this.contextMenu.Visible)
				return;
			Point pt = Cursor.Position;
			Point clientPt = PointToClient(pt);
			if (!ClientRectangle.Contains(clientPt))
				return;
			var tooltip = viewModel.OnTooltip(clientPt.X, clientPt.Y);
			if (tooltip == null)
				return;
			lastToolTipPoint = clientPt;
			Cursor cursor = this.Cursor;
			if (cursor != null)
			{
				pt.Y += cursor.Size.Height - cursor.HotSpot.Y;
			}
			toolTip.Show(tooltip, this, PointToClient(pt));
			toolTipVisible = true;
		}

		void OnResetToolTip()
		{
			HideToolTip();
			toolTipTimer.Stop();
			toolTipTimer.Start();
		}

		#endregion
	}
}
