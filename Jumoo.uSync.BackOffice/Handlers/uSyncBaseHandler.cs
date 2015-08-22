﻿namespace Jumoo.uSync.BackOffice.Handlers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;

    using Jumoo.uSync.Core;

    using Jumoo.uSync.BackOffice.Helpers;

    using Umbraco.Core.Logging;
    using Umbraco.Core.Models.EntityBase;
    using System;

    abstract public class uSyncBaseHandler<T>
    {
        abstract public SyncAttempt<T> Import(string filePath, bool force = false);

        public IEnumerable<uSyncAction> ImportAll(string folder, bool force)
        {
            LogHelper.Info<Logging>("Running Import: {0}", () => Path.GetFileName(folder));
            List<uSyncAction> actions = new List<uSyncAction>();

            // for a non-force sync, we use the actions to process deletes.
            // when it's a force, then we delete anything that is in umbraco
            // that isn't in our folder??
            // if (!force)
            //{
            actions.AddRange(ProcessActions());
            //}

            actions.AddRange(ImportFolder(folder, force));

            LogHelper.Info<Logging>("Import Complete: {0} Items {1} changes {2} failures",
                () => actions.Count(),
                () => actions.Count(x => x.Change > ChangeType.NoChange),
                () => actions.Count(x => x.Change > ChangeType.Fail));

            return actions; 
        }

        private IEnumerable<uSyncAction> ImportFolder(string folder, bool force)
        {
            Dictionary<string, T> updates = new Dictionary<string, T>();

            List<uSyncAction> actions = new List<uSyncAction>();

            string mappedfolder = Umbraco.Core.IO.IOHelper.MapPath(folder);

            if (Directory.Exists(mappedfolder))
            {
                foreach (string file in Directory.GetFiles(mappedfolder, "*.config"))
                {
                    var attempt = Import(file, force);
                    if (attempt.Success && attempt.Item != null)
                    {
                        updates.Add(file, attempt.Item);
                    }

                    actions.Add(uSyncActionHelper<T>.SetAction(attempt, file));
                }

                foreach (var children in Directory.GetDirectories(mappedfolder))
                {
                    actions.AddRange(ImportFolder(children, force));
                }
            }

            if (updates.Any())
            {
                foreach (var update in updates)
                {
                    ImportSecondPass(update.Key, update.Value);
                }
            }

            return actions; 
        }

        private IEnumerable<uSyncAction> ProcessActions()
        {
            List<uSyncAction> syncActions = new List<uSyncAction>();

            var actions = ActionTracker.GetActions(typeof(T));

            if (actions != null && actions.Any())
            {
                foreach(var action in actions)
                {
                    LogHelper.Info<Logging>("Processing a Delete: {0}", () => action.TypeName);
                    switch (action.Action)
                    {
                        case SyncActionType.Delete:
                            syncActions.Add(DeleteItem(action.Key, action.Name));
                            break;
                    }
                }
            }

            return syncActions;
        }

        virtual public uSyncAction DeleteItem(Guid key, string keyString)
        {
            return new uSyncAction();
        }

        virtual public string GetItemPath(T item)
        {
            return ((IUmbracoEntity)item).Name;
        }

        /// <summary>
        ///  second pass placeholder, some things require a second pass
        ///  (doctypes for structures to be in place)
        /// 
        ///  they just override this function to do their thing.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="item"></param>
        virtual public void ImportSecondPass(string file, T item)
        {

        }

        /// <summary>
        ///  reutns a list of actions saying what will happen 
        /// on a import (force = false)
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public IEnumerable<uSyncAction> Report(string folder)
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            string mappedfolder = Umbraco.Core.IO.IOHelper.MapPath(folder);

            if (Directory.Exists(mappedfolder))
            {
                foreach (string file in Directory.GetFiles(mappedfolder, "*.config"))
                {
                    actions.Add(ReportItem(file));

                }

                foreach (var children in Directory.GetDirectories(mappedfolder))
                {
                    actions.AddRange(Report(children));
                }
            }

            return actions;
        }

        abstract public uSyncAction ReportItem(string file);
    }
}
