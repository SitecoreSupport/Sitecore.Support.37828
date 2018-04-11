using System;
using System.Linq;

using Sitecore.Data.Items;
using Sitecore.Events;
using Sitecore.Framework.Conditions;
using Sitecore.Publishing.Service.SitecoreAbstractions;
using Sitecore.SecurityModel;

namespace Sitecore.Support.Publishing.Service.Events
{
  public class ItemSavedHandler
  {
    private readonly IPublishingLog _logger;

    public ItemSavedHandler() : this(new PublishingLogWrapper())
    {
    }

    public ItemSavedHandler(IPublishingLog log)
    {
      Condition.Requires(log, "log").IsNotNull();

      _logger = log;
    }

    public void UpdateItemVariantRevisions(object sender, EventArgs args)
    {
      var sitecoreEventArgs = args as SitecoreEventArgs;

      if (sitecoreEventArgs == null || sitecoreEventArgs.Parameters == null || !sitecoreEventArgs.Parameters.Any())
      {
        return;
      }

      var savedItem = sitecoreEventArgs.Parameters[0] as Item;
      var savedItemChanges = sitecoreEventArgs.Parameters[1] as ItemChanges;

      if (savedItem == null || savedItemChanges == null)
      {
        return;
      }

      bool sharedFieldChanged = false;
      bool unversionedFieldChanged = false;

      if (savedItemChanges.HasFieldsChanged)
      {
        foreach (FieldChange fieldChange in savedItemChanges.FieldChanges)
        {
          if (fieldChange.IsShared || fieldChange.IsUnversioned)
          {
            if (fieldChange.IsShared)
            {
              sharedFieldChanged = true;

              break;
            }

            unversionedFieldChanged = true;
          }
        }
      }

      var updateVariantRevisions = sharedFieldChanged || unversionedFieldChanged;

      if (updateVariantRevisions)
      {
        var versionsToUpdate =
            savedItem.Versions.GetVersions(includeAllLanguages: sharedFieldChanged).Where(v => v.Version.Number != savedItem.Version.Number || v.Language != savedItem.Language).ToArray();

        _logger.Debug(string.Format("Starting to update the revisions for all versions of the item: {0}", savedItem.ID));

        using (new SecurityDisabler())
        {
          foreach (var itemVersion in versionsToUpdate)
          {
            _logger.Debug(string.Format("Updating the revision for all '{0}' versions of the item: {1}", itemVersion.Language, itemVersion.ID));

            itemVersion.Editing.BeginEdit();
            itemVersion.Fields[FieldIDs.Revision].SetValue(Guid.NewGuid().ToString(), true);
            itemVersion.Editing.EndEdit(false, false);
          }
        }

        _logger.Debug(string.Format("Completed updating the revisions for all versions of the item: {0}", savedItem.ID));
      }
    }
  }
}
