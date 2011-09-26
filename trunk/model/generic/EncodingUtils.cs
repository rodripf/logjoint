﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;

namespace LogJoint
{
	internal static class EncodingUtils
	{
		public static Encoding GetDefaultEncoding()
		{
#if SILVERLIGHT
			return Encoding.UTF8;
#else
			return Encoding.Default;
#endif
		}

		public static Encoding GetEncodingFromConfigXMLName(string encoding)
		{
			if (encoding == null)
				encoding = "";
			switch (encoding)
			{
				case "ACP": // use current ANSI code page
					return GetDefaultEncoding();
				case "":
					return null;
				case "BOM": // detect from byte-order-mask
				case "PI": // detect from processing instructions
					return null;
				default:
					try
					{
						return Encoding.GetEncoding(encoding);
					}
					catch (ArgumentException)
					{
						return null;
					}
			}
		}

		public static Encoding DetectEncodingFromBOM(Stream stream, Encoding defaultEncoding)
		{
			stream.Position = 0;
			StreamReader tmpReader = new StreamReader(stream, defaultEncoding, true);
			tmpReader.Read();
			return tmpReader.CurrentEncoding ?? defaultEncoding;
		}

		public static Encoding DetectEncodingFromProcessingInstructions(Stream stream)
		{
			stream.Position = 0;
			XmlReaderSettings rs = new XmlReaderSettings();
			rs.CloseInput = false;
			rs.ConformanceLevel = ConformanceLevel.Fragment;

			using (XmlReader tmpReader = XmlReader.Create(stream, rs))
				try
				{
					while (tmpReader.Read())
					{
						if (tmpReader.NodeType == XmlNodeType.XmlDeclaration)
						{
							string encoding = tmpReader.GetAttribute("encoding");
							if (!string.IsNullOrEmpty(encoding))
							{
								try
								{
									return Encoding.GetEncoding(encoding);
								}
								catch (ArgumentException)
								{
									return null;
								}
							}
							return null;
						}
						else if (tmpReader.NodeType == XmlNodeType.Element)
						{
							break;
						}
					}
				}
				catch (XmlException)
				{
					return null; // XML might be not well formed. Ignore such errors.
				}
			return null;
		}
	};

}
