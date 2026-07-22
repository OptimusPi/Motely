// Positional records compile to init-only setters, and init-only setters compile against
// this modreq type — present in net5.0+, absent from netstandard2.0. Declaring it internal
// here is the standard shim; the compiler only needs the name to exist.
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit;
