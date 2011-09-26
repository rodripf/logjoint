using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace LogJoint.UI
{
	public partial class FormatAdditionalOptionsPage : UserControl, IWizardPage
	{
		XmlNode formatRoot;

		public FormatAdditionalOptionsPage()
		{
			InitializeComponent();
			UpdateView();
			InitEncodings();
		}

		void UpdateView()
		{
			addExtensionButton.Enabled = GetValidExtension() != null;
			removeExtensionButton.Enabled = patternsListBox.SelectedIndices.Count > 0;
		}

		public enum EntryType
		{
			UseBOM,
			UseACP,
			Encoding
		}

		class EncodingEntry: IComparable<EncodingEntry>
		{
			public readonly EntryType Type;
			public readonly EncodingInfo Info;
			public EncodingEntry(EntryType t, EncodingInfo ei)
			{
				Type = t;
				Info = ei;
			}
			public string ToXMLString()
			{
				switch (Type)
				{
					case EntryType.UseACP:
						return "ACP";
					case EntryType.UseBOM:
						return "BOM";
					case EntryType.Encoding:
						return Info.Name;
				}
				return "";
			}

			public override string ToString()
			{
				switch (Type)
				{
					case EntryType.UseBOM:
						return "(Determine the encoding with BOM)";
					case EntryType.UseACP:
						return "(Use system's current ANSI codepage)";
					case EntryType.Encoding:
						return Info.Name;
				}
				return "";
			}
			int GetSortOrder()
			{
				if (Type == EntryType.UseACP)
					return 0;
				if (Type == EntryType.UseBOM)
					return 1;
				if (Info.Name == "utf-8")
					return 10;
				if (Info.Name == "utf-16")
					return 11;
				return 0xff;
			}

			#region IComparable<EncodingEntry> Members

			public int CompareTo(EncodingEntry other)
			{
				int order1 = this.GetSortOrder();
				int order2 = other.GetSortOrder();
				if (order1 != order2)
					return order1 - order2;
				if (order1 == 0xff)
					return this.Info.Name.CompareTo(other.Info.Name);
				return 0;
			}

			#endregion
		};

		void InitEncodings()
		{
			List<EncodingEntry> entries = new List<EncodingEntry>();
			entries.Add(new EncodingEntry(EntryType.UseBOM, null));
			entries.Add(new EncodingEntry(EntryType.UseACP, null));
			foreach (EncodingInfo e in Encoding.GetEncodings())
				entries.Add(new EncodingEntry(EntryType.Encoding, e));
			entries.Sort();
			foreach (EncodingEntry e in entries)
				encodingComboBox.Items.Add(e);
		}

		string GetValidExtension()
		{
			string ext = extensionTextBox.Text.Trim();
			if (ext == "")
				return null;
			if (ext.IndexOfAny(new char[] { '\\', '/', ':', '"', '<', '>', '|' }) >= 0)
				return null;
			return ext;
		}

		private void extensionTextBox_TextChanged(object sender, EventArgs e)
		{
			UpdateView();
		}

		private void extensionsListBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			UpdateView();
		}

		private void addExtensionButton_Click(object sender, EventArgs e)
		{
			string ext = GetValidExtension();
			if (ext == null)
				return;
			for (int i = 0; i < patternsListBox.Items.Count; ++i)
			{
				if (string.Compare((string)patternsListBox.Items[i], ext, true) == 0)
					return;
			}
			patternsListBox.Items.Add(ext);
			extensionTextBox.Text = "";
			UpdateView();
		}

		private void removeExtensionButton_Click(object sender, EventArgs e)
		{
			List<int> idxs = new List<int>();
			foreach (int i in patternsListBox.SelectedIndices)
				idxs.Add(i);
			for (int i = idxs.Count - 1; i >= 0; --i)
				patternsListBox.Items.RemoveAt(i);
			UpdateView();
		}

		public IEnumerable<string> Patterns
		{
			get
			{
				foreach (string s in patternsListBox.Items)
					yield return s;
			}
		}

		public void SetFormatRoot(XmlNode formatRoot)
		{
			this.formatRoot = formatRoot;

			patternsListBox.Items.Clear();
			foreach (XmlNode e in formatRoot.SelectNodes("patterns/pattern[text()!='']"))
				patternsListBox.Items.Add(e.InnerText);

			XmlNode encodingNode = formatRoot.SelectSingleNode("encoding");
			string encoding = "";
			if (encodingNode != null)
				encoding = encodingNode.InnerText;
			encodingComboBox.SelectedItem = null;
			foreach (EncodingEntry ee in encodingComboBox.Items)
				if (ee.ToXMLString() == encoding)
					encodingComboBox.SelectedItem = ee;
		}

		public bool ExitPage(bool movingForward)
		{
			XmlNode patternsRoot = formatRoot.SelectSingleNode("patterns");
			if (patternsRoot == null)
				patternsRoot = formatRoot.AppendChild(formatRoot.OwnerDocument.CreateElement("patterns"));
			patternsRoot.RemoveAll();

			foreach (string e in Patterns)
				patternsRoot.AppendChild(patternsRoot.OwnerDocument.CreateElement("pattern")).InnerText = e;

			EncodingEntry ee = encodingComboBox.SelectedItem as EncodingEntry;
			string encodingXMLCode = (ee != null) ? ee.ToXMLString() : "ACP";
			XmlNode encodingNode = formatRoot.SelectSingleNode("encoding");
			if (encodingNode == null)
				encodingNode = formatRoot.AppendChild(formatRoot.OwnerDocument.CreateElement("encoding"));
			encodingNode.InnerText = encodingXMLCode;

			return true;
		}
	}
}
