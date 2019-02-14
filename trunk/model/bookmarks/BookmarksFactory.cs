using System;
using System.Collections.Generic;
using System.Text;
using LogJoint.RegularExpressions;
using System.Linq;
using System.Diagnostics;

namespace LogJoint
{
	public class BookmarksFactory : IBookmarksFactory
	{
		IBookmark IBookmarksFactory.CreateBookmark(MessageTimestamp time, IThread thread, string displayName, string messageText, long position, int lineIndex)
		{
			return new Bookmark(time, thread, displayName, messageText, position, lineIndex);
		}

		IBookmark IBookmarksFactory.CreateBookmark(MessageTimestamp time, string sourceConnectionId, long position, int lineIndex)
		{
			return new Bookmark(time, sourceConnectionId, position, lineIndex);
		}

		IBookmark IBookmarksFactory.CreateBookmark(IMessage message, int lineIndex, bool useRawText)
		{
			return new Bookmark(message, lineIndex, useRawText);
		}

		IBookmarks IBookmarksFactory.CreateBookmarks()
		{
			return new Bookmarks(this);
		}
	};

}
