using YamlDotNet.RepresentationModel;

var jaml = File.ReadAllText(@"JamlFilters\sixtid.jaml");

var yaml = new YamlStream();
using (var r = new StringReader(jaml))
    yaml.Load(r);

using var writer = new StringWriter();
yaml.Save(writer, assignAnchors: false);
var result = writer.ToString();

Console.WriteLine(result);
