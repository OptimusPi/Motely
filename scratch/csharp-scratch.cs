#!/usr/bin/env dotnet

#:property TargetFramework=net10.0
#:property PublishAot=false

using System.Text.Json;

var payload = new
{
    Message = "Motely C# scratchpad is alive.",
    Now = DateTimeOffset.Now,
    Args = args,
};

Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions
{
    WriteIndented = true,
}));