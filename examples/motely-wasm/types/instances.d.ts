export declare const instances: {
    /** Invokes the specified factory to create and register an exported instance wrapper associated with the ID,
     *  unless an exported instance is already registered under the ID, in which case returns its wrapper. */
    export(id: number, factory: (id: number) => object): object;
    /** Registers specified imported instance and associates it with a unique ID, unless it's already registered,
     *  in which case the ID of the registered instance is returned. */
    import(instance: object, factory?: (id: number) => () => void): number;
    /** Returns a registered imported instance associated with the specified ID. */
    imported(id: number): object;
    /** Invoked from C# to notify that the imported (JS -> C#) instance is no longer used
     *  (eg, was garbage collected) and can be released on the JavaScript side as well.
     *  @param id Unique identifier of the disposed instance. */
    disposeImported(id: number): void;
};
