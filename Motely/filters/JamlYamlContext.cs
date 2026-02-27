using YamlDotNet.Serialization;

namespace Motely.Filters;

[YamlStaticContext]
[YamlSerializable(typeof(JamlDto))]
[YamlSerializable(typeof(JamlClauseDto))]
[YamlSerializable(typeof(JamlDefaultsDto))]
[YamlSerializable(typeof(JamlSourcesDto))]
public partial class JamlYamlContext : StaticContext { }
