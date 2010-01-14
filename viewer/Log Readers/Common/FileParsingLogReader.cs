using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Xml;

namespace LogJoint
{
	internal static class EncodingUtils
	{
		public static Encoding GetEncodingFromConfigXMLName(string encoding)
		{
			if (encoding == null)
				encoding = "";
			switch (encoding)
			{
				case "ACP": // use current ANSI code page
					return Encoding.Default;
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

	internal abstract class FileParsingLogReader : RangeManagingReader, IPositionedMessagesProvider
	{
		readonly string fileName;
		readonly Regex headerRe;

		TextFileStream stream;
		Encoding cachedEncoding;

		long bytesEndPosition;
		MessageBase firstMessage;

		public FileParsingLogReader(
			ILogReaderHost host, 
			ILogReaderFactory factory,
			string fileName,
			Regex headerRe
		):
			base (host, factory)
		{
			this.fileName = fileName;
			this.headerRe = headerRe;
			this.stats.ConnectionParams["path"] = fileName;
		}

		public string FileName
		{
			get { return fileName; }
		}

		public struct TextStreamPosition
		{
			public const int TextBufferSize = 16 * 1024;
			const long TextPosMask = TextBufferSize - 1;
			const long StreamPosMask = unchecked((long)(0xffffffffffffffff - TextPosMask));

			[DebuggerStepThrough]
			public TextStreamPosition(long streamPositionAlignedToBufferSize, int textPositionInsideBuffer)
			{
				if ((streamPositionAlignedToBufferSize & TextPosMask) != 0)
					throw new ArgumentException("Stream position must be aligned to buffer boundaries");

				data = streamPositionAlignedToBufferSize + textPositionInsideBuffer;
			}

			[DebuggerStepThrough]
			public TextStreamPosition(long positionValue)
			{
				data = positionValue;
			}
			public long StreamPositionAlignedToBufferSize
			{
				[DebuggerStepThrough]
				get { return data & StreamPosMask; }
			}
			public int CharPositionInsideBuffer
			{
				[DebuggerStepThrough]
				get { return unchecked((int)(data & TextPosMask)); }
			}
			public long Value
			{
				[DebuggerStepThrough]
				get { return data; } 
			}

			long data;
		};

		TextFileStream FStream
		{
			get
			{
				if (stream == null)
				{
					stream = new TextFileStream(this);
				}
				return stream;
			}
		}

		public long BeginPosition
		{
			get 
			{ 
				// If we know the first message in the stream, return its position.
				//if (firstMessage.Message != null)
				//	return firstMessage.Position;

				// Return phisical stream begin otherwise.
				// We get here when this provider is just created or
				// when we are updating the boundary messages 
				// cache (see UpdateAvailableBounds()).
				return 0;
			}
		}

		public long EndPosition
		{
			get 
			{
				// If we know the last message in the stream, return its position + 1.
				// +1 is needed to implement "pass-the-end" semantics:the position of the message 
				// following the last known one.
				//
				// We can't return lastMessage.Position because that would be the position
				// of the last message. Actually we may return lastMessage.Position but that
				// will result in the situaction that the "lastMessage" is not visible 
				// to the user. It is not a big problem for live logs but is a problem
				// for static ones.
				//
				// We can't return the length of the stream (bytesEndPosition) because there 
				// might be this situaltion:
				// ... ---------------------|------------------------|------|
				//                          |                        |      |
				// lastMessage.Position ____|                        |      |
				// End of the last known message ____________________|      |
				// Begin of partianlly written message ______________|      |
				// bytesEndPosition ________________________________________|

				// We can't say that 
				//
				// In case of live logs we don't know the exact position of the following message. 
				// The end of the "lastMessage" may change when the stream has grown in the future.
				// Note: there is no such flag as "this is a live log". Every log may become live
				// at any time.
				//
				// "lastMessage.Position + 1" is optimal.

				return bytesEndPosition;
			}
		}

		public long ActiveRangeRadius
		{
			get { return 1024 * 512; }
		}


		bool UpdatEndPosition()
		{
			long tmp = FStream.Length;
			if (tmp == bytesEndPosition)
				return false;
			bytesEndPosition = tmp;
			stats.TotalBytes = tmp;
			AcceptStats(StatsFlag.BytesCount);
			return true;
		}

		void UpdateBoundaryMessages()
		{
		}

		public IPositionedMessagesParser CreateParser(long position, FileRange.Range? range, bool isMainStreamReader)
		{
			return CreateReader(FStream, range, position, isMainStreamReader);
		}

		static DateRange GetAvailableDateRangeHelper(MessageBase first, MessageBase last)
		{
			return DateRange.MakeFromBoundaryValues(
				first != null ? first.Time : DateTime.MinValue,
				last != null ? last.Time : DateTime.MinValue
			);
		}

		public bool UpdateAvailableBounds(bool incrementalMode)
		{
			// Save the current phisical stream end
			long prevEndPosition = bytesEndPosition;

			// Reread the phisical stream end
			if (!UpdatEndPosition())
			{
				// The stream has the same size as it had before
				return false;
			}

			if (bytesEndPosition < prevEndPosition)
			{
				// The size of source file has reduced. This means that the 
				// file was probably overwritten. We have to delete all the messages 
				// we have loaded so far and start loading the file from the beginning.
				// Otherwise there is a high posiblity of messages' integrity violation.
				// Fall to non-incremental mode
				incrementalMode = false;
			}

			// Get new boundary values into temorary variables
			MessageBase newFirst, newLast;
			PositionedMessagesUtils.GetBoundaryMessages(this, null, out newFirst, out newLast);

			if (firstMessage != null && newFirst.Time != firstMessage.Time)
			{
				// The first message we've just read differs from the cached one. 
				// This means that the log was overwritten. Fall to non-incremental mode.
				incrementalMode = false;
			}

			if (!incrementalMode)
			{
				// Reset everythinh that have been loaded so far
				InvalidateEverythingThatHasBeenLoaded();
				firstMessage = null;
			}

			// Try to get the dates range for new bounday messages
			DateRange newAvailTime = GetAvailableDateRangeHelper(newFirst, newLast);
			firstMessage = newFirst;

			// Getting here means that the boundaries changed. 
			// Fire the notfication.
			stats.AvailableTime = newAvailTime;
			StatsFlag f = StatsFlag.AvailableTime;
			if (incrementalMode)
				f |= StatsFlag.AvailableTimeUpdatedIncrementallyFlag;
			AcceptStats(f);

			return true;
		}

		public long PositionRangeToBytes(FileRange.Range range)
		{
			// Here is not precise calculation: TextStreamPosition cannot be converted to bytes 
			// directly. But this function is used only for statistics, so it's OK to 
			// treat differece between TextStreamPosition's as bytes range.
			return range.Length;
		}

		public override void Dispose()
		{
			base.Dispose();
			if (stream != null)
			{
				stream.Dispose();
			}
		}

		protected override IPositionedMessagesProvider GetProvider()
		{
			return this;
		}

		protected abstract class Parser : IPositionedMessagesParser
		{
			readonly FileRange.Range? range;
			readonly bool isMainStreamParser;
			readonly FileParsingLogReader reader;
			TextFileStream fso;

			public Parser(FileParsingLogReader reader, TextFileStream fso, FileRange.Range? range, long startPosition, bool isMainStreamParser)
			{
				this.reader = reader;
				this.range = range;
				this.fso = fso;
				this.isMainStreamParser = isMainStreamParser;
				fso.AttachParser(this, startPosition);

				if (isMainStreamParser && fso.CurrentMessageIsEmpty && (reader.EndPosition - reader.BeginPosition) >= TextFileStream.MaximumMessageSize)
					throw new Exception("Unable to parse the stream. The data seems to have incorrect format.");
			}

			public bool IsMainStreamParser
			{
				get { return isMainStreamParser; }
			}

			protected TextFileStream Stream
			{
				get { return fso; }
			}

			public FileRange.Range? Range
			{
				get { return range; }
			}

			public abstract MessageBase ReadNext();

			public virtual void Dispose()
			{
				if (fso != null)
				{
					fso.DetachParser();
					fso = null;
				}
			}
		};

		protected abstract Parser CreateReader(TextFileStream stream, FileRange.Range? range, long startPosition, bool isMainStreamReader);
		protected abstract Encoding GetStreamEncoding(TextFileStream stream);

		protected class TextFileStream : FileStream
		{
			const int ParserBufferSize = TextStreamPosition.TextBufferSize;
			public const long MaximumMessageSize = 1024 * 2;

			readonly FileParsingLogReader reader;
			readonly Regex headerRe;
			readonly Encoding encoding;
			readonly StringBuilder buf = new StringBuilder(ParserBufferSize + 16);
			readonly byte[] binBuf;
			readonly char[] charBuf;
			readonly DateTime logFileLastModified;
			readonly Decoder decoder;
			readonly int maxBytesPerChar;

			Parser currentParser;
			FileRange.Range? currentRange;

			string bufString = "";
			int headerEnd;
			int headerStart;
			int prevHeaderEnd;
			int bufferOrigin;
			long streamPos;
			Match currMessageStart;

			public TextFileStream(FileParsingLogReader reader)
				: base(reader.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)
			{
				this.headerRe = reader.headerRe;
				this.logFileLastModified = File.GetLastWriteTime(reader.FileName);

				if (reader.cachedEncoding == null)
					reader.cachedEncoding = reader.GetStreamEncoding(this);
				this.encoding = reader.cachedEncoding;

				this.reader = reader;

				this.decoder = this.encoding.GetDecoder();
				this.maxBytesPerChar = this.encoding.GetMaxByteCount(1);

				binBuf = new byte[ParserBufferSize + maxBytesPerChar];
				charBuf = new char[binBuf.Length];

				SetTextPositionInternal(0);
			}

			internal void AttachParser(Parser parser, long startPosition)
			{
				if (parser == null)
					throw new ArgumentNullException("parser");
				if (currentParser != null)
					throw new InvalidOperationException("Cannot create more than one parser for a single stream");

				if (parser.Range.HasValue)
				{
					FileRange.Range r = parser.Range.Value;
					if (r.End > reader.EndPosition
					 || r.Begin < reader.BeginPosition)
					{
						throw new ArgumentOutOfRangeException(
							string.Format("Value passed is out of available stream range. value={0}. avaialble={1}",
								r, new FileRange.Range(reader.BeginPosition, reader.EndPosition)));
					}
				}

				currentParser = parser;
				currentRange = parser.Range;

				SetTextPositionInternal(startPosition);
			}

			internal void DetachParser()
			{
				if (currentParser == null)
					throw new InvalidOperationException("No parser is attached to the steam. Nothing to detach");
				currentParser = null;
				currentRange = null;
			}

			int MoveBuffer()
			{
				long stmPos = this.Position;

				bufferOrigin = bufString.Length - headerEnd;

				int bytesRead = this.Read(binBuf, 0, ParserBufferSize);
				int charsDecoded = decoder.GetChars(binBuf, 0, bytesRead, charBuf, 0);

				if (charsDecoded == 0)
					return 0;

				streamPos = stmPos;

				int ret = headerEnd;
				buf.Remove(0, headerEnd);
				buf.Append(charBuf, 0, charsDecoded);
				bufString = buf.ToString();

				headerEnd -= ret;
				headerStart -= ret;
				prevHeaderEnd -= ret;

				return ret;
			}

			void SetTextPositionInternal(long value)
			{
				if (value < 0 || value > reader.EndPosition)
					throw new ArgumentOutOfRangeException("value", "Position is out of range BeginPosition-EndPosition");

				TextStreamPosition beginPosObj = new TextStreamPosition(value);
				long posAlignedToBufferSize = beginPosObj.StreamPositionAlignedToBufferSize;

				if (posAlignedToBufferSize != 0)
				{
					this.Position = posAlignedToBufferSize - maxBytesPerChar;
					this.Read(binBuf, 0, maxBytesPerChar);
					decoder.GetChars(binBuf, 0, maxBytesPerChar, charBuf, 0);
				}
				else
				{
					this.Position = posAlignedToBufferSize;
				}

				headerEnd = 0;

				buf.Length = 0;
				bufString = "";

				MoveBuffer();

				headerEnd = Math.Min(beginPosObj.CharPositionInsideBuffer, bufString.Length);

				FindNextMessageStart();
			}

			Match FindNextMessageStart()
			{
				/*				// Protection againts header regexps that can match empty strings.
								// Normally, FindNextMessageStart() returns null when it has reached the end of the stream
								// because the regex can't find the next line. The problem is that regex can be composed so
								// that is can match empty strings. In that case without this check we would never 
								// stop parsing the stream producing more and more empty messages.
								if (???)
								{
									currMessageStart = null;
									return null;
								}*/

				Match m = headerRe.Match(bufString, headerEnd);
				if (!m.Success)
				{
					if (MoveBuffer() != 0)
					{
						m = headerRe.Match(bufString, headerEnd);
					}
				}
				if (m.Success)
				{
					prevHeaderEnd = headerEnd;

					headerStart = m.Index;
					headerEnd = m.Index + m.Length;

					currMessageStart = m;

				}
				else
				{
					currMessageStart = null;
				}
				return currMessageStart;
			}

			public override int Read(byte[] array, int offset, int count)
			{
				count = CheckEndPositionStopCondition(count);

				return base.Read(array, offset, count);
			}

			public override int ReadByte()
			{
				int count = CheckEndPositionStopCondition(1);

				int rv = -1;
				if (count == 1)
					rv = base.ReadByte();

				return rv;
			}

			int CheckEndPositionStopCondition(int count)
			{
				long end;
				if (currentRange.HasValue)
				{
					end = currentRange.Value.End;
				}
				else if (reader != null)
				{
					end = reader.bytesEndPosition;
				}
				else
				{
					return count;
				}
				if (Position + count > end)
				{
					count = (int)(end - Position);
					if (count < 0)
						count = 0;
				}

				return count;
			}

			TextStreamPosition CharIndexToStreamPosition(int idx)
			{
				return new TextStreamPosition(streamPos, idx - bufferOrigin);
			}

			public class TextMessageCapture
			{
				public readonly string HeadBuffer;
				public readonly Match HeaderMatch;
				public readonly TextStreamPosition BeginStreamPosition;

				public readonly string BodyBuffer;
				public readonly int BodyIndex;
				public readonly int BodyLength;
				public readonly TextStreamPosition EndStreamPosition;

				public readonly bool IsLastMessage;

				public TextMessageCapture(
					string headerBuffer, 
					Match headerMatch, 
					TextStreamPosition beginPos,
					string bodyBuffer, 
					int bodyIdx, 
					int bodyLen, 
					TextStreamPosition endPos, 
					bool isLastMessage)
				{
					HeadBuffer = headerBuffer;
					HeaderMatch = headerMatch;
					BeginStreamPosition = beginPos;

					BodyBuffer = bodyBuffer;
					BodyIndex = bodyIdx;
					BodyLength = bodyLen;

					EndStreamPosition = endPos;
					IsLastMessage = isLastMessage;
				}
			};

			public bool CurrentMessageIsEmpty
			{
				get { return currMessageStart == null; }
			}

			public TextMessageCapture GetCurrentMessageAndMoveToNextOne()
			{
				if (currMessageStart == null)
					return null;

				string headerBuffer = bufString;
				Match headerMatch = currMessageStart;
				TextStreamPosition beginPos = CharIndexToStreamPosition(headerStart);

				if (FindNextMessageStart() != null)
				{
					return new TextMessageCapture(
						headerBuffer, headerMatch, beginPos,
						bufString, prevHeaderEnd, headerStart - prevHeaderEnd, CharIndexToStreamPosition(headerStart), 
						false
					);
				}
				else
				{
					return new TextMessageCapture(
						headerBuffer, headerMatch, beginPos,
						bufString, headerEnd, bufString.Length - headerEnd,	CharIndexToStreamPosition(bufString.Length), 
						true
					);
				}
			}

			public DateTime LastModified
			{
				get { return logFileLastModified; }
			}

		}

	};
}
