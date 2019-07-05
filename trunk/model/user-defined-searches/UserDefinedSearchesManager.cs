using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.IO;

namespace LogJoint
{
	public class UserDefinedSearchesManager : IUserDefinedSearches, IUserDefinedSearchesInternal
	{
		readonly Dictionary<string, IUserDefinedSearch> items = 
			new Dictionary<string, IUserDefinedSearch>(StringComparer.CurrentCultureIgnoreCase);
		readonly Lazy<Persistence.IStorageEntry> storageEntry;
		readonly IFiltersFactory filtersFactory;
		readonly AsyncInvokeHelper changeHandlerInvoker;
		const string sectionName = "items";

		public UserDefinedSearchesManager(
			Persistence.IStorageManager storage,
			IFiltersFactory filtersFactory,
			ISynchronizationContext modelThreadSynchronization
		)
		{
			this.filtersFactory = filtersFactory;
			this.storageEntry = new Lazy<Persistence.IStorageEntry>(() => storage.GetEntry("UserDefinedSearches"));
			this.changeHandlerInvoker = new AsyncInvokeHelper(modelThreadSynchronization, HandleChange);

			LoadItems();
		}

		IEnumerable<IUserDefinedSearch> IUserDefinedSearches.Items
		{
			get { return items.Values; }
		}

		bool IUserDefinedSearches.ContainsItem(string name)
		{
			return items.ContainsKey(name);
		}

		public event EventHandler OnChanged;

		IUserDefinedSearch IUserDefinedSearches.AddNew()
		{
			string name;
			for (int num = 1;;++num)
			{
				name = string.Format("New filter {0}", num);
				if (!items.ContainsKey(name))
					break;
			}
			var search = new UserDefinedSearch(
				this, name,
				filtersFactory.CreateFiltersList(FilterAction.Exclude, FiltersListPurpose.Search)
			);
			items[name] = search;
			changeHandlerInvoker.Invoke();
			return search;
		}

		void IUserDefinedSearches.Delete(IUserDefinedSearch search)
		{
			((IUserDefinedSearchInternal)search).DetachFromOwner(this);
			items.Remove(search.Name);
			changeHandlerInvoker.Invoke();
		}

		void IUserDefinedSearches.Export(IUserDefinedSearch[] searches, Stream stm)
		{
			new XDocument(SaveItems(searches)).Save(stm);
		}

		void IUserDefinedSearches.Import(Stream stm, Func<string, NameDuplicateResolution> dupesResolver)
		{
			if (LoadItems(XDocument.Load(stm), dupesResolver) > 0)
			{
				changeHandlerInvoker.Invoke();
			}
		}

		void IUserDefinedSearchesInternal.OnNameChanged(IUserDefinedSearch sender, string oldName)
		{
			items.Remove(oldName);
			items.Add(sender.Name, sender);
			changeHandlerInvoker.Invoke();
		}

		void IUserDefinedSearchesInternal.OnFiltersChanged(IUserDefinedSearch sender)
		{
			changeHandlerInvoker.Invoke();
		}

		void LoadItems()
		{
			items.Clear();
			using (var section = storageEntry.Value.OpenXMLSection(
				sectionName, 
				Persistence.StorageSectionOpenFlag.ReadOnly))
			{
				LoadItems(section.Data, _ => NameDuplicateResolution.Skip);
			}
		}

		int LoadItems(XDocument doc, Func<string, NameDuplicateResolution> dupesResolver)
		{
			if (doc.Root == null)
				return 0;
			var itemNodes = new List<XElement>();
			foreach (var itemElt in doc.Root.Elements("item"))
			{
				var name = itemElt.AttributeValue("name");
				if (string.IsNullOrEmpty(name))
					continue;
				if (items.ContainsKey(name))
				{
					var resolution = dupesResolver(name);
					if (resolution == NameDuplicateResolution.Cancel)
						return 0;
					if (resolution == NameDuplicateResolution.Skip)
						continue;
				}
				itemNodes.Add(itemElt);
			}
			foreach (var itemElt in itemNodes)
			{
				var name = itemElt.AttributeValue("name");
				var search = new UserDefinedSearch(
					this, 
					name,
					filtersFactory.CreateFiltersList(itemElt, FiltersListPurpose.Search)
				);
				items[name] = search;
			}
			return itemNodes.Count;
		}

		void SaveItems ()
		{
			using (var section = storageEntry.Value.OpenXMLSection (
				sectionName,
				Persistence.StorageSectionOpenFlag.ReadWrite | Persistence.StorageSectionOpenFlag.ClearOnOpen | Persistence.StorageSectionOpenFlag.IgnoreStorageExceptions)) 
			{
				section.Data.Add (SaveItems (items.Values));
			}
		}

		private XElement SaveItems (IEnumerable<IUserDefinedSearch> searches)
		{
			return new XElement (
				"root",
				searches.Select (item => 
				{
					var itemElt = new XElement ("item", new XAttribute ("name", item.Name));
					item.Filters.Save (itemElt);
					return itemElt;
				})
			);
		}

		void HandleChange()
		{
			SaveItems ();
			OnChanged?.Invoke (this, EventArgs.Empty);
		}
	};
}
