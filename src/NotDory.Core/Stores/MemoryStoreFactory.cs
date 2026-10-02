namespace NotDory.Core.Stores
{
    using System;
    using NotDory.Core.Enums;
    using NotDory.Core.Models;
    using NotDory.Core.Stores.Filesystem;
    using NotDory.Core.Stores.RecallDb;

    /// <summary>
    /// Creates the appropriate <see cref="IMemoryStore"/> for a scope based on its configured provider.
    /// </summary>
    public static class MemoryStoreFactory
    {
        #region Public-Methods

        /// <summary>
        /// Create a memory store for the given provider.
        /// </summary>
        /// <param name="provider">The store provider.</param>
        /// <returns>A memory store.</returns>
        public static IMemoryStore Create(StoreProviderEnum provider)
        {
            switch (provider)
            {
                case StoreProviderEnum.RecallDb:
                    return new RecallDbMemoryStore();
                case StoreProviderEnum.Filesystem:
                    return new FilesystemMemoryStore();
                default:
                    throw new NotSupportedException("Unknown store provider: " + provider + ".");
            }
        }

        /// <summary>
        /// Create a memory store for the given scope using default (unconfigured) external clients.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <returns>A memory store.</returns>
        /// <exception cref="ArgumentNullException">Thrown when scope is null.</exception>
        public static IMemoryStore Create(Scope scope)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            return WithMirror(scope, Create(scope.StoreProvider));
        }

        /// <summary>
        /// Create a memory store for the given scope, configuring external clients from the supplied options.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="options">Store connection options.</param>
        /// <returns>A memory store.</returns>
        /// <exception cref="ArgumentNullException">Thrown when scope is null.</exception>
        public static IMemoryStore Create(Scope scope, StoreOptions? options)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));

            switch (scope.StoreProvider)
            {
                case StoreProviderEnum.RecallDb:
                    if (options != null && !string.IsNullOrEmpty(options.RecallDbEndpoint) && !string.IsNullOrEmpty(options.RecallDbAdminKey))
                    {
                        return WithMirror(scope, new RecallDbMemoryStore(options.RecallDbEndpoint!, options.RecallDbAdminKey!));
                    }

                    return WithMirror(scope, new RecallDbMemoryStore());
                case StoreProviderEnum.Filesystem:
                    return new FilesystemMemoryStore();
                default:
                    throw new NotSupportedException("Unknown store provider: " + scope.StoreProvider + ".");
            }
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Wrap a RecallDb scope's store in a <see cref="MirroredMemoryStore"/> when the scope mirrors to the filesystem.
        /// A Filesystem scope already writes files, so its flag is ignored.
        /// </summary>
        private static IMemoryStore WithMirror(Scope scope, IMemoryStore store)
        {
            if (scope.FilesystemMirror && scope.StoreProvider == StoreProviderEnum.RecallDb) return new MirroredMemoryStore(store);
            return store;
        }

        #endregion
    }
}
