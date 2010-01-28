using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.Text.RegularExpressions;

namespace LogJoint.UI
{
	public partial class RegexBasedFormatPage : UserControl, IProvideSampleLog
	{
		XmlNode formatRoot;
		XmlNode reGrammarRoot;
		bool testOk;
		static readonly string[] parameterStatusStrings = new string[] { "Not set", "OK" };
		static readonly string[] testStatusStrings = new string[] { "", "Passed" };
		string sampleLog = "";

		public RegexBasedFormatPage()
		{
			InitializeComponent();
		}

		public void SetFormatRoot(XmlNode formatRoot)
		{
			this.formatRoot = formatRoot;
			this.reGrammarRoot = formatRoot.SelectSingleNode("regular-grammar");
			if (this.reGrammarRoot == null)
				this.reGrammarRoot = formatRoot.AppendChild(formatRoot.OwnerDocument.CreateElement("regular-grammar"));
			UpdateView();
		}

		void InitStatusLabel(Label label, bool statusOk, string[] strings)
		{
			label.Text = statusOk ? strings[1] : strings[0];
			label.ForeColor = statusOk ? Color.Green : Color.Black;
		}

		public void UpdateView()
		{
			InitStatusLabel(headerReStatusLabel, reGrammarRoot.SelectSingleNode("head-re[text()!='']") != null, parameterStatusStrings);
			InitStatusLabel(bodyReStatusLabel, reGrammarRoot.SelectSingleNode("body-re[text()!='']") != null, parameterStatusStrings);
			InitStatusLabel(fieldsMappingLabel, reGrammarRoot.SelectSingleNode("fields-config[field[@name='Time']]") != null, parameterStatusStrings);
			InitStatusLabel(testStatusLabel, testOk, testStatusStrings);
			InitStatusLabel(sampleLogStatusLabel, sampleLog != "", parameterStatusStrings);
		}

		private void selectSampleButton_Click(object sender, EventArgs e)
		{
			using (EditSampleLogForm f = new EditSampleLogForm(this))
				f.ShowDialog();
			UpdateView();
		}

		private void testButton_Click(object sender, EventArgs e)
		{
			if (sampleLog == "")
			{
				MessageBox.Show("Provide a sample log first", "", MessageBoxButtons.OK,
					MessageBoxIcon.Warning);
				return;
			}

			string tmpLog = TempFilesManager.GetInstance(Source.EmptyTracer).GenerateNewName();
			try
			{
				using (StreamWriter w = new StreamWriter(tmpLog, false, Encoding.ASCII))
					w.Write(sampleLog);

				ConnectionParams cp = new ConnectionParams();
				cp["path"] = tmpLog;

				using (RegularGrammar.UserDefinedFormatFactory f = new RegularGrammar.UserDefinedFormatFactory(
					null, formatRoot, reGrammarRoot))
				{
					testOk = TestParserForm.Execute(f, cp);
				}

				UpdateView();
			}
			finally
			{
				File.Delete(tmpLog);
			}
		}

		private void changeHeaderReButton_Click(object sender, EventArgs e)
		{
			using (EditRegexForm f = new EditRegexForm(reGrammarRoot, true, this))
			{
				if (f.ShowDialog() != DialogResult.OK)
					return;
				UpdateView();
			}
		}

		private void changeBodyReButon_Click(object sender, EventArgs e)
		{
			using (EditRegexForm f = new EditRegexForm(reGrammarRoot, false, this))
			{
				if (f.ShowDialog() != DialogResult.OK)
					return;
				UpdateView();
			}
		}

		IEnumerable<string> GetRegExCaptures(string reId)
		{
			XmlNode reNore = reGrammarRoot.SelectSingleNode(reId);
			if (reNore == null)
				yield break;
			Regex re;
			try
			{
				re = new Regex(reNore.InnerText, RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);
			}
			catch
			{
				yield break;
			}

			int i = 0;
			foreach (string n in re.GetGroupNames())
				if (i++ > 0)
					yield return n;
		}

		private void changeFieldsMappingButton_Click(object sender, EventArgs e)
		{
			List<string> allCaptures = new List<string>();
			allCaptures.AddRange(GetRegExCaptures("head-re"));
			allCaptures.AddRange(GetRegExCaptures("body-re"));
			using (FieldsMappingForm f = new FieldsMappingForm(reGrammarRoot, allCaptures.ToArray()))
			{
				f.ShowDialog();
				UpdateView();
			}
		}

		#region IProvideSampleLog Members

		public string SampleLog
		{
			get
			{
				return sampleLog;
			}
			set
			{
				sampleLog = value;
			}
		}

		#endregion
	}

	public interface IProvideSampleLog
	{
		string SampleLog { get; set; }
	};
}
