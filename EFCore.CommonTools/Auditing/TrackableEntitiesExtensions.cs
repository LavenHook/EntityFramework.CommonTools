﻿using System;
using System.Linq;

#if EF_CORE
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EntityFrameworkCore.CommonTools
#elif EF_6
using System.Data.Entity;
using EntityEntry = System.Data.Entity.Infrastructure.DbEntityEntry;

namespace EntityFramework.CommonTools
#endif
{
    public static partial class DbContextExtensions
    {
        /// <summary>
        /// Populate special properties for all Trackable Entities in context.
        /// </summary>
        public static void UpdateTrackableEntities(this DbContext context)
        {
            DateTime utcNow = DateTime.UtcNow;

            var changedEntries = context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added
                         || e.State == EntityState.Modified
                         || e.State == EntityState.Deleted);

            foreach (var dbEntry in changedEntries)
            {
                UpdateTrackableEntity(dbEntry, utcNow);
            }
        }

        private static void UpdateTrackableEntity(EntityEntry dbEntry, DateTime utcNow)
        {
            object entity = dbEntry.Entity;

            switch (dbEntry.State)
            {
                case EntityState.Added:
                    if (entity is ICreationTrackable creationTrackable)
                    {
                        creationTrackable.CreatedUtc = utcNow;
                    }
                    break;

                case EntityState.Modified:
                    if (entity is IModificationTrackable modificatonTrackable)
                    {
                        modificatonTrackable.UpdatedUtc = utcNow;
                        dbEntry.CurrentValues[nameof(IModificationTrackable.UpdatedUtc)] = utcNow;

                        if (entity is ICreationTrackable)
                        {
                            PreventPropertyOverwrite<DateTime>(dbEntry, nameof(ICreationTrackable.CreatedUtc));
                        }
                    }
                    break;

                case EntityState.Deleted:
                    if (entity is ISoftDeletable softDeletable)
                    {
                        dbEntry.State = EntityState.Unchanged;
                        softDeletable.IsDeleted = true;
                        dbEntry.CurrentValues[nameof(ISoftDeletable.IsDeleted)] = true;

                        if (entity is IDeletionTrackable deletionTrackable)
                        {
                            deletionTrackable.DeletedUtc = utcNow;
                            dbEntry.CurrentValues[nameof(IDeletionTrackable.DeletedUtc)] = utcNow;
                        }
                    }
                    break;

                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// If we set <see cref="EntityEntry.State"/> to <see cref="EntityState.Modified"/> on entity with
        /// empty <see cref="ICreationTrackable.CreatedUtc"/> or <see cref="ICreationAuditable.CreatorUserId"/>
        /// we should not overwrite database values.
        /// https://github.com/gnaeus/EntityFramework.CommonTools/issues/4
        /// </summary>
        private static void PreventPropertyOverwrite<TProperty>(EntityEntry dbEntry, string propertyName)
        {
            var propertyEntry = dbEntry.Property(propertyName);

            if (propertyEntry.IsModified && Equals(dbEntry.CurrentValues[propertyName], default(TProperty)))
            {
                propertyEntry.IsModified = false;
            }
        }
    }
}
