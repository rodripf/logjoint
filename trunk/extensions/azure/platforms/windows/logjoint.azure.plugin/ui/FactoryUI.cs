using System;
using System.Linq;
using System.Windows.Forms;
using LogJoint.MRU;
using LogJoint.Azure;
using LogJoint.UI.Presenters.NewLogSourceDialog;

namespace LogJoint.UI.Azure
{
	public partial class FactoryUI : UserControl, IPagePresenter
	{
		Factory factory;
		bool updateLocked;
		LJTraceSource trace;
		ILogSourcesManager logSources;
		IRecentlyUsedEntities recentlyUsedLogs;
		Presenters.LabeledStepperPresenter.IPresenter recentPeriodCounterPresenter;

		public FactoryUI(Factory factory, ILogSourcesManager logSources, IRecentlyUsedEntities recentlyUsedLogs, ITraceSourceFactory traceSourceFactory)
		{
			this.trace = traceSourceFactory.CreateTraceSource("UI");
			this.factory = factory;
			this.logSources = logSources;
			this.recentlyUsedLogs = recentlyUsedLogs;
			InitializeComponent();

			recentPeriodCounterPresenter = new Presenters.LabeledStepperPresenter.Presenter(recentPeriodCounter);
			this.recentPeriodCounterPresenter.MaxValue = 2147483647;
			this.recentPeriodCounterPresenter.MinValue = 1;
			this.recentPeriodCounterPresenter.Value = 1;
			this.recentPeriodCounterPresenter.AllowedValues = new int[0];

			var mostRecentConnectionParams = FindMostRecentConnectionParams();
			if (mostRecentConnectionParams != null)
			{
				PrefillAccountFields(mostRecentConnectionParams);
			}
			
			SetInitialDatesRange();
			UpdateControls();
		}


		private void SetInitialDatesRange()
		{
			var now = DateTime.UtcNow;
			tillDateTimePicker.Value = now;
			fromDateTimePicker.Value = now.AddHours(-1);
			recentPeriodUnitComboBox.SelectedIndex = 1;
		}

		object IPagePresenter.View
		{
			get { return this; }
		}

		void IPagePresenter.Apply()
		{
			StorageAccount account = CreateStorageAccount();

			IConnectionParams connectParams = null;
			if (loadFixedRangeRadioButton.Checked)
				connectParams = factory.CreateParams(account, fromDateTimePicker.Value, tillDateTimePicker.Value);
			else if (loadRecentRadioButton.Checked)
				connectParams = factory.CreateParams(account, GetRecentPeriod(), liveLogCheckBox.Checked);
			else
				return;

			logSources.Create(factory, connectParams);
		}

		void IPagePresenter.Activate()
		{
		}

		void IPagePresenter.Deactivate()
		{
		}


		StorageAccount CreateStorageAccount()
		{
			if (devAccountRadioButton.Checked)
				return new StorageAccount();
			else
				return new StorageAccount(accountNameTextBox.Text, accountKeyTextBox.Text, useHTTPSCheckBox.Checked);
		}

		private void devAccountRadioButton_CheckedChanged(object sender, EventArgs e)
		{
			UpdateControls();
		}

		void UpdateControls()
		{
			accountNameTextBox.Enabled = cloudAccountRadioButton.Checked;
			accountKeyTextBox.Enabled = cloudAccountRadioButton.Checked;
			useHTTPSCheckBox.Enabled = cloudAccountRadioButton.Checked;
			fromDateTimePicker.Enabled = loadFixedRangeRadioButton.Checked;
			tillDateTimePicker.Enabled = loadFixedRangeRadioButton.Checked;
			recentPeriodCounter.Enabled = loadRecentRadioButton.Checked;
			recentPeriodUnitComboBox.Enabled = loadRecentRadioButton.Checked;
			liveLogCheckBox.Enabled = false;// loadRecentRadioButton.Checked; TODO: implement auto updates
		}

		private void fromDateTimePicker_ValueChanged(object sender, EventArgs e)
		{
			if (!updateLocked)
				using (LockControlUpdates())
					tillDateTimePicker.MinDate = fromDateTimePicker.Value;
		}

		private void tillDateTimePicker_ValueChanged(object sender, EventArgs e)
		{
			if (!updateLocked)
				using (LockControlUpdates())
					fromDateTimePicker.MaxDate = tillDateTimePicker.Value;
		}

		IDisposable LockControlUpdates()
		{
			return new ScopedGuard(() => { updateLocked = true; }, () => { updateLocked = false; });
		}

		private void loadFixedRangeRadioButton_CheckedChanged(object sender, EventArgs e)
		{
			if (!updateLocked)
			{
				using (LockControlUpdates())
					loadRecentRadioButton.Checked = false;
				UpdateControls();
			}
		}

		private void loadRecentRadioButton_CheckedChanged(object sender, EventArgs e)
		{
			if (!updateLocked)
			{
				using (LockControlUpdates())
					loadFixedRangeRadioButton.Checked = false;
				UpdateControls();
			}
		}

		TimeSpan GetRecentPeriod()
		{
			var counter = recentPeriodCounterPresenter.Value;
			TimeSpan[] units = new TimeSpan[]
			{
				TimeSpan.FromMinutes(1),
				TimeSpan.FromHours(1),
				TimeSpan.FromDays(1),
				TimeSpan.FromDays(7),
				TimeSpan.FromDays(30),
				TimeSpan.FromDays(365)
			};
			var unit = units[recentPeriodUnitComboBox.SelectedIndex];
			return new TimeSpan(-counter * unit.Ticks);
		}

		private void testConnectionButton_Click(object sender, EventArgs e)
		{
			Cursor = Cursors.WaitCursor;
			try
			{
				factory.TestAccount(CreateStorageAccount());
				MessageBox.Show("Your account is OK");
			}
			catch (Exception ex)
			{
				trace.Error(ex, "Storage account test failed");
				MessageBox.Show(string.Format("Failed to connect to storage account:\n{0}", ex.Message), "Testing your account", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
			finally
			{
				Cursor = Cursors.Default;
			}
		}

		AzureConnectionParams FindMostRecentConnectionParams()
		{
			var serializedParams =
				recentlyUsedLogs.GetMRUList().OfType<RecentLogEntry>().Where(e => e.Factory == factory).Select(e => e.ConnectionParams).FirstOrDefault();
			if (serializedParams == null)
				return null;
			try
			{
				return new AzureConnectionParams(serializedParams);
			}
			catch (InvalidConnectionParamsException)
			{
				return null;
			}
		}

		void PrefillAccountFields(AzureConnectionParams cp)
		{
			if (cp.Account.AccountType == StorageAccount.Type.CloudAccount)
			{
				devAccountRadioButton.Checked = false;
				cloudAccountRadioButton.Checked = true;
				accountNameTextBox.Text = cp.Account.AccountName;
				accountKeyTextBox.Text = cp.Account.AccountKey;
				useHTTPSCheckBox.Checked = cp.Account.UseHTPPS;
			}
			else
			{
				devAccountRadioButton.Checked = true;
				cloudAccountRadioButton.Checked = false;
			}
		}
	}
}
