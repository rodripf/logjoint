﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using System.Xml;

namespace LogJoint.Extensibility
{
	public class PluginManifest : IPluginManifest
	{
		readonly string pluginDirectory;
		readonly string absolulePath;
		readonly string id;
		readonly Version version;
		readonly IReadOnlyList<IPluginFile> files;
		readonly IPluginFile entry;
		readonly IReadOnlyList<IPluginDependency> dependencies;

		public PluginManifest(string pluginDirectory)
		{
			this.pluginDirectory = pluginDirectory;
			this.absolulePath = Path.Combine(pluginDirectory, ManifestFileName);
			XDocument doc;
			try
			{
				doc = XDocument.Load(this.absolulePath);
			}
			catch (XmlException e)
			{
				throw new BadManifestException($"Failed to load manifest", e);
			}
			var root = doc.Element("manifest") ?? throw new BadManifestException($"Bad manifest root element");
			XElement getMandatory(string name) =>
				root.Element(name) ?? throw new BadManifestException($"Mandatory property {name} is missing");
			this.id = getMandatory("id").Value;
			if (string.IsNullOrWhiteSpace(this.id))
				throw new BadManifestException($"'{id}' is not a valid plugin id");
			var versionStr = getMandatory("version").Value;
			if (!Version.TryParse(versionStr, out this.version))
				throw new BadManifestException($"'{versionStr}' is not a valid plugin version");
			this.files = root.Elements("file").Select(fileNode =>
			{
				PluginFileType type;
				var typeStr = fileNode.Attribute("type")?.Value ?? "unspecified";
				switch (typeStr)
				{
					case "entry": type = PluginFileType.Entry; break;
					case "format": type = PluginFileType.FormatDefinition; break;
					case "dll": type = PluginFileType.Library; break;
					case "sdk": type = PluginFileType.SDK; break;
					case "nib": type = PluginFileType.Nib; break;
					case "unspecified": type = PluginFileType.Unspecified; break;
					default: throw new BadManifestException($"Bad file type {typeStr}");
				}
				return new File
				{
					manifest = this,
					type = type,
					relativePath = fileNode.Value // todo: normalize path separators
				};
			}).ToArray().AsReadOnly();
			this.entry = this.files.FirstOrDefault(f => f.Type == PluginFileType.Entry)
				?? throw new BadManifestException($"Plugin entry is missing from manifest");
			this.dependencies = root.Elements("dependency").Select(depNode =>
			{
				return new Dependency
				{
					manifest = this,
					pluginId = !string.IsNullOrWhiteSpace(depNode.Value) ? depNode.Value : throw new BadManifestException($"Bad dependency {depNode}")
				};
			}).ToArray().AsReadOnly();
		}

		public static string ManifestFileName => "manifest.xml";

		string IPluginManifest.PluginDirectory => pluginDirectory;

		string IPluginManifest.AbsolulePath => absolulePath;

		string IPluginManifest.Id => id;

		IReadOnlyList<IPluginFile> IPluginManifest.Files => files;

		IPluginFile IPluginManifest.Entry => entry;

		IReadOnlyList<IPluginDependency> IPluginManifest.Dependencies => dependencies;

		public override string ToString() => $"{id} {version}";

		class File : IPluginFile
		{
			public PluginManifest manifest;
			public PluginFileType type;
			public string relativePath;

			IPluginManifest IPluginFile.Manifest => manifest;

			PluginFileType IPluginFile.Type => type;

			string IPluginFile.RelativePath => relativePath;

			string IPluginFile.AbsolulePath => Path.Combine(manifest.pluginDirectory, relativePath);

			public override string ToString() => $"{type} {relativePath}";
		};

		class Dependency : IPluginDependency
		{
			public PluginManifest manifest;
			public string pluginId;

			IPluginManifest IPluginDependency.Manifest => manifest;

			string IPluginDependency.PluginId => pluginId;
		};
	}
}
