namespace Guncho
{
    public enum RealmEditingOutcome
    {
        /// <summary>
        /// The realm was successfully recompiled and reloaded.
        /// </summary>
        Success,
        /// <summary>
        /// There was no source code to compile.
        /// </summary>
        Missing,
        /// <summary>
        /// Whoever's trying to edit the realm doesn't have the right access level.
        /// </summary>
        PermissionDenied,
        /// <summary>
        /// Inform 7 failed to translate the realm.
        /// </summary>
        NiError,
        /// <summary>
        /// Inform 7 translated the realm, but Inform 6 failed to compile it.
        /// </summary>
        InfError,
        /// <summary>
        /// The realm was compiled, but it couldn't be loaded.
        /// </summary>
        VMError,
    }
}
