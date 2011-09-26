using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace LogJoint.MSLogParser.EVT
{
	public partial class SelectLogSourceDialog : Form
	{
		public SelectLogSourceDialog()
		{
			InitializeComponent();
		}

		class Item
		{
			public string DisplayName;
			public string Name;
			public Item(System.Diagnostics.EventLog l)
			{
				DisplayName = l.LogDisplayName;
				Name = l.Log;
			}
			public override string ToString()
			{
				return DisplayName;
			}
		};

		void UpdateLogs()
		{
			EventLog[] logs;
			try
			{
				logs = EventLog.GetEventLogs(machineNameTextBox.Text);
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message, "Failed to connect", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			logsListBox.Items.Clear();
			foreach (EventLog l in logs)
			{
				Item item;
				try
				{
					item = new Item(l);
				}
				catch (System.Security.SecurityException)
				{
					// User may not have anought permissions to view certain hives. Ignore those cases
					// not showing these hives in the list.
					continue;
				}

				logsListBox.Items.Add(item);
			}
		}

		private void SelectLogSourceDialog_Load(object sender, EventArgs e)
		{
			UpdateLogs();
		}

		public new string[] ShowDialog()
		{
			if (base.ShowDialog() != DialogResult.OK)
				return null;
			List<string> ret = new List<string>();
			string machine = machineNameTextBox.Text;
			foreach (Item i in logsListBox.CheckedItems)
			{
				if (machine == ".")
					ret.Add(i.Name);
				else
					ret.Add(string.Format(@"\\{0}\{1}", machine, i.Name));
			}
			return ret.ToArray();
		}

		private void logsListBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			okButton.Enabled = logsListBox.CheckedItems.Count > 0;
		}

		private void connectButton_Click(object sender, EventArgs e)
		{
			UpdateLogs();
		}
	}
}