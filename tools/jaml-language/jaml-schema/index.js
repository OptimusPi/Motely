import schema from "./schemas/jaml.schema.json" with { type: "json" };

// The generator (Motely.CLI JamlSchemaGenerator) runs System.Text.Json.Schema.JsonSchemaExporter
// over the JAML DTO graph. The clause DTO is deduplicated by the exporter under the first usage
// it encounters — properties.must.items — and `and`/`or`/`clauses` then $ref back to that path.
// Keys are discovered structurally; don't re-hardcode them.
const rootKeys = Object.freeze(Object.keys(schema.properties ?? {}));
const clauseKeys = Object.freeze(
    Object.keys(schema.properties?.must?.items?.properties ?? {}),
);

export default schema;
export { clauseKeys, rootKeys, schema };
