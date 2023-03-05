namespace FSharp.Data

/// This is the public inference mode enum used when initializing a type provider,
/// with backward compatibility.
type InferenceMode =
    /// Used as a default value for backward compatibility with the legacy InferTypesFromValues boolean static parameter.
    /// The actual behaviour will depend on whether InferTypesFromValues is set to true (default) or false.
    | BackwardCompatible = 0
    /// Type everything as strings
    /// (or the most basic type possible for the value when it's not string, e.g. for json numbers or booleans).
    | NoInference = 1
    /// Infer types from values only. Inline schemas are disabled.
    | ValuesOnly = 2
    /// Inline schemas types have the same weight as value infered types.
    | ValuesAndInlineSchemasHints = 3
    /// Inline schemas types override value infered types. (Value infered types are ignored if an inline schema is present)
    | ValuesAndInlineSchemasOverrides = 4
