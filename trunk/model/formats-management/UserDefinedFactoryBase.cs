﻿using LogJoint.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace LogJoint
{
	public struct LoadedRegex
	{
		public IRegex Regex;
		public bool SuffersFromPartialMatchProblem;

		public LoadedRegex Clone(ReOptions optionsToAdd = ReOptions.None)
		{
			LoadedRegex ret;
			ret.Regex = RegularExpressions.RegexUtils.CloneRegex(this.Regex, optionsToAdd);
			ret.SuffersFromPartialMatchProblem = this.SuffersFromPartialMatchProblem;
			return ret;
		}

		public MessagesSplitterFlags GetHeaderReSplitterFlags()
		{
			var headerRe = this;
			MessagesSplitterFlags ret = MessagesSplitterFlags.None;
			if (headerRe.SuffersFromPartialMatchProblem)
				ret |= MessagesSplitterFlags.PreventBufferUnderflow;
			return ret;
		}
	};

	public abstract class UserDefinedFactoryBase : IUserDefinedFactory, ILogProviderFactory, IDisposable
	{
		string IUserDefinedFactory.Location { get { return location; } }
		bool IUserDefinedFactory.IsDisposed { get { return disposed; } }

		string ILogProviderFactory.CompanyName { get { return companyName; } }
		string ILogProviderFactory.FormatName { get { return formatName; } }
		string ILogProviderFactory.FormatDescription { get { return description; } }
		IFormatViewOptions ILogProviderFactory.ViewOptions { get { return viewOptions; } }
		string ILogProviderFactory.GetConnectionId(IConnectionParams connectParams) { return ConnectionParamsUtils.GetConnectionIdentity(connectParams); }

		public abstract string UITypeKey { get; }
		public abstract string GetUserFriendlyConnectionName(IConnectionParams connectParams);
		public abstract IConnectionParams GetConnectionParamsToBeStoredInMRUList(IConnectionParams originalConnectionParams);
		public abstract ILogProvider CreateFromConnectionParams(ILogProviderHost host, IConnectionParams connectParams);
		public abstract LogProviderFactoryFlag Flags { get; }

		public UserDefinedFactoryBase(UserDefinedFactoryParams createParams)
		{
			if (createParams.FormatSpecificNode == null)
				throw new ArgumentNullException("createParams.FormatSpecificNode");
			if (createParams.RootNode == null)
				throw new ArgumentNullException("createParams.RootNode");

			this.location = createParams.Location;
			this.factoryRegistry = createParams.FactoryRegistry;
			this.regexFactory = createParams.RegexFactory;

			var idData = createParams.RootNode.Elements("id").Select(
				id => new { company = id.AttributeValue("company"), formatName = id.AttributeValue("name") }).FirstOrDefault();

			if (idData != null)
			{
				companyName = idData.company;
				formatName = idData.formatName;
			}

			description = ReadParameter(createParams.RootNode, "description").Trim();

			viewOptions = new FormatViewOptions(createParams.RootNode.Element("view-options"));

			if (factoryRegistry != null)
				factoryRegistry.Register(this);
		}

		public override string ToString()
		{
			return LogProviderFactoryRegistry.ToString(this);
		}

		void IDisposable.Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			if (factoryRegistry != null)
				factoryRegistry.Unregister(this);
		}

		protected static string ReadParameter(XElement root, string name)
		{
			return root.Elements(name).Select(a => a.Value).FirstOrDefault() ?? "";
		}

		protected LoadedRegex ReadRe(XElement root, string name, ReOptions opts)
		{
			LoadedRegex ret = new LoadedRegex();
			var n = root.Element(name);
			if (n == null)
				return ret;
			string pattern = n.Value;
			if (string.IsNullOrEmpty(pattern))
				return ret;
			ret.Regex = regexFactory.Create(pattern, opts);
			XAttribute attr;
			ret.SuffersFromPartialMatchProblem =
				(attr = n.Attribute("suffers-from-partial-match-problem")) != null
				&& attr.Value == "yes";
			return ret;
		}

		protected static Type ReadType(XElement root, string name, Type defType)
		{
			string typeName = ReadParameter(root, name);
			if (string.IsNullOrEmpty(typeName))
				return defType;
			return Type.GetType(typeName);
		}

		protected static Type ReadPrecompiledUserCode(XElement root)
		{
			var codeNode = root.Element("precompiled-user-code");
			if (codeNode == null)
				return null;
			var typeAttr = codeNode.Attribute("type");
			if (typeAttr == null)
				return null;
			Assembly asm;
			byte[] asmBytes = Convert.FromBase64String(codeNode.Value);
#if !SILVERLIGHT
			asm = Assembly.Load(asmBytes);
#else
				var asmPart = new System.Windows.AssemblyPart();
				asm = asmPart.Load(new MemoryStream(asmBytes));
#endif
			return asm.GetType(typeAttr.Value);
		}

		protected static void ReadPatterns(XElement formatSpecificNode, List<string> patternsList)
		{
			patternsList.AddRange(
				from patterns in formatSpecificNode.Elements("patterns")
				from pattern in patterns.Elements("pattern")
				let patternVal = pattern.Value
				where patternVal != ""
				select patternVal);
		}

		readonly string location;
		readonly string companyName;
		readonly string formatName;
		readonly string description = "";
		readonly ILogProviderFactoryRegistry factoryRegistry;
		readonly IRegexFactory regexFactory;
		protected readonly FormatViewOptions viewOptions;
		bool disposed;
	};
}
